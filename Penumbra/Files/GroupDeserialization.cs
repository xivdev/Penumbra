using System.Text.Json;
using Dalamud.Interface.ImGuiNotification;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Files;

public static class GroupDeserialization
{
    public static IModGroup? ReadGroup(ModDeserialization.Context context, ref Utf8JsonReader reader, string filePath)
    {
        if (reader.TokenType is JsonTokenType.Null)
            return null;

        if (reader.TokenType is not JsonTokenType.StartObject)
            throw new InvalidMetaException(context.Mod, filePath, $"Object start for group expected, but got {reader.TokenType}.");

        var groupReader = reader.CreateObjectLimit();
        if (reader.TryPeekEnumProperty("Type"u8, out GroupType type) is not JsonFunctions.PeekError.Success)
            type = GroupType.Single;

        return type switch
        {
            GroupType.Single    => ReadSingle(context, groupReader, ref reader),
            GroupType.Multi     => ReadMulti(context, groupReader, ref reader),
            GroupType.Imc       => ReadImc(context, groupReader, ref reader),
            GroupType.Combining => ReadCombining(context, groupReader, ref reader),
            _                   => throw new JsonException($"{type} is not a valid group type for mod {context.Mod.Name}."),
        };
    }

    public static void ReadDefaultContainer(ModDeserialization.Context context, ref Utf8JsonReader reader, string filePath)
    {
        if (reader.TokenType is JsonTokenType.Null)
        {
            context.Mod.Default.Files.Clear();
            context.Mod.Default.FileSwaps.Clear();
            context.Mod.Default.Manipulations.Clear();
            return;
        }

        if (reader.TokenType is not JsonTokenType.StartObject)
            throw new InvalidMetaException(context.Mod, filePath,
                $"Object start for default container expected, but got {reader.TokenType}.");

        var containerReader = reader.CreateObjectLimit();
        var visited         = 0;
        while (containerReader.Read(ref reader))
        {
            if (reader.TokenType is not JsonTokenType.PropertyName)
                throw new InvalidMetaException(context.Mod, filePath, "Property name expected.");

            if (!LoadDataContainer(ref reader, context.Mod.Default, context.Mod.ModPath, ref visited))
                reader.Skip();
        }

        if ((visited & 1) is not 1)
            context.Mod.Default.Files.Clear();
        if ((visited & 2) is not 2)
            context.Mod.Default.FileSwaps.Clear();
        if ((visited & 4) is not 4)
            context.Mod.Default.Manipulations.Clear();
    }

    public static IModGroup? ReadGroupFile(SaveService saveService, ModDeserialization.Context context, FileInfo file)
        => IJsonParsable.ReadJson<GroupIntermediate>(saveService, file.FullName, true, context).Group;

    public static void ReadDefaultContainerFile(SaveService saveService, ModDeserialization.Context context)
    {
        var file = saveService.FileNames.OptionGroupFile(context.Mod, -1, false);
        if (!File.Exists(file))
        {
            context.Mod.Default.Files.Clear();
            context.Mod.Default.FileSwaps.Clear();
            context.Mod.Default.Manipulations.Clear();
            return;
        }

        IJsonParsable.ReadJson<DefaultOptionIntermediate>(saveService, file, true, context);
    }

    public static MultiModGroup ReadMulti(ModDeserialization.Context context, Utf8JsonObjectLimit groupReader, ref Utf8JsonReader j)
    {
        var ret = new MultiModGroup(context.Mod);
        while (groupReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
                throw new JsonException("Property name expected.");

            if (j.ArrayProperty("Options"u8, out var array, true))
                LoadMultiOptions(context, array, ref j, ret);
            else if (!ReadJsonBase(context, ref j, ret))
                j.Skip();
        }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);

