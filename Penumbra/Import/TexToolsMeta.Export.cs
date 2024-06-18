using Penumbra.Collections.Cache;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Import;

public partial class TexToolsMeta
{
    public static void WriteTexToolsMeta(MetaFileManager manager, MetaDictionary manipulations, DirectoryInfo basePath)
    {
        var files = ConvertToTexTools(manager, manipulations);

        foreach (var (file, data) in files)
        {
            var path = Path.Combine(basePath.FullName, file);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                manager.Compactor.WriteAllBytes(path, data);
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not write meta file {path}:\n{e}");
            }
        }
    }

    public static Dictionary<string, byte[]> ConvertToTexTools(MetaFileManager manager, MetaDictionary manips)
    {
        var ret = new Dictionary<string, byte[]>();
        foreach (var group in manips.Rsp.GroupBy(ManipToPath))
        {
            if (group.Key.Length == 0)
                continue;

            var bytes = WriteRgspFile(manager, group);
            if (bytes.Length == 0)
                continue;

            ret.Add(group.Key, bytes);
        }

        foreach (var (file, dict) in SplitByFile(manips))
        {
            var bytes = WriteMetaFile(manager, file, dict);
            if (bytes.Length == 0)
                continue;

            ret.Add(file, bytes);
        }

        return ret;
    }

    private static Dictionary<string, MetaDictionary> SplitByFile(MetaDictionary manips)
    {
        var ret = new Dictionary<string, MetaDictionary>();
        foreach (var (identifier, key) in manips.Imc)
            GetDict(ManipToPath(identifier)).TryAdd(identifier, key);

        foreach (var (identifier, key) in manips.Eqp)
            GetDict(ManipToPath(identifier)).TryAdd(identifier, key);

        foreach (var (identifier, key) in manips.Eqdp)
            GetDict(ManipToPath(identifier)).TryAdd(identifier, key);

        foreach (var (identifier, key) in manips.Est)
            GetDict(ManipToPath(identifier)).TryAdd(identifier, key);

        foreach (var (identifier, key) in manips.Gmp)
            GetDict(ManipToPath(identifier)).TryAdd(identifier, key);

        ret.Remove(string.Empty);

        return ret;

        MetaDictionary GetDict(string path)
        {
            if (!ret.TryGetValue(path, out var dict))
            {
                dict = new MetaDictionary();
                ret.Add(path, dict);
            }

            return dict;
        }
    }

    private static byte[] WriteRgspFile(MetaFileManager manager, IEnumerable<KeyValuePair<RspIdentifier, RspEntry>> manips)
    {
        var       list = manips.GroupBy(m => m.Key.Attribute).ToDictionary(g => g.Key, g => g.Last());
        using var m    = new MemoryStream(45);
        using var b    = new BinaryWriter(m);
        // Version
        b.Write(byte.MaxValue);
        b.Write((ushort)2);

        var race   = list.First().Value.Key.SubRace;
        var gender = list.First().Value.Key.Attribute.ToGender();
        b.Write((byte)(race - 1));   // offset by one due to Unknown
        b.Write((byte)(gender - 1)); // offset by one due to Unknown

        if (gender == Gender.Male)
        {
            Add(RspAttribute.MaleMinSize, RspAttribute.MaleMaxSize, RspAttribute.MaleMinTail, RspAttribute.MaleMaxTail);
        }
        else
        {
            Add(RspAttribute.FemaleMinSize, RspAttribute.FemaleMaxSize, RspAttribute.FemaleMinTail, RspAttribute.FemaleMaxTail);
            Add(RspAttribute.BustMinX, RspAttribute.BustMinY, RspAttribute.BustMinZ, RspAttribute.BustMaxX, RspAttribute.BustMaxY,
                RspAttribute.BustMaxZ);
        }

        return m.GetBuffer();

        void Add(params RspAttribute[] attributes)
        {
            foreach (var attribute in attributes)
            {
                var value = list.TryGetValue(attribute, out var tmp) ? tmp.Value : CmpFile.GetDefault(manager, race, attribute);
                b.Write(value.Value);
            }
        }
    }

    private static byte[] WriteMetaFile(MetaFileManager manager, string path, MetaDictionary manips)
    {
        var headerCount = (manips.Imc.Count > 0 ? 1 : 0)
          + (manips.Eqp.Count > 0 ? 1 : 0)
          + (manips.Eqdp.Count > 0 ? 1 : 0)
          + (manips.Est.Count > 0 ? 1 : 0)
          + (manips.Gmp.Count > 0 ? 1 : 0);
        using var m = new MemoryStream();
        using var b = new BinaryWriter(m);

        // Header
        // Current TT Metadata version.
        b.Write(2u);

        // Null-terminated ASCII path.
        var utf8Path = Encoding.ASCII.GetBytes(path);
        b.Write(utf8Path);
        b.Write((byte)0);

        // Number of Headers
        b.Write((uint)headerCount);
        // Current TT Size of Headers
        b.Write((uint)12);

        // Start of Header Entries for some reason, which is absolutely useless.
        var headerStart = b.BaseStream.Position + 4;
        b.Write((uint)headerStart);

        var offset = (uint)(b.BaseStream.Position + 12 * manips.Count);
        offset += WriteData(manager, b,      offset, manips.Imc);
        offset += WriteData(b,       offset, manips.Eqdp);
        offset += WriteData(b,       offset, manips.Eqp);
        offset += WriteData(b,       offset, manips.Est);
        offset += WriteData(b,       offset, manips.Gmp);

        return m.ToArray();
    }

    private static uint WriteData(MetaFileManager manager, BinaryWriter b, uint offset, IReadOnlyDictionary<ImcIdentifier, ImcEntry> manips)
    {
        if (manips.Count == 0)
            return 0;

        b.Write((uint)MetaManipulationType.Imc);
        b.Write(offset);

        var oldPos = b.BaseStream.Position;
        b.Seek((int)offset, SeekOrigin.Begin);

        var refIdentifier = manips.First().Key;
        var baseFile      = new ImcFile(manager, refIdentifier);
        foreach (var (identifier, entry) in manips)
            ImcCache.Apply(baseFile, identifier, entry);

        var partIdx = refIdentifier.ObjectType is ObjectType.Equipment or ObjectType.Accessory
            ? ImcFile.PartIndex(refIdentifier.EquipSlot)
            : 0;

        for (var i = 0; i <= baseFile.Count; ++i)
        {
            var entry = baseFile.GetEntry(partIdx, (Variant)i);
            b.Write(entry.MaterialId);
            b.Write(entry.DecalId);
            b.Write(entry.AttributeAndSound);
            b.Write(entry.VfxId);
            b.Write(entry.MaterialAnimationId);
        }

        var size = b.BaseStream.Position - offset;
        b.Seek((int)oldPos, SeekOrigin.Begin);
        return (uint)size;
    }

    private static uint WriteData(BinaryWriter b, uint offset, IReadOnlyDictionary<EqdpIdentifier, EqdpEntryInternal> manips)
    {
        if (manips.Count == 0)
            return 0;

        b.Write((uint)MetaManipulationType.Eqdp);
        b.Write(offset);

        var oldPos = b.BaseStream.Position;
        b.Seek((int)offset, SeekOrigin.Begin);

        foreach (var (identifier, entry) in manips)
        {
            b.Write((uint)identifier.GenderRace);
            b.Write(entry.AsByte);
        }

        var size = b.BaseStream.Position - offset;
        b.Seek((int)oldPos, SeekOrigin.Begin);
        return (uint)size;
    }

    private static uint WriteData(BinaryWriter b, uint offset,
        IReadOnlyDictionary<EqpIdentifier, EqpEntryInternal> manips)
    {
        if (manips.Count == 0)
            return 0;

        b.Write((uint)MetaManipulationType.Imc);
        b.Write(offset);

        var oldPos = b.BaseStream.Position;
        b.Seek((int)offset, SeekOrigin.Begin);

        foreach (var (identifier, entry) in manips)
        {
            var numBytes = Eqp.BytesAndOffset(identifier.Slot).Item1;
            for (var i = 0; i < numBytes; ++i)
                b.Write((byte)(entry.Value >> (8 * i)));
        }

        var size = b.BaseStream.Position - offset;
        b.Seek((int)oldPos, SeekOrigin.Begin);
        return (uint)size;
    }

    private static uint WriteData(BinaryWriter b, uint offset, IReadOnlyDictionary<EstIdentifier, EstEntry> manips)
    {
        if (manips.Count == 0)
            return 0;

        b.Write((uint)MetaManipulationType.Imc);
        b.Write(offset);

        var oldPos = b.BaseStream.Position;
        b.Seek((int)offset, SeekOrigin.Begin);

        foreach (var (identifier, entry) in manips)
        {
            b.Write((ushort)identifier.GenderRace);
            b.Write(identifier.SetId.Id);
            b.Write(entry.Value);
        }

        var size = b.BaseStream.Position - offset;
        b.Seek((int)oldPos, SeekOrigin.Begin);
        return (uint)size;
    }

    private static uint WriteData(BinaryWriter b, uint offset, IReadOnlyDictionary<GmpIdentifier, GmpEntry> manips)
    {
        if (manips.Count == 0)
            return 0;

        b.Write((uint)MetaManipulationType.Imc);
        b.Write(offset);

        var oldPos = b.BaseStream.Position;
        b.Seek((int)offset, SeekOrigin.Begin);

        foreach (var entry in manips.Values)
        {
            b.Write((uint)entry.Value);
            b.Write(entry.UnknownTotal);
        }

        var size = b.BaseStream.Position - offset;
        b.Seek((int)oldPos, SeekOrigin.Begin);
        return (uint)size;
    }

    private static string ManipToPath(ImcIdentifier manip)
    {
        var path = manip.GamePath().ToString();
        var replacement = manip.ObjectType switch
        {
            ObjectType.Accessory => $"_{manip.EquipSlot.ToSuffix()}.meta",
            ObjectType.Equipment => $"_{manip.EquipSlot.ToSuffix()}.meta",
            ObjectType.Character => $"_{manip.BodySlot.ToSuffix()}.meta",
            _                    => ".meta",
        };

        return path.Replace(".imc", replacement);
    }

    private static string ManipToPath(EqdpIdentifier manip)
        => manip.Slot.IsAccessory()
            ? $"chara/accessory/a{manip.SetId.Id:D4}/a{manip.SetId.Id:D4}_{manip.Slot.ToSuffix()}.meta"
            : $"chara/equipment/e{manip.SetId.Id:D4}/e{manip.SetId.Id:D4}_{manip.Slot.ToSuffix()}.meta";

    private static string ManipToPath(EqpIdentifier manip)
        => manip.Slot.IsAccessory()
            ? $"chara/accessory/a{manip.SetId.Id:D4}/a{manip.SetId.Id:D4}_{manip.Slot.ToSuffix()}.meta"
            : $"chara/equipment/e{manip.SetId.Id:D4}/e{manip.SetId.Id:D4}_{manip.Slot.ToSuffix()}.meta";

    private static string ManipToPath(EstIdentifier manip)
    {
        var raceCode = Names.CombinedRace(manip.Gender, manip.Race).ToRaceCode();
        return manip.Slot switch
        {
            EstType.Hair => $"chara/human/c{raceCode}/obj/hair/h{manip.SetId.Id:D4}/c{raceCode}h{manip.SetId.Id:D4}_hir.meta",
            EstType.Face => $"chara/human/c{raceCode}/obj/face/h{manip.SetId.Id:D4}/c{raceCode}f{manip.SetId.Id:D4}_fac.meta",
            EstType.Body => $"chara/equipment/e{manip.SetId.Id:D4}/e{manip.SetId.Id:D4}_{EquipSlot.Body.ToSuffix()}.meta",
            EstType.Head => $"chara/equipment/e{manip.SetId.Id:D4}/e{manip.SetId.Id:D4}_{EquipSlot.Head.ToSuffix()}.meta",
            _            => throw new ArgumentOutOfRangeException(),
        };
    }

    private static string ManipToPath(GmpIdentifier manip)
        => $"chara/equipment/e{manip.SetId.Id:D4}/e{manip.SetId.Id:D4}_{EquipSlot.Head.ToSuffix()}.meta";


    private static string ManipToPath(KeyValuePair<RspIdentifier, RspEntry> manip)
        => $"chara/xls/charamake/rgsp/{(int)manip.Key.SubRace - 1}-{(int)manip.Key.Attribute.ToGender() - 1}.rgsp";
}
