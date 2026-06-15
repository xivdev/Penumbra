using System.Text.Json;
using Luna;
using Penumbra.GameData.Structs;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.SubMods;

namespace Penumbra.Files;

public static class ModDeserialization
{
    public sealed class Context(Mod mod) : IDisposable
    {
        public readonly Mod                                                   Mod        = mod;
        public readonly Dictionary<IModGroup, Guid>                           Parents    = [];
        public readonly Dictionary<IModObject, ICondition<ModSettingContext>> Conditions = [];

        public void Dispose()
        {
            foreach (var (group, guid) in Parents)
            {
                if (!Mod.SubObjects.TryGetValue(guid, out var parentObject))
                    throw new InvalidMetaException(Mod, string.Empty, $"The specified parent object {guid} for {group.Name} does not exist.");

                if (CycleChecker.Check(group, parentObject))
                    throw new InvalidMetaException(Mod, string.Empty,
                        $"The specified parent object {parentObject.Name} for {group.Name} would cause a cycle.");

                group.ParentSetting = parentObject;
            }

            foreach (var (@object, condition) in Conditions)
                ResolveConditions(@object, condition);

            if (CheckCyclicConditions(Mod) is { } error)
                throw new InvalidMetaException(Mod, string.Empty, error);
        }

        private static string? ValidateCondition(IModObject parent, Guid condition, out IModOption? option)
        {
            if (!parent.Mod.SubObjects.TryGetValue(condition, out var @object) || @object is not IModOption o)
            {
                option = null;
                return $"The specified option {condition} for {parent.Name}'s conditions does not exist or is not an option.";
            }

            option = o;
            return LayoutManager.ValidateCondition(parent, option);
        }

        private static string? CheckCyclicConditions(Mod _)
        {
            // TODO
            return null;
        }

        private static ICondition<ModSettingContext> Convert(IModObject parent, SettingIdCondition condition)
        {
            if (ValidateCondition(parent, condition.Option, out var option) is { } error)
                throw new InvalidMetaException(parent.Mod, string.Empty, error);
            return new SettingCondition(option!);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ResolveConditions(IModObject @object, ICondition<ModSettingContext> condition)
        {
            var reduced = condition.EditConditions(m => m is SettingIdCondition id ? Convert(@object, id) : null)?.Reduce();
            if (reduced is not null and not TrueCondition<ModSettingContext>)
                @object.Condition = reduced;
        }
    }


    public static ModDataChangeType ReloadMod(SaveService files, Mod mod)
    {
        mod.ModPath.Refresh();
        if (!mod.ModPath.Exists)
            throw new MetaMissingException(mod, mod.ModPath.FullName);

        var metaPath = files.FileNames.ModMetaPath(mod);
        if (!File.Exists(metaPath))
            throw new MetaMissingException(mod, metaPath);

        using var context = new Context(mod);
        var       meta    = IJsonParsable.ReadJson<ModMetaData>(files, metaPath, false, context).Changes;
        MigrateGroups(files, context);

        return meta;
    }

    private static void MigrateGroups(SaveService files, Context context)
    {
        if (context.Mod.LoadedVersion >= 4)
            return;

        GroupDeserialization.ReadDefaultContainerFile(files, context);
        foreach (var groupFile in files.FileNames.GetOptionGroupFiles(context.Mod))
        {
            if (GroupDeserialization.ReadGroupFile(files, context, groupFile) is { } group)
                context.Mod.AddGroup(group, groupFile.FullName);
        }
    }

