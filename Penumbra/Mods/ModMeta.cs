using System.Text.Json;
using ImSharp;
using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.Files;
using Penumbra.GameData.Structs;
using Penumbra.Mods.Manager;

namespace Penumbra.Mods;

public readonly struct ModMeta(Mod mod) : ISavable
{
    public const uint CurrentFileVersion = 3;

    public string ToFilePath(FilenameService fileNames)
        => fileNames.ModMetaPath(mod);

    public void Save(Stream stream)
    {
        using var j = new Utf8JsonWriter(stream, JsonFunctions.WriterOptions);
        j.WriteStartObject();

        j.WriteNumber("FileVersion"u8, CurrentFileVersion);
        j.WriteString("Identifier"u8,  mod.StableIdentifier);
        j.WriteString("Name"u8,        mod.Name);
        j.WriteNonEmptyString("Author"u8,      mod.Author);
        j.WriteNonEmptyString("Description"u8, mod.Description);
        j.WriteNonEmptyString("Image"u8,       mod.Image);
        j.WriteNonEmptyString("Version"u8,     mod.Version);
        j.WriteNonEmptyString("Website"u8,     mod.Website);

        if (mod.ModTags.Count > 0)
        {
            j.WriteStartArray("ModTags"u8);
            foreach (var tag in mod.ModTags)
                j.WriteStringValue(tag);
            j.WriteEndArray();
        }

        if (mod.DefaultPreferredItems.Count > 0)
        {
            j.WriteStartArray("DefaultPreferredItems"u8);
            foreach (var item in mod.DefaultPreferredItems)
                j.WriteNumberValue(item);
            j.WriteEndArray();
        }

        if (mod.RequiredFeatures is not FeatureFlags.None)
        {
            var features = mod.RequiredFeatures;
            j.WriteStartArray("RequiredFeatures"u8);
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
        if (!File.Exists(metaFile))
        {
            creator.FailedMod.AddMissingMeta(mod);
            return ModDataChangeType.Deletion;
        }

        try
        {
            var data   = JsonFunctions.ReadUtf8Bytes(metaFile, out _);
            var reader = new Utf8JsonReader(data.Span, JsonFunctions.ReaderOptions);
            var dto    = Dto.Read(ref reader);
            return dto.Apply(editor, creator, mod, metaFile);
        }
        catch (Exception e)
        {
            creator.FailedMod.AddInvalidMeta(mod, e);
            return ModDataChangeType.Deletion;
        }
    }

    public struct Dto
    {
        public uint?         FileVersion;
        public Guid?         StableIdentifier;
        public string?       Name;
        public string?       Author;
        public string?       Description;
        public string?       Image;
        public string?       Version;
        public string?       Website;
        public List<string>? ModTags;
        public List<ulong>?  PreferredItems;
        public FeatureFlags  RequiredFeatures;

        public ModDataChangeType Apply(ModDataEditor editor, ModCreator creator, Mod mod, string metaFile)
        {
            ModDataChangeType changes = 0;
            if (mod.Name != Name)
            {
                changes  |= ModDataChangeType.Name;
                mod.Name =  Name ?? string.Empty;
            }

            if (mod.StableIdentifier != StableIdentifier)
            {
                if (StableIdentifier.HasValue)
                    mod.StableIdentifier = StableIdentifier.Value;
                else
                    mod.StableIdentifier = Guid.NewGuid();
                changes |= ModDataChangeType.Identifier;
            }

            if (mod.Author != Author)
            {
                changes    |= ModDataChangeType.Author;
                mod.Author =  Author ?? string.Empty;
            }

            if (mod.Description != Description)
            {
                changes         |= ModDataChangeType.Description;
                mod.Description =  Description ?? string.Empty;
            }

            if (mod.Image != Image)
            {
                changes   |= ModDataChangeType.Image;
                mod.Image =  Image ?? string.Empty;
            }

            if (mod.Version != Version)
            {
                changes     |= ModDataChangeType.Version;
                mod.Version =  Version ?? string.Empty;
            }

            if (mod.Website != Website)
            {
                changes     |= ModDataChangeType.Website;
                mod.Website =  Website ?? string.Empty;
            }

            if (PreferredItems is null)
            {
                if (mod.DefaultPreferredItems.Count > 0)
                {
                    changes |= ModDataChangeType.DefaultChangedItems;
                    mod.DefaultPreferredItems.Clear();
                }
            }
            else if (!mod.DefaultPreferredItems.SetEquals(PreferredItems!.Select(i => new CustomItemId(i))))
            {
                changes |= ModDataChangeType.DefaultChangedItems;
                mod.DefaultPreferredItems.Clear();
                mod.DefaultPreferredItems.UnionWith(PreferredItems!.Select(i => new CustomItemId(i)));
            }

            // TODO: No JObject optimization.
            if (FileVersion != CurrentFileVersion)
            {
                if (FileVersion is null)
                    throw new Exception("No mod meta version provided to migrate from.");

                var version = FileVersion.Value;
                if (ModMigration.Migrate(creator, editor.SaveService, mod, JObject.Parse(File.ReadAllText(metaFile)),
                        ref version))
                {
                    changes |= ModDataChangeType.Migration;
                    editor.SaveService.ImmediateSave(new ModMeta(mod));
                }
            }

            // Required features get checked during parsing, in which case the new required features signal invalid.
            if (RequiredFeatures != mod.RequiredFeatures)
            {
                changes              |= ModDataChangeType.RequiredFeatures;
                mod.RequiredFeatures =  RequiredFeatures;
            }

            changes |= ModDataEditor.UpdateTags(mod, ModTags, null);
            return changes;
        }

        public static Dto Read(ref Utf8JsonReader reader)
        {
            if (!reader.Read())
                throw new JsonException("Empty Data");

            var ret = new Dto();
            var obj = reader.CreateObjectReader();
            while (obj.Read(ref reader))
            {
                if (reader.TokenType is not JsonTokenType.PropertyName)
                    throw new JsonException("Expected property");

                if (reader.CheckProperty("FileVersion"u8))
                    ret.FileVersion = reader.TryReadNumber(out uint fv) ? fv : throw new JsonException();
                else if (reader.CheckProperty("Identifier"u8))
                    ret.StableIdentifier = reader.TryGetGuid(out var guid) ? guid : null;
                else if (reader.CheckProperty("Name"u8))
                    ret.Name = reader.GetString();
                else if (reader.CheckProperty("Author"u8))
                    ret.Author = reader.GetString();
                else if (reader.CheckProperty("Description"u8))
                    ret.Description = reader.GetString();
                else if (reader.CheckProperty("Image"u8))
                    ret.Image = reader.GetString();
                else if (reader.CheckProperty("Version"u8))
                    ret.Version = reader.GetString();
                else if (reader.CheckProperty("Website"u8))
                    ret.Website = reader.GetString();
                else if (reader.CheckProperty("ModTags"u8))
                    ret.ModTags = reader.ReadStringArray()!;
                else if (reader.CheckProperty("DefaultPreferredItems"u8))
                    ret.PreferredItems = reader.ReadNumberArray<ulong>();
                else if (reader.CheckProperty("RequiredFeatures"u8))
                    ret.RequiredFeatures = reader.ReadFlagEnumArray<FeatureFlags>() ?? FeatureFlags.None;
                else
                    reader.Skip();
            }

            return ret;
        }

        public bool Validate(out string? error)
        {
            if (FileVersion is null)
            {
                error = "Mod without provided file version is not allowed.";
                return false;
            }

            if (string.IsNullOrEmpty(Name))
            {
                error = "Mod with empty name is not allowed.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
