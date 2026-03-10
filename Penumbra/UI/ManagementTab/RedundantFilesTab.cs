using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.String.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class RedundantFilesTab(ModManager mods, IDataManager dataManager, UiNavigator navigator) : ITab<ManagementTabType>
{
    public ReadOnlySpan<byte> Label
        => "Redundant Files"u8;

    public void DrawContent()
    {
        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(mods, dataManager));
        if (Im.Button("Scan"u8))
            cache.Scanner.ScanRedirections();
        Im.Line.Same();
        var running = cache.Scanner.Running;
        if (ImEx.Button("Cancel"u8, default, StringU8.Empty, !running))
            cache.Scanner.Cancel();
        if (running)
        {
            Im.Line.Same();
            Im.ProgressBar(cache.Scanner.Progress, ImEx.ScaledVectorX(200));
        }

        using var table = Im.Table.Begin("t"u8, 4, TableFlags.RowBackground | TableFlags.SizingFixedFit, Im.ContentRegion.Available);
        if (!table)
            return;

        var       data = cache.Scanner.GetCurrentList();
        var       id   = new Im.IdDisposable();
        using var clip = new Im.ListClipper(data.Count, Im.Style.TextHeightWithSpacing);
        foreach (var (idx, file) in clip.Iterate(data).Index())
        {
            id.Push(idx);
            if (!file.Container.TryGetTarget(out var container))
                continue;

            table.DrawColumn($"{file.GamePath}");
            table.DrawColumn(file.Redirection.FullName);
            table.NextColumn();
            if (Im.Selectable(container.GetFullName()))
                navigator.OpenTo(container.Mod as Mod);
            table.DrawColumn(file.Size < 0 ? "MISSING"u8 : FormattingFunctions.HumanReadableSize(file.Size));
            id.Pop();
        }
    }

    private sealed class Cache(ModManager mods, IDataManager dataManager) : BasicCache(TimeSpan.FromMinutes(10))
    {
        public sealed class RedundantScannedRedirection(Utf8GamePath gamePath, FullPath redirection, IModDataContainer container, bool swap)
            : BaseScannedRedirection(gamePath, redirection, container, swap)
        {
            public readonly long Size = File.Exists(redirection.FullName) ? new FileInfo(redirection.FullName).Length : -1;
        }

        public sealed class RedundantRedirectionScanner(ModManager mods, IDataManager dataManager)
            : RedirectionScanner<RedundantScannedRedirection>(mods)
        {
            protected override RedundantScannedRedirection Create(Utf8GamePath gamePath, FullPath redirection, IModDataContainer container,
                bool swap)
                => new(gamePath, redirection, container, swap);

            protected override bool DoCreateRedirection(Utf8GamePath gamePath, FullPath redirection, IModDataContainer container, bool swap)
            {
                if (swap)
                    return false;

                if (!File.Exists(redirection.FullName))
                    return true;

                if (!gamePath.Contains("/"))
                    return true;

                if (dataManager.GetFile(gamePath.ToString()) is not { } originalFile)
                    return false;

                var bytes = File.ReadAllBytes(redirection.FullName);
                return originalFile.Data.SequenceEqual(bytes);
            }
        }

        public readonly RedundantRedirectionScanner Scanner = new(mods, dataManager);

        public override void Update()
        { }
    }

    public ManagementTabType Identifier
        => ManagementTabType.RedundantFiles;
}