    public ref struct ModMetaData : IJsonParsable<ModMetaData>
    {
        public ModDataChangeType Changes;

        public static ModMetaData Read(scoped ref Utf8JsonReader reader, string filePath, object? userInput)
        {
            if (userInput is not Context context)
                throw new ArgumentException("The user input for reading a Mod has to be the mod object.");

            if (!reader.Read() || reader.TokenType is not JsonTokenType.StartObject)
                throw new InvalidMetaException(context.Mod, filePath, "File is empty or malformed.");

            ModDataChangeType visited            = 0;
            ModDataChangeType ret                = 0;
            var               encounteredDefault = false;
            var               objectReader       = reader.CreateObjectLimit();

            context.Mod.Groups.Clear();
            context.Mod.SubObjects.Clear();
            while (objectReader.Read(ref reader))
            {
                if (reader.TokenType is not JsonTokenType.PropertyName)
                    throw new InvalidMetaException(context.Mod, filePath, $"Expected property name but got {reader.TokenType}.");

                if (reader.NumberProperty("FileVersion"u8, out uint fileVersion))
                {
                    if (context.Mod.LoadedVersion != fileVersion)
                    {
                        ret                       |= ModDataChangeType.Migration;
                        context.Mod.LoadedVersion =  fileVersion;
                    }

                    visited |= ModDataChangeType.Migration;
                    continue;
                }

                if (reader.GuidProperty("Identifier"u8, out var identifier))
                {
                    if (context.Mod.StableIdentifier != identifier)
                    {
                        ret                          |= ModDataChangeType.Identifier;
                        context.Mod.StableIdentifier =  identifier;
                    }

                    visited |= ModDataChangeType.Identifier;
                    continue;
                }

                if (reader.StringProperty("Name"u8, out string? name, true))
                {
                    if (string.IsNullOrEmpty(name))
                        throw new InvalidMetaException(context.Mod, filePath, "Mod with empty name is not allowed.");

                    if (context.Mod.Name != name)
                    {
                        ret              |= ModDataChangeType.Name;
                        context.Mod.Name =  name;
                    }

                    visited |= ModDataChangeType.Name;
                    continue;
                }

                if (reader.StringProperty("Author"u8, out string? author, true))
                {
                    author ??= string.Empty;
                    if (context.Mod.Author != author)
                    {
                        ret                |= ModDataChangeType.Author;
                        context.Mod.Author =  author;
                    }

                    visited |= ModDataChangeType.Author;
                    continue;
                }

                if (reader.StringProperty("Description"u8, out string? description, true))
                {
                    description ??= string.Empty;
                    if (context.Mod.Description != description)
                    {
                        ret                     |= ModDataChangeType.Description;
                        context.Mod.Description =  description;
                    }

                    visited |= ModDataChangeType.Description;
                    continue;
                }

                if (reader.StringProperty("Image"u8, out string? image, true))
                {
                    image ??= string.Empty;
                    if (context.Mod.Image != image)
                    {
                        ret               |= ModDataChangeType.Image;
                        context.Mod.Image =  image;
                    }

                    visited |= ModDataChangeType.Image;
                    continue;
                }

                if (reader.StringProperty("Version"u8, out string? version, true))
                {
                    version ??= string.Empty;
                    if (context.Mod.Version != version)
                    {
                        ret                 |= ModDataChangeType.Version;
                        context.Mod.Version =  version;
                    }

                    visited |= ModDataChangeType.Version;
                    continue;
                }

                if (reader.StringProperty("Website"u8, out string? website, true))
                {
                    website ??= string.Empty;
                    if (context.Mod.Website != website)
                    {
                        ret                 |= ModDataChangeType.Website;
                        context.Mod.Website =  website;
                    }

                    visited |= ModDataChangeType.Website;
                    continue;
                }

                if (reader.ArrayProperty("ModTags"u8, out _, true))
                {
                    var tags = reader.ReadStringArray() ?? [];
                    ret     |= ModDataEditor.UpdateTags(context.Mod, tags!, null);
                    visited |= ModDataChangeType.ModTags;
                    continue;
                }

                if (reader.ArrayProperty("DefaultPreferredItems"u8, out _, true))
                {
                    var items = reader.ReadNumberArray<ulong>();
                    if (items is null)
                    {
                        if (context.Mod.DefaultPreferredItems.Count > 0)
                        {
                            ret |= ModDataChangeType.DefaultChangedItems;
                            context.Mod.DefaultPreferredItems.Clear();
                        }
                    }
                    else if (!context.Mod.DefaultPreferredItems.SetEquals(items.Select(i => new CustomItemId(i))))
                    {
                        ret |= ModDataChangeType.DefaultChangedItems;
                        context.Mod.DefaultPreferredItems.Clear();
                        context.Mod.DefaultPreferredItems.UnionWith(items.Select(i => new CustomItemId(i)));
                    }

                    visited |= ModDataChangeType.DefaultChangedItems;
                    continue;
                }

                if (reader.ArrayProperty("RequiredFeatures"u8, out _, true))
                {
                    var features = reader.ReadFlagEnumArray<FeatureFlags>() ?? FeatureFlags.None;
                    if (context.Mod.RequiredFeatures != features)
                    {
                        ret                          |= ModDataChangeType.RequiredFeatures;
                        context.Mod.RequiredFeatures =  features;
                    }

                    visited |= ModDataChangeType.RequiredFeatures;
                    continue;
                }

                if (reader.ObjectProperty("DefaultData"u8, out _, true))
                {
                    GroupDeserialization.ReadDefaultContainer(context, ref reader, filePath);
                    encounteredDefault = true;
                    continue;
                }

                if (reader.ObjectProperty("PageNames"u8, out var pageNameReader, true))
                {
                    context.Mod.PageNames.Clear();
                    while (pageNameReader.Read(ref reader))
                    {
                        if (reader.TokenType is not JsonTokenType.PropertyName || !reader.TryReadNumber<int>(out var pageNumber))
                            throw new InvalidMetaException(context.Mod, filePath, "PageNames dictionary expected number as property name.");

                        if (!pageNameReader.Read(ref reader) || !reader.TryReadString(out var pageName, "", true))
                            throw new InvalidMetaException(context.Mod, filePath,
                                "PageNames dictionary expected string or null as property value.");

                        context.Mod.PageNames.TryAdd(pageNumber, pageName ?? $"Page {pageNumber + 1}");
                    }

                    visited |= ModDataChangeType.PageNames;
                    continue;
                }

                if (ReadGroups(ref reader, filePath, context))
                    continue;

                reader.Skip();
            }

            ret |= SetDefaults(context.Mod, filePath, visited);

            if (context.Mod.LoadedVersion >= 4 && !encounteredDefault)
            {
                context.Mod.Default.Files.Clear();
                context.Mod.Default.FileSwaps.Clear();
                context.Mod.Default.Manipulations.Clear();
            }

            return new ModMetaData { Changes = ret };
        }

        private static bool ReadGroups(ref Utf8JsonReader reader, string filePath, Context context)
        {
            if (!reader.ArrayProperty("Groups"u8, out var arrayReader, true))
                return false;

            while (arrayReader.Read(ref reader))
            {
                if (GroupDeserialization.ReadGroup(context, ref reader, filePath) is { } group)
                    context.Mod.AddGroup(group, filePath);
            }

            return true;
        }

        private static ModDataChangeType SetDefaults(Mod mod, string filePath, ModDataChangeType visited)
        {
            if (!visited.HasFlag(ModDataChangeType.Migration) || mod.LoadedVersion < 1)
                throw new InvalidMetaException(mod, filePath, "No file version provided.");

            if (!visited.HasFlag(ModDataChangeType.Name) || mod.Name.Length is 0)
                throw new InvalidMetaException(mod, filePath, "Either no or empty mod name provided.");

            ModDataChangeType ret = 0;
            if (!visited.HasFlag(ModDataChangeType.Identifier))
            {
                mod.StableIdentifier =  Guid.NewGuid();
                ret                  |= ModDataChangeType.Identifier;
            }

            if (!visited.HasFlag(ModDataChangeType.Author) && mod.Author.Length > 0)
            {
                mod.Author =  string.Empty;
                ret        |= ModDataChangeType.Author;
            }

            if (!visited.HasFlag(ModDataChangeType.Description) && mod.Description.Length > 0)
            {
                mod.Description =  string.Empty;
                ret             |= ModDataChangeType.Description;
            }

            if (!visited.HasFlag(ModDataChangeType.Image) && mod.Image.Length > 0)
            {
                mod.Image =  string.Empty;
                ret       |= ModDataChangeType.Image;
            }

            if (!visited.HasFlag(ModDataChangeType.Version) && mod.Version.Length > 0)
            {
                mod.Version =  string.Empty;
                ret         |= ModDataChangeType.Version;
            }

            if (!visited.HasFlag(ModDataChangeType.Website) && mod.Website.Length > 0)
            {
                mod.Website =  string.Empty;
                ret         |= ModDataChangeType.Website;
            }

            if (!visited.HasFlag(ModDataChangeType.PreferredChangedItems) && mod.DefaultPreferredItems.Count > 0)
            {
                mod.DefaultPreferredItems.Clear();
                ret |= ModDataChangeType.DefaultChangedItems;
            }

            if (!visited.HasFlag(ModDataChangeType.RequiredFeatures) && mod.RequiredFeatures is not FeatureFlags.None)
            {
                mod.RequiredFeatures =  FeatureFlags.None;
                ret                  |= ModDataChangeType.RequiredFeatures;
            }

            if (!visited.HasFlag(ModDataChangeType.ModTags) && mod.ModTags.Count > 0)
            {
                mod.ModTags =  [];
                ret         |= ModDataChangeType.ModTags;
            }

            if (!visited.HasFlag(ModDataChangeType.PageNames) && mod.PageNames.Count > 0)
            {
                mod.PageNames =  [];
                ret           |= ModDataChangeType.PageNames;
            }

            return ret;
        }
    }
}

public sealed class MetaMissingException(Mod mod, string metaPath) : FileNotFoundException
{
    public readonly Mod    Mod      = mod;
    public readonly string MetaPath = metaPath;
}

public sealed class InvalidMetaException(Mod mod, string filePath, string reason) : JsonException
{
    public readonly Mod    Mod      = mod;
    public readonly string FilePath = filePath;
    public readonly string Reason   = reason;
}