        return ret;
    }

    public static SingleModGroup ReadSingle(ModDeserialization.Context context, Utf8JsonObjectLimit groupReader, ref Utf8JsonReader j)
    {
        var ret = new SingleModGroup(context.Mod);
        while (groupReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
                throw new JsonException("Property name expected.");

            if (j.ArrayProperty("Options"u8, out var array, true))
                LoadSingleOptions(context, array, ref j, ret);
            else if (!ReadJsonBase(context, ref j, ret))
                j.Skip();
        }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);

        return ret;
    }

    public static ImcModGroup? ReadImc(ModDeserialization.Context context, Utf8JsonObjectLimit groupReader, ref Utf8JsonReader j)
    {
        var ret = new ImcModGroup(context.Mod);
        while (groupReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
                throw new JsonException("Property name expected.");

            if (j.ArrayProperty("Options"u8, out var array, true))
                LoadImcOptions(context, array, ref j, ret);
            else if (j.ObjectProperty("Identifier"u8, out var identifier))
                ret.Identifier = MetaDeserialization.ReadImc(identifier, ref j, out _) ?? default;
            else if (j.ObjectProperty("DefaultEntry"u8, out var entry))
                ret.DefaultEntry = MetaDeserialization.ReadImcEntry(entry, ref j) ?? new ImcEntry();
            else if (j.BoolProperty("AllVariants"u8, out var allVariants))
                ret.AllVariants = allVariants;
            else if (j.BoolProperty("OnlyAttributes"u8, out var onlyAttributes))
                ret.OnlyAttributes = onlyAttributes;
            else if (!ReadJsonBase(context, ref j, ret))
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

    public static CombiningModGroup ReadCombining(ModDeserialization.Context context, Utf8JsonObjectLimit groupReader, ref Utf8JsonReader j)
    {
        var ret = CombiningModGroup.EmptyData(context.Mod);
        while (groupReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
                throw new JsonException("Property name expected.");

            if (j.ArrayProperty("Options"u8, out var array, true))
                LoadCombiningOptions(context, array, ref j, ret);
            else if (j.ArrayProperty("Containers"u8, out var containers, true))
                LoadCombiningContainers(containers, ref j, ret);
            else if (!ReadJsonBase(context, ref j, ret))
                j.Skip();
        }

        var requiredContainers = 1 << ret.OptionData.Count;
        if (requiredContainers > ret.Data.Count)
        {
            Penumbra.Messager.NotificationMessage(
                $"Combining Group {ret.Name} in {context.Mod.Name} has not enough data containers for its {ret.OptionData.Count} options, filling with empty containers.",
                NotificationType.Warning);
            ret.Data.EnsureCapacity(requiredContainers);
            ret.Data.AddRange(Enumerable.Repeat(0, requiredContainers - ret.Data.Count).Select(_ => new CombinedDataContainer(ret)));
        }
        else if (requiredContainers < ret.Data.Count)
        {
            Penumbra.Messager.NotificationMessage(
                $"Combining Group {ret.Name} in {context.Mod.Name} has more than {IModGroup.MaxCombiningOptions} options, ignoring excessive options.",
                NotificationType.Warning);
            ret.Data.RemoveRange(requiredContainers, ret.Data.Count - requiredContainers);
        }

        ret.DefaultSettings = ret.FixSetting(ret.DefaultSettings);
        return ret;
    }


    private static void LoadSingleOptions(ModDeserialization.Context context, Utf8JsonObjectLimit optionArray, ref Utf8JsonReader j,
        SingleModGroup ret)
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

                if (LoadOptionData(context, ref j, option))
                    continue;

                if (LoadDataContainer(ref j, option, ret.Mod.ModPath, ref visited))
                    continue;

                j.Skip();
            }

            ret.OptionData.Add(option);
        }
    }

    private static void LoadMultiOptions(ModDeserialization.Context context, Utf8JsonObjectLimit optionArray, ref Utf8JsonReader j,
        MultiModGroup ret)
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

                    if (LoadOptionData(context, ref j, option))
                        continue;

                    if (LoadDataContainer(ref j, option, ret.Mod.ModPath, ref visited))
                        continue;

                    j.Skip();
                }

                ret.OptionData.Add(option);
            }
        }
    }

    private static void LoadImcOptions(ModDeserialization.Context context, Utf8JsonObjectLimit optionArray, ref Utf8JsonReader j,
        ImcModGroup ret)
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

                if (LoadOptionData(context, ref j, option))
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

    private static void LoadCombiningOptions(ModDeserialization.Context context, Utf8JsonObjectLimit optionArray, ref Utf8JsonReader j,
        CombiningModGroup ret)
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

                    if (LoadOptionData(context, ref j, option))
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

                if (j.StringProperty("Name"u8, out string? name, true))
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
    private static bool LoadOptionData(ModDeserialization.Context context, ref Utf8JsonReader j, IModOption option)
    {
        if (ReadJsonObject(context, ref j, option))
            return true;

        if (j.NumberProperty("Color"u8, out int number))
        {
            option.Color = IModOption.ConvertColor(number);
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

    private static bool ReadJsonObject(ModDeserialization.Context context, ref Utf8JsonReader j, IModObject @object)
    {
        if (j.StringProperty("Name"u8, out string? name, true))
        {
            @object.Name = name ?? string.Empty;
            return true;
        }

        if (j.StringProperty("Description"u8, out string? description, true))
        {
            @object.Description = description ?? string.Empty;
            return true;
        }

        if (j.GuidProperty("Id"u8, out var id))
        {
            @object.Id = id;
            return true;
        }

        if (j.ArrayProperty("Layout"u8, out _, true))
        {
            @object.Layout = j.ReadFlagEnumArray<ModSettingsLayout>()?.Reduce(@object) ?? ModSettingsLayout.None;
            return true;
        }

        if (j.ObjectProperty("Condition"u8, out _, true))
        {
            if (j.TokenType is JsonTokenType.Null)
            {
                @object.Condition = null;
                return true;
            }

            if (!ConditionParser.TryParse<ModSettingContext>(ref j, out var condition))
                throw new JsonException("Could not parse condition.");

            if (condition?.Reduce() is { } c and not TrueCondition<ModSettingContext>)
                context.Conditions[@object] = c;
            else
                @object.Condition = null;

            return true;
        }

        return false;
    }

    private static bool ReadJsonBase(ModDeserialization.Context context, ref Utf8JsonReader j, IModGroup group)
    {
        if (ReadJsonObject(context, ref j, group))
            return true;

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

        if (j.GuidProperty("ParentSettings"u8, out var parent))
        {
            context.Parents[group] = parent;
            return true;
        }

        return false;
    }

    private struct GroupIntermediate : IJsonParsable<GroupIntermediate>
    {
        public IModGroup? Group;

        public static GroupIntermediate Read(ref Utf8JsonReader j, string filePath, object? parent)
        {
            if (parent is not ModDeserialization.Context context)
                throw new ArgumentException("The user input for reading a mod group has to be the deserialization context.");

            if (!j.Read())
                throw new InvalidMetaException(context.Mod, filePath, "Empty or malformed JSON encountered.");

            return new GroupIntermediate { Group = ReadGroup(context, ref j, filePath) };
        }
    }

    private struct DefaultOptionIntermediate : IJsonParsable<DefaultOptionIntermediate>
    {
        public static DefaultOptionIntermediate Read(ref Utf8JsonReader j, string filePath, object? parent)
        {
            if (parent is not ModDeserialization.Context context)
                throw new ArgumentException("The user input for reading a default option has to be the deserialization context.");

            if (!j.Read())
                throw new InvalidMetaException(context.Mod, filePath, "Empty or malformed JSON encountered.");

            ReadDefaultContainer(context, ref j, filePath);
            return default;
        }
    }
}
