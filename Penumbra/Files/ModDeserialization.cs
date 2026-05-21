using System.Text.Json;
using Luna;
using Penumbra.GameData.Structs;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.Files;

public static class ModDeserialization
{
    public static void Test(ModManager mods, SaveService files)
    {
        foreach (var mod in mods)
        {
            var testMod = new Mod(mod.ModPath);
            try
            {
                ReloadMod(files, testMod);
                if (!DebugUtilities.CompareMod(mod, testMod, false))
                    Penumbra.Log.Information($"[Failure] {mod.Name}");
                else
                    Penumbra.Log.Debug($"[Success] {mod.Name}");
            }
            catch (Exception ex)
            {
                Penumbra.Log.Information($"[Failure] {mod.Name} {ex.GetType().Name}");
            }
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

        var meta = IJsonParsable.ReadJson<ModMetaData>(files, metaPath, false, mod).Changes;
        MigrateGroups(files, mod);
        return meta;
    }

    private static void MigrateGroups(SaveService files, Mod mod)
    {
        if (mod.LoadedVersion >= 4)
            return;

        GroupDeserialization.ReadDefaultContainerFile(files, mod);
        foreach (var groupFile in files.FileNames.GetOptionGroupFiles(mod))
        {
            if (GroupDeserialization.ReadGroupFile(files, mod, groupFile) is { } group)
                mod.AddGroup(group, groupFile.FullName);
        }
    }

    public ref struct ModMetaData() : IJsonParsable<ModMetaData>
    {
        public ModDataChangeType Changes;

        public static ModMetaData Read(scoped ref Utf8JsonReader reader, string filePath, object? userInput)
        {
            if (userInput is not Mod mod)
                throw new ArgumentException("The user input for reading a Mod has to be the mod object.");

            if (!reader.Read() || reader.TokenType is not JsonTokenType.StartObject)
                throw new InvalidMetaException(mod, filePath, "File is empty or malformed.");

            ModDataChangeType visited            = 0;
            ModDataChangeType ret                = 0;
            var               encounteredDefault = false;
            var               objectReader       = reader.CreateObjectLimit();

            mod.Groups.Clear();
            mod.SubObjects.Clear();
            while (objectReader.Read(ref reader))
            {
                if (reader.TokenType is not JsonTokenType.PropertyName)
                    throw new InvalidMetaException(mod, filePath, $"Expected property name but got {reader.TokenType}.");

                if (reader.NumberProperty("FileVersion"u8, out uint fileVersion))
                {
                    if (mod.LoadedVersion != fileVersion)
                    {
                        ret               |= ModDataChangeType.Migration;
                        mod.LoadedVersion =  fileVersion;
                    }

                    visited |= ModDataChangeType.Migration;
                    continue;
                }

                if (reader.GuidProperty("Identifier"u8, out var identifier))
                {
                    if (mod.StableIdentifier != identifier)
                    {
                        ret                  |= ModDataChangeType.Identifier;
                        mod.StableIdentifier =  identifier;
                    }

                    visited |= ModDataChangeType.Identifier;
                    continue;
                }

                if (reader.StringProperty("Name"u8, out string? name, true))
                {
                    if (string.IsNullOrEmpty(name))
                        throw new InvalidMetaException(mod, filePath, "Mod with empty name is not allowed.");

                    if (mod.Name != name)
                    {
                        ret      |= ModDataChangeType.Name;
                        mod.Name =  name;
                    }

                    visited |= ModDataChangeType.Name;
                    continue;
                }

                if (reader.StringProperty("Author"u8, out string? author, true))
                {
                    author ??= string.Empty;
                    if (mod.Author != author)
                    {
                        ret        |= ModDataChangeType.Author;
                        mod.Author =  author;
                    }

                    visited |= ModDataChangeType.Author;
                    continue;
                }

                if (reader.StringProperty("Description"u8, out string? description, true))
                {
                    description ??= string.Empty;
                    if (mod.Description != description)
                    {
                        ret             |= ModDataChangeType.Description;
                        mod.Description =  description;
                    }

                    visited |= ModDataChangeType.Description;
                    continue;
                }

                if (reader.StringProperty("Image"u8, out string? image, true))
                {
                    image ??= string.Empty;
                    if (mod.Image != image)
                    {
                        ret       |= ModDataChangeType.Image;
                        mod.Image =  image;
                    }

                    visited |= ModDataChangeType.Image;
                    continue;
                }

                if (reader.StringProperty("Version"u8, out string? version, true))
                {
                    version ??= string.Empty;
                    if (mod.Version != version)
                    {
                        ret         |= ModDataChangeType.Version;
                        mod.Version =  version;
                    }

                    visited |= ModDataChangeType.Version;
                    continue;
                }

                if (reader.StringProperty("Website"u8, out string? website, true))
                {
                    website ??= string.Empty;
                    if (mod.Website != website)
                    {
                        ret         |= ModDataChangeType.Website;
                        mod.Website =  website;
                    }

                    visited |= ModDataChangeType.Website;
                    continue;
                }

                if (reader.ArrayProperty("ModTags"u8, out _, true))
                {
                    var tags = reader.ReadStringArray() ?? [];
                    ret     |= ModDataEditor.UpdateTags(mod, tags!, null);
                    visited |= ModDataChangeType.ModTags;
                    continue;
                }

                if (reader.ArrayProperty("DefaultPreferredItems"u8, out _, true))
                {
                    var items = reader.ReadNumberArray<ulong>();
                    if (items is null)
                    {
                        if (mod.DefaultPreferredItems.Count > 0)
                        {
                            ret |= ModDataChangeType.DefaultChangedItems;
                            mod.DefaultPreferredItems.Clear();
                        }
                    }
                    else if (!mod.DefaultPreferredItems.SetEquals(items.Select(i => new CustomItemId(i))))
                    {
                        ret |= ModDataChangeType.DefaultChangedItems;
                        mod.DefaultPreferredItems.Clear();
                        mod.DefaultPreferredItems.UnionWith(items.Select(i => new CustomItemId(i)));
                    }

                    visited |= ModDataChangeType.DefaultChangedItems;
                    continue;
                }

                if (reader.ArrayProperty("RequiredFeatures"u8, out _, true))
                {
                    var features = reader.ReadFlagEnumArray<FeatureFlags>() ?? FeatureFlags.None;
                    if (mod.RequiredFeatures != features)
                    {
                        ret                  |= ModDataChangeType.RequiredFeatures;
                        mod.RequiredFeatures =  features;
                    }

                    visited |= ModDataChangeType.RequiredFeatures;
                    continue;
                }

                if (reader.ObjectProperty("DefaultData"u8, out _, true))
                {
                    GroupDeserialization.ReadDefaultContainer(ref reader, filePath, mod);
                    encounteredDefault = true;
                    continue;
                }

                if (ReadGroups(ref reader, filePath, mod))
                    continue;

                reader.Skip();
            }

            ret |= SetDefaults(mod, filePath, visited);

            if (mod.LoadedVersion >= 4 && !encounteredDefault)
            {
                mod.Default.Files.Clear();
                mod.Default.FileSwaps.Clear();
                mod.Default.Manipulations.Clear();
            }

            return new ModMetaData { Changes = ret };
        }

        private static bool ReadGroups(ref Utf8JsonReader reader, string filePath, Mod mod)
        {
            if (!reader.ArrayProperty("Groups"u8, out var arrayReader, true))
                return false;

            while (arrayReader.Read(ref reader))
            {
                if (GroupDeserialization.ReadGroup(ref reader, filePath, mod) is { } group)
                    mod.AddGroup(group, filePath);
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
