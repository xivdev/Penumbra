using ImSharp;
using Luna;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ManagementTab;

public sealed class UnusedFilesTab(ModManager mods, UiNavigator navigator) : ITab<ManagementTabType>
{
    public ReadOnlySpan<byte> Label
        => "Unused Files (WIP)"u8;

    public void DrawContent()
    {
        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(mods));
        ManagementTab.DrawScanButtons(cache.Scanner);

        using var table = Im.Table.Begin("t"u8, 3, TableFlags.RowBackground | TableFlags.SizingFixedFit, Im.ContentRegion.Available);
        if (!table)
            return;

        var data = cache.Scanner.GetCurrentList();
        var id   = new Im.IdDisposable();
        foreach (var (idx, file) in data.Index())
        {
            id.Push(idx);
            if (!file.Mod.TryGetTarget(out var mod))
                continue;

            table.DrawColumn(file.RelativePath);
            table.NextColumn();
            if(Im.Selectable(mod.Name))
                navigator.OpenTo(mod);
            table.DrawColumn(FormattingFunctions.HumanReadableSize(file.Size));
            id.Pop();
        }
    }

    public ManagementTabType Identifier
        => ManagementTabType.UnusedFiles;

    private sealed class Cache(ModManager mods) : BasicCache(TimeSpan.FromMinutes(10))
    {
        public sealed class UnusedScannedFile(string filePath, Mod mod) : BaseScannedFile(filePath, mod)
        {
            public readonly long Size = new FileInfo(filePath).Length;
        }

        public sealed class UnusedFileScanner(ModManager mods) : ModFileScanner<UnusedScannedFile>(mods)
        {
            protected override UnusedScannedFile Create(string fileName, Mod mod)
                => new(fileName, mod);

            protected override bool DoCreateFile(string fileName, Mod mod)
            {
                if (!File.Exists(fileName))
                    return false;

                foreach (var container in mod.AllDataContainers)
                {
                    if (container.Files.Values.Any(f => string.Equals(fileName, f.FullName, StringComparison.OrdinalIgnoreCase)))
                        return false;
                }

                return true;
            }
        }

        public readonly UnusedFileScanner Scanner = new(mods);

        public override void Update()
        { }
    }
}
