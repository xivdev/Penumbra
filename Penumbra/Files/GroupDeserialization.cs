using System.Text.Json;
using Dalamud.Interface.ImGuiNotification;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.Files;

public static class GroupDeserialization
{
    public static IModGroup? ReadGroup(ref Utf8JsonReader reader, string filePath, Mod mod)
    {
        if (reader.TokenType is JsonTokenType.Null)
            return null;

        if (reader.TokenType is not JsonTokenType.StartObject)
            throw new InvalidMetaException(mod, filePath, $"Object start for group expected, but got {reader.TokenType}.");

        var groupReader = reader.CreateObjectLimit();
        if (reader.TryPeekEnumProperty("Type"u8, out GroupType type) is not JsonFunctions.PeekError.Success)
            type = GroupType.Single;

        return type switch
        {
            GroupType.Single    => ReadSingle(groupReader, ref reader, mod),
            GroupType.Multi     => ReadMulti(groupReader, ref reader, mod),
            GroupType.Imc       => ReadImc(groupReader, ref reader, mod),
            GroupType.Combining => ReadCombining(groupReader, ref reader, mod),
            _                   => throw new JsonException($"{type} is not a valid group type for mod {mod.Name}."),
        };
    }

    public static void ReadDefaultContainer(ref Utf8JsonReader reader, string filePath, Mod mod)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            mod.Default.Files.Clear();
            mod.Default.FileSwaps.Clear();
            mod.Default.Manipulations.Clear();
            return;
        }

        if (reader.TokenType is not JsonTokenType.StartObject)
            throw new InvalidMetaException(mod, filePath,
                $"Object start for default container expected, but got {reader.TokenType}.");

        var containerReader = reader.CreateObjectLimit();
        var visited         = 0;
        while (containerReader.Read(ref reader))
        {
            if (reader.TokenType is not JsonTokenType.PropertyName)
                throw new InvalidMetaException(mod, filePath, "Property name expected.");

            if (!LoadDataContainer(ref reader, mod.Default, mod.ModPath, ref visited))
                reader.Skip();
        }

        if ((visited & 1) is not 1)
            mod.Default.Files.Clear();
        if ((visited & 2) is not 2)
            mod.Default.FileSwaps.Clear();
        if ((visited & 4) is not 4)
            mod.Default.Manipulations.Clear();
    }

    public static IModGroup? ReadGroupFile(SaveService saveService, Mod mod, FileInfo file)
        => IJsonParsable.ReadJson<GroupIntermediate>(saveService, file.FullName, true, mod).Group;

    public static void ReadDefaultContainerFile(SaveService saveService, Mod mod)
    {
        var file = saveService.FileNames.OptionGroupFile(mod, -1, false);
        if (!File.Exists(file))
        {
            mod.Default.Files.Clear();
            mod.Default.FileSwaps.Clear();
            mod.Default.Manipulations.Clear();
            return;
        }

        IJsonParsable.ReadJson<DefaultOptionIntermediate>(saveService, file, true, mod);
    }

    public static MultiModGroup ReadMulti(Utf8JsonObjectLimit groupReader, ref Utf8JsonReader j, Mod mod)
    {
        var ret = new MultiModGroup(mod);
        while (groupReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
                throw new JsonException("Property name expected.");

            if (j.ArrayProperty("Options"u8, out var array, true))
                LoadMultiOptions(array, ref j, ret);
            else if (!ReadJsonBase(ref j, ret))
                j.Skip();
        }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);

        return ret;
    }

    public static SingleModGroup ReadSingle(Utf8JsonObjectLimit groupReader, ref Utf8JsonReader j, Mod mod)
    {
        var ret = new SingleModGroup(mod);
        while (groupReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
                throw new JsonException("Property name expected.");

            if (j.ArrayProperty("Options"u8, out var array, true))
                LoadSingleOptions(array, ref j, ret);
            else if (!ReadJsonBase(ref j, ret))
                j.Skip();
        }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);

        return ret;
    }

    public static ImcModGroup? ReadImc(Utf8JsonObjectLimit groupReader, ref Utf8JsonReader j, Mod mod)
    {
        var ret = new ImcModGroup(mod);
        while (groupReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
                throw new JsonException("Property name expected.");

            if (j.ArrayProperty("Options"u8, out var array, true))
                LoadImcOptions(array, ref j, ret);
            else if (j.ObjectProperty("Identifier"u8, out var identifier))
                ret.Identifier = MetaDeserialization.ReadImc(identifier, ref j, out _) ?? default;
            else if (j.ObjectProperty("DefaultEntry"u8, out var entry))
                ret.DefaultEntry = MetaDeserialization.ReadImcEntry(entry, ref j) ?? new ImcEntry();
            else if (j.BoolProperty("AllVariants"u8, out var allVariants))
                ret.AllVariants = allVariants;
            else if (j.BoolProperty("OnlyAttributes"u8, out var onlyAttributes))
                ret.OnlyAttributes = onlyAttributes;
            else if (!ReadJsonBase(ref j, ret))
                j.Skip();
        }

        if (ret.Identifier.ObjectType is GameData.Enums.ObjectType.Unknown || ret.DefaultEntry.MaterialId is 0)
        {
            Penumbra.Messager.NotificationMessage($"Could not add IMC group {ret.Name} because the associated IMC Entry is invalid.",
                NotificationType.Warning);
            return null;
        }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);
        return ret;
    }

    public static CombiningModGroup ReadCombining(Utf8JsonObjectLimit groupReader, ref Utf8JsonReader j, Mod mod)
    {
        var ret = CombiningModGroup.EmptyData(mod);
        while (groupReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
                throw new JsonException("Property name expected.");

            if (j.ArrayProperty("Options"u8, out var array, true))
                LoadCombiningOptions(array, ref j, ret);
            else if (j.ArrayProperty("Containers"u8, out var containers, true))
                LoadCombiningContainers(containers, ref j, ret);
            else if (!ReadJsonBase(ref j, ret))
                j.Skip();
        }

        var requiredContainers = 1 << ret.OptionData.Count;
        if (requiredContainers > ret.Data.Count)
        {
            Penumbra.Messager.NotificationMessage(
                $"Combining Group {ret.Name} in {mod.Name} has not enough data containers for its {ret.OptionData.Count} options, filling with empty containers.",
                NotificationType.Warning);
            ret.Data.EnsureCapacity(requiredContainers);
            ret.Data.AddRange(Enumerable.Repeat(0, requiredContainers - ret.Data.Count).Select(_ => new CombinedDataContainer(ret)));
        }
        else if (requiredContainers < ret.Data.Count)
        {
            Penumbra.Messager.NotificationMessage(
                $"Combining Group {ret.Name} in {mod.Name} has more than {IModGroup.MaxCombiningOptions} options, ignoring excessive options.",
                NotificationType.Warning);
            ret.Data.RemoveRange(requiredContainers, ret.Data.Count - requiredContainers);
        }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);
        return ret;
    }


    private static void LoadSingleOptions(Utf8JsonObjectLimit optionArray, ref Utf8JsonReader j, SingleModGroup ret)
    {
        while (optionArray.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.StartObject)
                throw new JsonException("New Object expected.");

            var option       = new SingleSubMod(ret);
            var optionReader = j.CreateObjectLimit();
            var visited      = 0;
            while (optionReader.Read(ref j))
            {
                if (j.TokenType is not JsonTokenType.PropertyName)
                    throw new JsonException("Property name expected.");

                if (LoadOptionData(ref j, option))
                    continue;

                if (LoadDataContainer(ref j, option, ret.Mod.ModPath, ref visited))
                    continue;

                j.Skip();
            }

            ret.OptionData.Add(option);
        }
    }

    private static void LoadMultiOptions(Utf8JsonObjectLimit optionArray, ref Utf8JsonReader j, MultiModGroup ret)
    {
        var warned  = false;
        var visited = 0;
        while (optionArray.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.StartObject)
                throw new JsonException("New Object expected.");

            if (ret.OptionData.Count is IModGroup.MaxMultiOptions)
            {
                if (!warned)
                    continue;

                Penumbra.Messager.NotificationMessage(
                    $"Multi Group {ret.Name} in {ret.Mod.Name} has more than {IModGroup.MaxMultiOptions} options, ignoring excessive options.",
                    NotificationType.Warning);
                warned = true;
            }
            else
            {
                var option       = new MultiSubMod(ret);
                var optionReader = j.CreateObjectLimit();
                while (optionReader.Read(ref j))
                {
                    if (j.TokenType is not JsonTokenType.PropertyName)
                        throw new JsonException("Property name expected.");

                    if (j.NumberProperty("Priority"u8, out int priority))
                    {
                        option.Priority = new ModPriority(priority);
                        continue;
                    }

                    if (LoadOptionData(ref j, option))
                        continue;

                    if (LoadDataContainer(ref j, option, ret.Mod.ModPath, ref visited))
                        continue;

                    j.Skip();
                }

                ret.OptionData.Add(option);
            }
        }
    }

    private static void LoadImcOptions(Utf8JsonObjectLimit optionArray, ref Utf8JsonReader j, ImcModGroup ret)
    {
        var rollingMask = 0ul;
        while (optionArray.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.StartObject)
                throw new JsonException("New Object expected.");

            var option       = new ImcSubMod(ret);
            var optionReader = j.CreateObjectLimit();
            while (optionReader.Read(ref j))
            {
                if (j.TokenType is not JsonTokenType.PropertyName)
                    throw new JsonException("Property name expected.");

                if (j.NumberProperty("AttributeMask"u8, out ushort mask))
                {
                    option.AttributeMask = (ushort)(mask & ImcEntry.AttributesMask);
                    continue;
                }

                if (j.BoolProperty("IsDisableSubMod"u8, out var isDisable))
                {
                    option.IsDisableSubMod = isDisable;
                    continue;
                }

                if (LoadOptionData(ref j, option))
                    continue;

                j.Skip();
            }

            if (option.IsDisableSubMod && ret.OptionData.FirstOrDefault(m => m.IsDisableSubMod) is { } disable)
            {
                Penumbra.Messager.NotificationMessage(
                    $"Could not add IMC option {option.Name} to {ret.Name} because it already contains {disable.Name} as disable option.",
                    NotificationType.Warning);
            }
            else if ((option.AttributeMask & rollingMask) is not 0)
            {
                Penumbra.Messager.NotificationMessage(
                    $"Could not add IMC option {option.Name} to {ret.Name} because it contains attributes already in use.",
                    NotificationType.Warning);
            }
            else
            {
                rollingMask |= option.AttributeMask;
                ret.OptionData.Add(option);
                if (option.IsDisableSubMod)
                    ret.CanBeDisabled = true;
            }
        }
    }

    private static void LoadCombiningOptions(Utf8JsonObjectLimit optionArray, ref Utf8JsonReader j, CombiningModGroup ret)
    {
        var warned = false;
        while (optionArray.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.StartObject)
                throw new JsonException("New Object expected.");

            if (ret.OptionData.Count is IModGroup.MaxCombiningOptions)
            {
                if (warned)
                    continue;

                Penumbra.Messager.NotificationMessage(
                    $"Combining Group {ret.Name} in {ret.Mod.Name} has more than {IModGroup.MaxCombiningOptions} options, ignoring excessive options.",
                    NotificationType.Warning);
                warned = true;
            }
            else
            {
                var option       = new CombiningSubMod(ret);
                var optionReader = j.CreateObjectLimit();
                while (optionReader.Read(ref j))
                {
                    if (j.TokenType is not JsonTokenType.PropertyName)
                        throw new JsonException("Property name expected.");

                    if (LoadOptionData(ref j, option))
                        continue;

                    j.Skip();
                }

                ret.OptionData.Add(option);
            }
        }
    }

    private static void LoadCombiningContainers(Utf8JsonObjectLimit containerArray, ref Utf8JsonReader j, CombiningModGroup ret)
    {
        var visited = 0;
        while (containerArray.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.StartObject)
                throw new JsonException("New Object expected.");

            var container       = new CombinedDataContainer(ret);
            var containerReader = j.CreateObjectLimit();
            while (containerReader.Read(ref j))
            {
                if (j.TokenType is not JsonTokenType.PropertyName)
                    throw new JsonException("Property name expected.");

                if (j.StringProperty("Name"u8, out string? name))
                {
                    container.Name = name ?? string.Empty;
                    continue;
                }

                if (LoadDataContainer(ref j, container, ret.Mod.ModPath, ref visited))
                    continue;

                j.Skip();
            }

            ret.Data.Add(container);
        }
    }

    /// <summary> Load the relevant data for a selectable option from a JToken of that option. </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static bool LoadOptionData(ref Utf8JsonReader j, IModOption option)
    {
        if (j.StringProperty("Name"u8, out string? name, true))
        {
            option.Name = name ?? string.Empty;
            return true;
        }

        if (j.StringProperty("Description"u8, out string? description, true))
        {
            option.Description = description ?? string.Empty;
            return true;
        }

        if (j.BoolProperty("Separator"u8, out var separator))
        {
            option.Separator = separator;
            return true;
        }

        if (j.NumberProperty("Color"u8, out int number))
        {
            option.Color = number switch
            {
                1 => ColorId.OptionColor1,
                2 => ColorId.OptionColor2,
                3 => ColorId.OptionColor3,
                4 => ColorId.OptionColor4,
                5 => ColorId.OptionColor5,
                6 => ColorId.OptionColor6,
                7 => ColorId.OptionColor7,
                8 => ColorId.OptionColor8,
                _ => default,
            };
            return true;
        }

        return false;
    }

    /// <summary> Load all file redirections, file swaps and meta manipulations from a JToken of that option into a data container. </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static unsafe bool LoadDataContainer(ref Utf8JsonReader j, IModDataContainer data, DirectoryInfo basePath, ref int visited)
    {
        Span<byte> buffer = stackalloc byte[Utf8GamePath.MaxGamePathLength + 1];
        if (j.ObjectProperty("Files"u8, out var files, true))
        {
            data.Files.Clear();
            while (files.Read(ref j))
            {
                if (j.TokenType is not JsonTokenType.PropertyName)
                    throw new JsonException($"Expected property in file dictionary, got {j.TokenType}.");

                var length = j.CopyString(buffer);
                buffer[length] = 0;
                if (length is 0 || !Utf8GamePath.FromSpan(buffer[..length], MetaDataComputation.All, out var gamePathTmp))
                    continue;

                if (!j.Read() || !j.TryReadString(out var rel) || !Utf8RelPath.FromString(rel, out var relPath))
                    throw new JsonException($"Expected relative path in file dictionary for key {gamePathTmp}.");

                data.Files.TryAdd(gamePathTmp.Clone(), new FullPath(basePath, relPath));
            }

            visited |= 1;
            return true;
        }

        if (j.ObjectProperty("FileSwaps"u8, out var swaps, true))
        {
            data.FileSwaps.Clear();
            while (swaps.Read(ref j))
            {
                if (j.TokenType is not JsonTokenType.PropertyName)
                    throw new JsonException($"Expected property in file dictionary, got {j.TokenType}.");

                var length = j.CopyString(buffer);
                buffer[length] = 0;
                if (length is 0 || !Utf8GamePath.FromSpan(buffer[..length], MetaDataComputation.All, out var gamePathTmp))
                    continue;

                if (!j.Read() || !j.TryReadString(out var swap))
                    throw new JsonException($"Expected game path in file swap dictionary for key {gamePathTmp}.");

                data.FileSwaps.TryAdd(gamePathTmp.Clone(), new FullPath(swap!));
            }

            visited |= 2;
            return true;
        }

        if (j.ArrayProperty("Manipulations"u8, out _, true))
        {
            data.Manipulations =  MetaDeserialization.ReadMetaDictionary(ref j, data);
            visited            |= 4;
            return true;
        }

        return false;
    }

    private static bool ReadJsonBase(ref Utf8JsonReader j, IModGroup group)
    {
        if (j.StringProperty("Name"u8, out string? name, true))
        {
            group.Name = name ?? string.Empty;
            return true;
        }

        if (j.StringProperty("Description"u8, out string? description, true))
        {
            group.Description = description ?? string.Empty;
            return true;
        }

        if (j.StringProperty("Image"u8, out string? image, true))
        {
            group.Image = image ?? string.Empty;
            return true;
        }

        if (j.NumberProperty("Page"u8, out int page))
        {
            group.Page = page;
            return true;
        }

        if (j.NumberProperty("Priority"u8, out int priority))
        {
            group.Priority = new ModPriority(priority);
            return true;
        }

        if (j.NumberProperty("DefaultSettings"u8, out ulong settings))
        {
            group.DefaultSettings = new Setting(settings);
            return true;
        }

        return false;
    }

    private struct GroupIntermediate : IJsonParsable<GroupIntermediate>
    {
        public IModGroup? Group;

        public static GroupIntermediate Read(ref Utf8JsonReader j, string filePath, object? parent)
        {
            if (parent is not Mod mod)
                throw new ArgumentException("The user input for reading a mod group has to be the mod object.");

            if (!j.Read())
                return default;

            return new GroupIntermediate { Group = ReadGroup(ref j, filePath, mod) };
        }
    }

    private struct DefaultOptionIntermediate : IJsonParsable<DefaultOptionIntermediate>
    {
        public static DefaultOptionIntermediate Read(ref Utf8JsonReader j, string filePath, object? parent)
        {
            if (parent is not Mod mod)
                throw new ArgumentException("The user input for reading a default option has to be the mod object.");

            if (!j.Read())
                throw new InvalidMetaException(mod, filePath, "Empty or malformed JSON encountered.");

            ReadDefaultContainer(ref j, filePath, mod);
            return default;
        }
    }
}
