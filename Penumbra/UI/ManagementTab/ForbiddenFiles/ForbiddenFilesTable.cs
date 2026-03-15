using ImSharp;
using ImSharp.Table;
using Luna;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.String.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class ForbiddenFilesTable(ModManager mods, TextureManager textures, UiNavigator navigator, Configuration config)
    : TableBase<ForbiddenFileCacheObject, ScannerTabCache<ForbiddenFileCacheObject, ForbiddenFileRedirection>>(new StringU8("##fft"u8),
        new ActionColumn(),
        new GamePathColumn<ForbiddenFileCacheObject, ForbiddenFileRedirection>{ Label = new StringU8("Game Path"u8)},
        new StateColumn { Label                                                       = new StringU8("State"u8) },
        new TargetColumn<ForbiddenFileCacheObject, ForbiddenFileRedirection> { Label  = new StringU8("Target File"u8) },
        new ModColumn(navigator) { Label                                              = new StringU8("Mod"u8) },
        new ContainerColumn(navigator) { Label                                        = new StringU8("Option"u8) })
{
    /// <remarks> Implemented in the cache due to use of scanner. </remarks>>
    public override IEnumerable<ForbiddenFileCacheObject> GetItems()
        => [];

    protected override void PreDraw(in ScannerTabCache<ForbiddenFileCacheObject, ForbiddenFileRedirection> cache)
    {
        cache.DrawScanButtons();

        var active = config.DeleteModModifier.IsActive();
        if (ImEx.Button("Remove All Redundant Redirections"u8, default, !active))
            RemoveRedundant(cache);
    }

    protected override ScannerTabCache<ForbiddenFileCacheObject, ForbiddenFileRedirection> CreateCache()
        => new Cache(mods, textures, this);

    private sealed class Cache(ModManager mods, TextureManager textures, ForbiddenFilesTable parent)
        : ScannerTabCache<ForbiddenFileCacheObject, ForbiddenFileRedirection>(parent, new ForbiddenFileScanner(mods, textures))
    {
        protected override ForbiddenFileCacheObject Convert(ForbiddenFileRedirection obj)
            => new(obj);
    }

    private sealed class ActionColumn : BasicColumn<ForbiddenFileCacheObject>
    {
        public ActionColumn()
            => Flags |= TableColumnFlags.NoSort | TableColumnFlags.NoResize;

        public override void DrawColumn(in ForbiddenFileCacheObject item, int globalIndex)
        { }

        public override float ComputeWidth(IEnumerable<ForbiddenFileCacheObject> _)
            => Im.Style.FrameHeight;
    }

    private sealed class ModColumn(UiNavigator navigator) : ModColumn<ForbiddenFileCacheObject>(navigator)
    {
        protected override Mod? GetMod(in ForbiddenFileCacheObject item, int globalIndex)
            => item.ScannedObject.Container.TryGetTarget(out var c) ? c.Mod as Mod : null;

        protected override StringPair GetModName(in ForbiddenFileCacheObject item, int globalIndex)
            => item.Mod;
    }

    private sealed class ContainerColumn(UiNavigator navigator) : ModColumn<ForbiddenFileCacheObject>(navigator)
    {
        protected override Mod? GetMod(in ForbiddenFileCacheObject item, int globalIndex)
            => item.ScannedObject.Container.TryGetTarget(out var c) ? c.Mod as Mod : null;

        protected override StringPair GetModName(in ForbiddenFileCacheObject item, int globalIndex)
            => item.Container;
    }

    private sealed class StateColumn : TextColumn<ForbiddenFileCacheObject>
    {
        protected override string ComparisonText(in ForbiddenFileCacheObject item, int globalIndex)
            => item.State;

        protected override StringU8 DisplayText(in ForbiddenFileCacheObject item, int globalIndex)
            => item.State;

        public override float ComputeWidth(IEnumerable<ForbiddenFileCacheObject> _)
            => ForbiddenFileCacheObject.Different.Utf8.CalculateSize().X;
    }

    private void RemoveRedundant(ScannerTabCache<ForbiddenFileCacheObject, ForbiddenFileRedirection> cache, bool dryRun = true)
    {
        var files   = new SetDictionary<Mod, FullPath>();
        var indices = new List<int>(cache.AllItems.Count);
        foreach (var group in cache.AllItems.Select((r, i) => (r.ScannedObject, i)).GroupBy(r => r.ScannedObject.Container.TryGetTarget(out var container) ? container : null).ToList())
        {
            if (group.Key is not { } container)
                continue;

            var swaps = container.FileSwaps.ToDictionary();
            var redirections = container.Files.ToDictionary();

            foreach (var (redirection, index) in group)
            {
                if (redirection.FileSwap)
                {
                    swaps.Remove(redirection.GamePath);
                    Penumbra.Log.Debug(
                        $"[ForbiddenFiles] Removed forbidden file swap {redirection.GamePath} -> {redirection.FilePath} in {container.Mod.Name} - {container.GetFullName()}.");
                    indices.Add(index);
                }
                else if (redirection.ConceptuallyEqual || redirection.Missing || redirection.Broken)
                {
                    redirections.Remove(redirection.GamePath, out var file);
                    files.TryAdd((Mod)container.Mod, file);
                    Penumbra.Log.Debug(
                        $"[ForbiddenFiles] Removed forbidden file redirection {redirection.GamePath} -> {redirection.FilePath} in {container.Mod.Name} - {container.GetFullName()} because the target file was {(redirection.Broken ? "broken." : redirection.Missing ? "missing." : "conceptually equal.")}");
                    indices.Add(index);
                }
            }

            if (swaps.Count < container.FileSwaps.Count)
            {
                Penumbra.Log.Information(
                    $"[ForbiddenFiles] Removed {container.FileSwaps.Count - swaps.Count} forbidden file swaps in {container.Mod.Name} - {container.GetFullName()}.");
                if (!dryRun)
                    mods.OptionEditor.SetFileSwaps(container, swaps);
            }

            if (redirections.Count < container.Files.Count)
            {
                Penumbra.Log.Information(
                    $"[ForbiddenFiles] Removed {container.Files.Count - redirections.Count} forbidden file redirections in {container.Mod.Name} - {container.GetFullName()}.");
                if (!dryRun)
                    mods.OptionEditor.SetFiles(container, redirections);
            }
        }

        indices.Sort();
        foreach (var idx in indices.AsEnumerable().Reverse())
            cache.DeleteSingleItem(idx);

        foreach (var (mod, modFiles) in files.Grouped)
        {
            var collectedUsage = mod.AllDataContainers.SelectMany(m => m.Files.Values).ToHashSet();
            foreach (var file in modFiles)
            {
                if (!file.Exists)
                    continue;

                if (collectedUsage.Contains(file))
                    continue;

                try
                {
                    if (!dryRun)
                        File.Delete(file.FullName);
                    Penumbra.Log.Information(
                        $"[ForbiddenFiles] Deleted now unused file {file.FullName} after removing it from forbidden file redirections.");
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error(
                        $"[ForbiddenFiles] Unable to delete forbidden file {file.FullName} removed from all redirections:\n{ex}");
                }
            }
        }
    }
}
