using ImSharp;
using ImSharp.Table;
using Luna;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class ForbiddenFilesTable(ModManager mods, TextureManager textures, UiNavigator navigator, Configuration config)
    : TableBase<ForbiddenFileCacheObject, ScannerTabCache<ForbiddenFileCacheObject, ForbiddenFileRedirection>>(new StringU8("##fft"u8),
        new ActionColumn(mods, config),
        new GamePathColumn<ForbiddenFileCacheObject, ForbiddenFileRedirection> { Label = new StringU8("Game Path"u8) },
        new StateColumn { Label                                                        = new StringU8("State"u8) },
        new TargetColumn<ForbiddenFileCacheObject, ForbiddenFileRedirection> { Label   = new StringU8("Target File"u8) },
        new ModColumn(navigator) { Label                                               = new StringU8("Mod"u8) },
        new ContainerColumn(navigator) { Label                                         = new StringU8("Option"u8) })
{
    private const bool DryRun = false;

    /// <remarks> Implemented in the cache due to use of scanner. </remarks>>
    public override IEnumerable<ForbiddenFileCacheObject> GetItems()
        => [];

    protected override void PreDraw(in ScannerTabCache<ForbiddenFileCacheObject, ForbiddenFileRedirection> cache)
    {
        cache.DrawScanButtons();

        var active = config.DeleteModModifier.IsActive();
        if (ImEx.Button("Remove All Simple Redirections"u8, default, !active))
            RemoveRedundant(cache);

        if (Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
        {
            using var tt = Im.Tooltip.Begin();
            Im.Text("Executing this will"u8);
            Im.BulletText("Remove all listed file swaps, as they can not be reasonable."u8);
            Im.BulletText("Remove all redirections listed as 'Broken', as they could not be read and can not be useful."u8);
            Im.BulletText("Remove all redirections listed as 'Missing', as their target files do not exist and thus can not be useful."u8);
            Im.BulletText(
                "Remove all redirections listed as 'Equal', as the target files are equivalent to the game files and thus the redirections are not meaningful."u8);
            Im.BulletText("Delete all target files who have no remaining redirections in their mods left afterwards."u8);
            Im.Text("\nTHIS IS NOT REVERTIBLE."u8, Colors.RegexWarningBorder);

            if (!active)
                Im.Text($"\nHold {config.DeleteModModifier} while clicking.");
        }
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
        private readonly ModManager    _mods;
        private readonly Configuration _config;
        private          int           _deleteIndex = -1;

        public ActionColumn(ModManager mods, Configuration config)
        {
            _mods   =  mods;
            _config =  config;
            Flags   |= TableColumnFlags.NoSort | TableColumnFlags.NoResize;
        }

        public override void PostDraw(in TableCache<ForbiddenFileCacheObject> cache)
        {
            if (_deleteIndex is -1)
                return;

            cache.DeleteSingleItem(_deleteIndex);
            _deleteIndex = -1;
        }

        public override void DrawColumn(in ForbiddenFileCacheObject item, int globalIndex)
        {
            var disabled = !_config.DeleteModModifier.IsActive();
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon,
                    item.ScannedObject.FileSwap
                        ? "Remove this file swap."u8
                        : "Remove this redirection and delete the target file if it was the last redirection in the mod referencing it."u8,
                    disabled))
            {
                if (item.ScannedObject.Container.TryGetTarget(out var container))
                {
                    if (item.ScannedObject.FileSwap)
                    {
                        var swaps = container.FileSwaps.ToDictionary();
                        if (swaps.Remove(item.ScannedObject.GamePath, out _))
                        {
                            if (!DryRun)
                                _mods.OptionEditor.SetFileSwaps(container, swaps);
                            Penumbra.Log.Debug(
                                $"[ForbiddenFiles] Removed forbidden file swap {item.ScannedObject.GamePath} -> {item.ScannedObject.FilePath} in {container.Mod.Name} - {container.GetFullName()}.");
                        }
                    }
                    else
                    {
                        var redirections = container.Files.ToDictionary();
                        if (redirections.Remove(item.ScannedObject.GamePath, out var file))
                        {
                            if (!DryRun)
                                _mods.OptionEditor.SetFiles(container, redirections);
                            Penumbra.Log.Debug(
                                $"[ForbiddenFiles] Removed forbidden file redirection {item.ScannedObject.GamePath} -> {item.ScannedObject.FilePath} in {container.Mod.Name} - {container.GetFullName()}.");
                            if (file.Exists && ((Mod)container.Mod).AllDataContainers.All(c => !c.Files.ContainsValue(file)))
                                DeleteFile(file);
                        }
                    }
                }

                _deleteIndex = globalIndex;
            }

            if (disabled)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"\nHold {_config.DeleteModModifier} while clicking to remove.");
        }

        public override float ComputeWidth(IEnumerable<ForbiddenFileCacheObject> _)
            => Im.Style.FrameHeight;
    }

    private sealed class ModColumn(UiNavigator navigator) : ModColumn<ForbiddenFileCacheObject>(navigator)
    {
        protected override Mod? GetMod(in ForbiddenFileCacheObject item, int globalIndex)
            => item.ScannedObject.Container.TryGetTarget(out var c) ? c.Mod as Mod : null;

        protected override StringPair GetModName(in ForbiddenFileCacheObject item, int globalIndex)
            => item.Mod;

        private string _lastMod = string.Empty;

        protected override bool MatchesLastItem(in ForbiddenFileCacheObject item)
        {
            var ret = _lastMod == item.Mod.Utf16;
            _lastMod = item.Mod.Utf16;
            return ret;
        }

        public override void PostDraw(in TableCache<ForbiddenFileCacheObject> cache)
        {
            _lastMod = string.Empty;
        }
    }

    private sealed class ContainerColumn(UiNavigator navigator) : ModColumn<ForbiddenFileCacheObject>(navigator)
    {
        protected override Mod? GetMod(in ForbiddenFileCacheObject item, int globalIndex)
            => item.ScannedObject.Container.TryGetTarget(out var c) ? c.Mod as Mod : null;

        protected override StringPair GetModName(in ForbiddenFileCacheObject item, int globalIndex)
            => item.Container;

        private string _lastMod       = string.Empty;
        private string _lastContainer = string.Empty;

        protected override bool MatchesLastItem(in ForbiddenFileCacheObject item)
        {
            var ret = _lastMod == item.Mod.Utf16 && _lastContainer == item.Container.Utf16;
            _lastMod       = item.Mod.Utf16;
            _lastContainer = item.Container.Utf16;
            return ret;
        }

        public override void PostDraw(in TableCache<ForbiddenFileCacheObject> cache)
        {
            _lastMod       = string.Empty;
            _lastContainer = string.Empty;
        }
    }

    private sealed class StateColumn : TextColumn<ForbiddenFileCacheObject>
    {
        protected override string ComparisonText(in ForbiddenFileCacheObject item, int globalIndex)
            => item.State;

        protected override StringU8 DisplayText(in ForbiddenFileCacheObject item, int globalIndex)
            => item.State;

        protected override void DrawTooltip(in ForbiddenFileCacheObject item, int globalIndex)
        {
            using var tt = Im.Tooltip.Begin();
            Im.Text(item.ScannedObject.FileSwap
                ? "There can no be any file swaps that are valid and useful for this kind of file."u8
                : item.ScannedObject.Broken
                    ? "The scanner was unable to read or parse this file, so it is invalid and should be removed."u8
                    : item.ScannedObject.Missing
                        ? "The file this is redirected to does not exist, so the redirection should just be removed."u8
                        : item.ScannedObject.ConceptuallyEqual
                            ? "The file this is redirected to is equivalent to the original file, so the redirection can just be removed without consequences."u8
                            : "This file is conceptually different from the original game file. The mod may have to be fixed by its creator.\n\nYou can freely remove this redirection to silence the warning, as it is not applied either way, but the mod may not work as intended."u8);
        }

        public override void DrawColumn(in ForbiddenFileCacheObject item, int globalIndex)
        {
            base.DrawColumn(in item, globalIndex);
            if (item.State.Utf16 is not "Different")
                return;

            Im.Line.SameInner();
            ImEx.Icon.Draw(LunaStyle.WarningIcon, Rgba32.Yellow);
            if (Im.Item.Hovered())
                DrawTooltip(item, globalIndex);
        }

        public override float ComputeWidth(IEnumerable<ForbiddenFileCacheObject> _)
            => ForbiddenFileCacheObject.Different.Utf8.CalculateSize().X + Im.Style.FrameHeightWithSpacing;
    }

    private void RemoveRedundant(ScannerTabCache<ForbiddenFileCacheObject, ForbiddenFileRedirection> cache)
    {
        var files   = new SetDictionary<Mod, FullPath>();
        var indices = new List<int>(cache.AllItems.Count);
        foreach (var group in cache.AllItems.Select((r, i) => (r.ScannedObject, i))
                     .GroupBy(r => r.ScannedObject.Container.TryGetTarget(out var container) ? container : null).ToList())
        {
            if (group.Key is not { } container)
                continue;

            var swaps        = container.FileSwaps.ToDictionary();
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
                if (!DryRun)
                    mods.OptionEditor.SetFileSwaps(container, swaps);
            }

            if (redirections.Count < container.Files.Count)
            {
                Penumbra.Log.Information(
                    $"[ForbiddenFiles] Removed {container.Files.Count - redirections.Count} forbidden file redirections in {container.Mod.Name} - {container.GetFullName()}.");
                if (!DryRun)
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

                if (!collectedUsage.Contains(file))
                    DeleteFile(file);
            }
        }
    }

    private static void DeleteFile(in FullPath file)
    {
        try
        {
            if (!DryRun)
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
