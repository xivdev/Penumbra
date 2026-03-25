using System.Text.Json;
using ImSharp;
using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Structs;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.Mods;

public readonly struct ModMeta(Mod mod) : ISavable
{
    public const uint FileVersion = 3;

    public string ToFilePath(FilenameService fileNames)
        => fileNames.ModMetaPath(mod);

    public void Save(Stream stream)
    {
        using var j = new Utf8JsonWriter(stream, JsonFunctions.WriterOptions);
        j.WriteStartObject();

        j.WriteNumber("FileVersion"u8, FileVersion);
        j.WriteString("Name"u8,        mod.Name);
        j.WriteString("Author"u8,      mod.Author);
        j.WriteString("Description"u8, mod.Description);
        j.WriteString("Image"u8,       mod.Image);
        j.WriteString("Version"u8,     mod.Version);
        j.WriteString("Website"u8,     mod.Website);

        if (mod.ModTags.Count > 0)
        {
            j.WritePropertyName("ModTags"u8);
            j.WriteStartArray();
            foreach (var tag in mod.ModTags)
                j.WriteStringValue(tag);
            j.WriteEndArray();
        }

        if (mod.DefaultPreferredItems.Count > 0)
        {
            j.WritePropertyName("DefaultPreferredItems"u8);
            j.WriteStartArray();
            foreach (var item in mod.DefaultPreferredItems)
                j.WriteNumberValue(item);
            j.WriteEndArray();
        }

        if (mod.RequiredFeatures is not FeatureFlags.None)
        {
            var features = mod.RequiredFeatures;
            j.WritePropertyName("RequiredFeatures"u8);
            j.WriteStartArray();
            foreach (var flag in FeatureFlags.Values)
            {
                if ((features & flag) is not FeatureFlags.None)
                    j.WriteStringValue(flag.ToNameU8());
            }

            j.WriteEndArray();
        }

        j.WriteEndObject();
        j.Flush();
    }

    public static ModDataChangeType Load(ModDataEditor editor, ModCreator creator, Mod mod)
    {
        var metaFile = editor.SaveService.FileNames.ModMetaPath(mod);

        var newFileVersion = 0u;

        // Empty name gets checked after loading and is not allowed.
        var newName          = string.Empty;
        var newAuthor        = string.Empty;
        var newDescription   = string.Empty;
        var newImage         = string.Empty;
        var newVersion       = string.Empty;
        var newWebsite       = string.Empty;
        var modTags          = Enumerable.Empty<string>();
        var defaultItems     = new HashSet<CustomItemId>();
        var requiredFeatures = FeatureFlags.None;

        if (!File.Exists(metaFile))
        {
            Penumbra.Log.Debug($"No mod meta found for {mod.ModPath.Name}.");
            return ModDataChangeType.Deletion;
        }

        try
        {
            var data   = JsonFunctions.ReadUtf8Bytes(metaFile);
            var reader = new Utf8JsonReader(data.Span, JsonFunctions.ReaderOptions);

            while (reader.Read())
            {
                if (reader.TokenType is not JsonTokenType.PropertyName)
                    continue;

                if (reader.ValueTextEquals("FileVersion"u8))
                {
                    reader.Read();
                    newFileVersion = reader.GetUInt32();
                }
                else if (reader.ValueTextEquals("Name"u8))
                {
                    reader.Read();
                    newName = reader.GetString() ?? string.Empty;
                }
                else if (reader.ValueTextEquals("Author"u8))
                {
                    reader.Read();
                    newAuthor = reader.GetString() ?? string.Empty;
                }
                else if (reader.ValueTextEquals("Description"u8))
                {
                    reader.Read();
                    newDescription = reader.GetString() ?? string.Empty;
                }
                else if (reader.ValueTextEquals("Image"u8))
                {
                    reader.Read();
                    newImage = reader.GetString() ?? string.Empty;
                }
                else if (reader.ValueTextEquals("Version"u8))
                {
                    reader.Read();
                    newVersion = reader.GetString() ?? string.Empty;
                }
                else if (reader.ValueTextEquals("Website"u8))
                {
                    reader.Read();
                    newWebsite = reader.GetString() ?? string.Empty;
                }
                else if (reader.ValueTextEquals("ModTags"u8))
                {
                    reader.Read();
                    if (reader.TokenType is not JsonTokenType.StartArray)
                        continue;

                    var tags = new HashSet<string>();
                    while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray && reader.GetString() is { Length: > 0 } tag)
                        tags.Add(tag);
                    modTags = tags;
                }
                else if (reader.ValueTextEquals("DefaultPreferredItems"u8))
                {
                    reader.Read();
                    if (reader.TokenType is not JsonTokenType.StartArray)
                        continue;

                    while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
                        defaultItems.Add(reader.GetUInt64());
                }
                else if (reader.ValueTextEquals("RequiredFeatures"u8))
                {
                    reader.Read();
                    if (reader.TokenType is not JsonTokenType.StartArray)
                        continue;

                    while (reader.Read()
                        && reader.TokenType is not JsonTokenType.EndArray
                        && Enum.TryParse(reader.GetString() ?? string.Empty, true, out FeatureFlags flag))
                        requiredFeatures |= flag;
                }
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not load mod meta for {metaFile}:\n{e}");
            return ModDataChangeType.Deletion;
        }

        ModDataChangeType changes = 0;
        if (mod.Name != newName)
        {
            changes  |= ModDataChangeType.Name;
            mod.Name =  newName;
        }

        if (mod.Author != newAuthor)
        {
            changes    |= ModDataChangeType.Author;
            mod.Author =  newAuthor;
        }

        if (mod.Description != newDescription)
        {
            changes         |= ModDataChangeType.Description;
            mod.Description =  newDescription;
        }

        if (mod.Image != newImage)
        {
            changes   |= ModDataChangeType.Image;
            mod.Image =  newImage;
        }

        if (mod.Version != newVersion)
        {
            changes     |= ModDataChangeType.Version;
            mod.Version =  newVersion;
        }

        if (mod.Website != newWebsite)
        {
            changes     |= ModDataChangeType.Website;
            mod.Website =  newWebsite;
        }

        if (!mod.DefaultPreferredItems.SetEquals(defaultItems))
        {
            changes                   |= ModDataChangeType.DefaultChangedItems;
            mod.DefaultPreferredItems =  defaultItems;
        }

        // TODO: No JObject optimization.
        if (newFileVersion != FileVersion
         && ModMigration.Migrate(creator, editor.SaveService, mod, JObject.Parse(File.ReadAllText(metaFile)), ref newFileVersion))
        {
            changes |= ModDataChangeType.Migration;
            editor.SaveService.ImmediateSave(new ModMeta(mod));
        }

        // Required features get checked during parsing, in which case the new required features signal invalid.
        if (requiredFeatures != mod.RequiredFeatures)
        {
            changes              |= ModDataChangeType.RequiredFeatures;
            mod.RequiredFeatures =  requiredFeatures;
        }

        changes |= ModDataEditor.UpdateTags(mod, modTags, null);

        return changes;
    }
}
