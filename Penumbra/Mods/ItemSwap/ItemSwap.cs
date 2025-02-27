using Penumbra.Api.Enums;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods.ItemSwap;

public static class ItemSwap
{
    public class InvalidItemTypeException : Exception
    { }

    public class MissingFileException(ResourceType type, object path) : Exception($"Could not load {type} File Data for \"{path}\".")
    {
        public readonly ResourceType Type = type;
    }

    private static bool LoadFile(MetaFileManager manager, FullPath path, out byte[] data)
    {
        if (path.FullName.Length > 0)
            try
            {
                if (path.IsRooted)
                {
                    data = File.ReadAllBytes(path.FullName);
                    return true;
                }

                var file = manager.GameData.GetFile(path.InternalName.ToString());
                if (file != null)
                {
                    data = file.Data;
                    return true;
                }
            }
            catch (Exception e)
            {
                Penumbra.Log.Debug($"Could not load file {path}:\n{e}");
            }

        data = [];
        return false;
    }

    public class GenericFile : IWritable
    {
        public readonly byte[] Data;
        public          bool   Valid { get; }

        public GenericFile(MetaFileManager manager, FullPath path)
            => Valid = LoadFile(manager, path, out Data);

        public byte[] Write()
            => Data;

        public static readonly GenericFile Invalid = new(null!, FullPath.Empty);
    }

    public static bool LoadFile(MetaFileManager manager, FullPath path, [NotNullWhen(true)] out GenericFile? file)
    {
        file = new GenericFile(manager, path);
        if (file.Valid)
            return true;

        file = null;
        return false;
    }

    public static bool LoadMdl(MetaFileManager manager, FullPath path, [NotNullWhen(true)] out MdlFile? file)
    {
        try
        {
            if (LoadFile(manager, path, out byte[] data))
            {
                file = new MdlFile(data);
                return true;
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Debug($"Could not parse file {path} to Mdl:\n{e}");
        }

        file = null;
        return false;
    }

    public static bool LoadMtrl(MetaFileManager manager, FullPath path, [NotNullWhen(true)] out MtrlFile? file)
    {
        try
        {
            if (LoadFile(manager, path, out byte[] data))
            {
                file = new MtrlFile(data);
                return true;
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Debug($"Could not parse file {path} to Mtrl:\n{e}");
        }

        file = null;
        return false;
    }

    public static bool LoadAvfx(MetaFileManager manager, FullPath path, [NotNullWhen(true)] out AvfxFile? file)
    {
        try
        {
            if (LoadFile(manager, path, out byte[] data))
            {
                file = new AvfxFile(data);
                return true;
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Debug($"Could not parse file {path} to Avfx:\n{e}");
        }

        file = null;
        return false;
    }


    public static FileSwap CreatePhyb(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, EstType type,
        GenderRace race, EstEntry estEntry)
    {
        var phybPath = GamePaths.Phyb.Customization(race, type.ToName(), estEntry.AsId);
        return FileSwap.CreateSwap(manager, ResourceType.Phyb, redirections, phybPath, phybPath);
    }

    public static FileSwap CreateSklb(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, EstType type,
        GenderRace race, EstEntry estEntry)
    {
        var sklbPath = GamePaths.Sklb.Customization(race, type.ToName(), estEntry.AsId);
        return FileSwap.CreateSwap(manager, ResourceType.Sklb, redirections, sklbPath, sklbPath);
    }

    public static MetaSwap<EstIdentifier, EstEntry>? CreateEst(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections,
        MetaDictionary manips, EstType type, GenderRace genderRace, PrimaryId idFrom, PrimaryId idTo, bool ownMdl)
    {
        if (type == 0)
            return null;

        var manipFromIdentifier = new EstIdentifier(idFrom, type, genderRace);
        var manipToIdentifier   = new EstIdentifier(idTo,   type, genderRace);
        var manipFromDefault    = EstFile.GetDefault(manager, manipFromIdentifier);
        var manipToDefault      = EstFile.GetDefault(manager, manipToIdentifier);
        var est = new MetaSwap<EstIdentifier, EstEntry>(i => manips.TryGetValue(i, out var e) ? e : null, manipFromIdentifier, manipFromDefault,
            manipToIdentifier, manipToDefault);

        if (ownMdl && est.SwapToModdedEntry.Value >= 2)
        {
            var phyb = CreatePhyb(manager, redirections, type, genderRace, est.SwapToModdedEntry);
            est.ChildSwaps.Add(phyb);
            var sklb = CreateSklb(manager, redirections, type, genderRace, est.SwapToModdedEntry);
            est.ChildSwaps.Add(sklb);
        }
        else if (est.SwapAppliedIsDefault)
        {
            return null;
        }

        return est;
    }

    public static int GetStableHashCode(this string str)
    {
        unchecked
        {
            var hash1 = 5381;
            var hash2 = hash1;

            for (var i = 0; i < str.Length && str[i] != '\0'; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1 || str[i + 1] == '\0')
                    break;

                hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
            }

            return hash1 + hash2 * 1566083941;
        }
    }

    public static string ReplaceAnyId(string path, char idType, PrimaryId id, bool condition = true)
        => condition
            ? Regex.Replace(path, $"{idType}\\d{{4}}", $"{idType}{id.Id:D4}")
            : path;

    public static string ReplaceAnyRace(string path, GenderRace to, bool condition = true)
        => ReplaceAnyId(path, 'c', (ushort)to, condition);

    public static string ReplaceAnyBody(string path, BodySlot slot, PrimaryId to, bool condition = true)
        => ReplaceAnyId(path, slot.ToAbbreviation(), to, condition);

    public static string ReplaceId(string path, char type, PrimaryId idFrom, PrimaryId idTo, bool condition = true)
        => condition
            ? path.Replace($"{type}{idFrom.Id:D4}", $"{type}{idTo.Id:D4}")
            : path;

    public static string ReplaceSlot(string path, EquipSlot from, EquipSlot to, bool condition = true)
        => condition
            ? path.Replace($"_{from.ToSuffix()}_", $"_{to.ToSuffix()}_")
            : path;

    public static string ReplaceType(string path, EquipSlot from, EquipSlot to, PrimaryId idFrom)
    {
        var isAccessoryFrom = from.IsAccessory();
        if (isAccessoryFrom == to.IsAccessory())
            return path;

        if (isAccessoryFrom)
        {
            path = path.Replace("accessory/a", "equipment/e");
            return path.Replace($"a{idFrom.Id:D4}", $"e{idFrom.Id:D4}");
        }

        path = path.Replace("equipment/e", "accessory/a");
        return path.Replace($"e{idFrom.Id:D4}", $"a{idFrom.Id:D4}");
    }

    public static string ReplaceRace(string path, GenderRace from, GenderRace to, bool condition = true)
        => ReplaceId(path, 'c', (ushort)from, (ushort)to, condition);

    public static string ReplaceBody(string path, BodySlot slot, PrimaryId idFrom, PrimaryId idTo, bool condition = true)
        => ReplaceId(path, slot.ToAbbreviation(), idFrom, idTo, condition);

    public static string AddSuffix(string path, string ext, string suffix, bool condition = true)
        => condition
            ? path.Replace(ext, suffix + ext)
            : path;
}
