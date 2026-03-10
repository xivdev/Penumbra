using System.Collections.Frozen;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class ForbiddenFilesTab(ModManager mods, IDataManager dataManager) : ITab<ManagementTabType>
{
    public static readonly FrozenDictionary<uint, CiByteString> ForbiddenFiles = (((uint, CiByteString)[])
    [
        (0x90E4EE2F, new CiByteString("common/graphics/texture/dummy.tex"u8,    MetaDataComputation.All)),
        (0x84815A1A, new CiByteString("chara/common/texture/white.tex"u8,       MetaDataComputation.All)),
        (0x749091FB, new CiByteString("chara/common/texture/black.tex"u8,       MetaDataComputation.All)),
        (0x5CB9681A, new CiByteString("chara/common/texture/id_16.tex"u8,       MetaDataComputation.All)),
        (0x7E78D000, new CiByteString("chara/common/texture/red.tex"u8,         MetaDataComputation.All)),
        (0xBDC0BFD3, new CiByteString("chara/common/texture/green.tex"u8,       MetaDataComputation.All)),
        (0xC410E850, new CiByteString("chara/common/texture/blue.tex"u8,        MetaDataComputation.All)),
        (0xD5CFA221, new CiByteString("chara/common/texture/null_normal.tex"u8, MetaDataComputation.All)),
        (0xBE48CA67, new CiByteString("chara/common/texture/skin_mask.tex"u8,   MetaDataComputation.All)),
    ]).ToFrozenDictionary(p => p.Item1, p =>
    {
        Debug.Assert((uint)p.Item2.Crc32 == p.Item1,
            $"Invalid hash computation in forbidden files for {p.Item2} ({p.Item1:X} vs {p.Item2.Crc32:X}).");
        return p.Item2;
    });


    public ReadOnlySpan<byte> Label
        => "Forbidden Files"u8;

    private sealed class Cache(ModManager mods, IDataManager dataManager) : BasicCache
    {
        public sealed class ForbiddenFileRedirection(
            Utf8GamePath path,
            FullPath redirection,
            IModDataContainer container,
            bool swap,
            byte[]? data,
            bool missing,
            bool bytewiseEqual,
            bool conceptuallyEqual)
            : BaseScannedRedirection(path, redirection, container, swap)
        {
            public readonly byte[]? Data              = data;
            public readonly bool    Missing           = missing;
            public readonly bool    BytewiseEqual     = bytewiseEqual;
            public readonly bool    ConceptuallyEqual = conceptuallyEqual;
        }

        public sealed class ForbiddenFileScanner(ModManager mods) : RedirectionScanner<ForbiddenFileRedirection>(mods)
        {
            public FrozenDictionary<uint, byte[]>? OriginalFiles;

            protected override ForbiddenFileRedirection Create(Utf8GamePath path, FullPath redirection, IModDataContainer container, bool swap)
            {
                if (swap)
                    return new ForbiddenFileRedirection(path, redirection, container, true, null, false, false, false);

                try
                {
                    if (!File.Exists(redirection.FullName))
                        return new ForbiddenFileRedirection(path, redirection, container, false, null, true, false, false);

                    var data = File.ReadAllBytes(redirection.FullName);
                    return new ForbiddenFileRedirection(path, redirection, container, false, data, false,
                        data.SequenceEqual(OriginalFiles![(uint)path.Path.Crc32]), false);
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Warning($"Could not read forbidden file redirection file {redirection}:\n{ex}");
                    return new ForbiddenFileRedirection(path, redirection, container, false, null, false, false, false);
                }
            }

            protected override bool DoCreateRedirection(Utf8GamePath path, FullPath redirection, IModDataContainer container, bool swap)
                => ForbiddenFiles.ContainsKey((uint)path.Path.Crc32);
        }


        public readonly ForbiddenFileScanner Redirections = new(mods);

        public override void Update()
        {
            Redirections.OriginalFiles ??= ForbiddenFiles.ToFrozenDictionary(kvp => kvp.Key, kvp =>
            {
                var file = dataManager.GetFile(kvp.Value.ToString());
                return file?.Data ?? throw new Exception($"Forbidden file {kvp.Value} could not be loaded from game files.");
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Redirections.Dispose();
        }
    }

    public void DrawContent()
    {
        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(mods, dataManager));
        if (Im.Button("Scan"u8))
            cache.Redirections.ScanRedirections();
        Im.Line.Same();
        var running = cache.Redirections.Running;
        if (ImEx.Button("Cancel"u8, default, StringU8.Empty, !running))
            cache.Redirections.Cancel();
        if (running)
        {
            Im.Line.Same();
            Im.ProgressBar(cache.Redirections.Progress);
        }

        using var table = Im.Table.Begin("t"u8, 7, TableFlags.RowBackground | TableFlags.SizingFixedFit, Im.ContentRegion.Available);
        if (!table)
            return;

        var data = cache.Redirections.GetCurrentList();
        foreach (var redirection in data)
        {
            table.DrawColumn($"{redirection.GamePath}");
            table.DrawColumn(redirection.Redirection.FullName);
            if (redirection.Container.TryGetTarget(out var container))
            {
                table.DrawColumn(container.Mod.Name);
                table.DrawColumn(container.GetFullName());
            }
            else
            {
                table.DrawColumn("MOD MISSING"u8);
                table.NextColumn();
            }

            table.DrawColumn(redirection.FileSwap ? "S"u8 : "R"u8);
            table.DrawColumn(redirection.BytewiseEqual ? "E"u8 : "D"u8);
            table.DrawColumn(redirection.ConceptuallyEqual ? "E"u8 : "D"u8);
        }
    }

    public ManagementTabType Identifier
        => ManagementTabType.ForbiddenFiles;
}
