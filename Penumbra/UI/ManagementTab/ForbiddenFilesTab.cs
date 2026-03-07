using System.Collections.Frozen;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using Penumbra.Mods.Manager;
using Penumbra.String;

namespace Penumbra.UI.ManagementTab;

public sealed class ForbiddenFilesTab(ModManager mods, IDataManager dataManager, Configuration config) : ITab<ManagementTabType>
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

    private sealed class Cache(IDataManager dataManager) : BasicCache
    {
        public FrozenDictionary<uint, byte[]>? OriginalFiles;

        public override void Update()
        {
            OriginalFiles ??= ForbiddenFiles.ToFrozenDictionary(kvp => kvp.Key, kvp =>
            {
                var file = dataManager.GetFile(kvp.Value.ToString());
                return file?.Data ?? throw new Exception($"Forbidden file {kvp.Value} could not be loaded from game files.");
            });
        }
    }

    public void DrawContent()
    {
        using var child = Im.Child.Begin("c"u8, Im.ContentRegion.Available);
        if (!child)
            return;

        Im.Text("WORK IN PROGRESS"u8);

        //using var table = Im.Table.Begin("t"u8, 6, TableFlags.RowBackground | TableFlags.SizingFixedFit);
        //if (!table)
        //    return;
        //
        //var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new Cache(dataManager));
        //foreach (var mod in mods)
        //{
        //    foreach (var option in mod.AllDataContainers)
        //    {
        //        foreach (var redirection in option.Files.Where(f => ForbiddenFiles.ContainsKey((uint)f.Key.Path.Crc32)))
        //        {
        //            table.DrawColumn($"{redirection.Key}");
        //            table.DrawColumn(redirection.Value.FullName);
        //            table.DrawColumn(mod.Name);
        //            table.DrawColumn(option.GetFullName());
        //            table.DrawColumn("R"u8);
        //            try
        //            {
        //                var file = File.ReadAllBytes(redirection.Value.FullName);
        //                if (file.SequenceEqual(cache.OriginalFiles![(uint)redirection.Key.Path.Crc32]))
        //                    table.DrawColumn("EQUAL"u8);
        //                else
        //                    table.DrawColumn("DIFF"u8);
        //            }
        //            catch
        //            {
        //                table.DrawColumn("MISSING"u8);
        //            }
        //        }
        //
        //        foreach (var swap in option.FileSwaps.Where(f => ForbiddenFiles.ContainsKey((uint)f.Key.Path.Crc32)))
        //        {
        //            table.DrawColumn($"{swap.Key}");
        //            table.DrawColumn(swap.Value.FullName);
        //            table.DrawColumn(mod.Name);
        //            table.DrawColumn(option.GetFullName());
        //            table.DrawColumn("S"u8);
        //            table.NextColumn();
        //        }
        //    }
        //}
    }

    public ManagementTabType Identifier
        => ManagementTabType.ForbiddenFiles;
}
