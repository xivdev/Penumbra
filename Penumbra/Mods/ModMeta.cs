using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Structs;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.Mods;

public readonly struct ModMeta(Mod mod) : ISavable
{
    public const uint FileVersion = 3;

    public string ToFilename(FilenameService fileNames)
        => fileNames.ModMetaPath(mod);

    public void Save(StreamWriter writer)
    {
        var jObject = new JObject
        {
            { nameof(FileVersion), JToken.FromObject(FileVersion) },
            { nameof(Mod.Name), JToken.FromObject(mod.Name) },
            { nameof(Mod.Author), JToken.FromObject(mod.Author) },
            { nameof(Mod.Description), JToken.FromObject(mod.Description) },
            { nameof(Mod.Image), JToken.FromObject(mod.Image) },
            { nameof(Mod.Version), JToken.FromObject(mod.Version) },
            { nameof(Mod.Website), JToken.FromObject(mod.Website) },
            { nameof(Mod.ModTags), JToken.FromObject(mod.ModTags) },
            { nameof(Mod.DefaultPreferredItems), JToken.FromObject(mod.DefaultPreferredItems) },
        };
        using var jWriter = new JsonTextWriter(writer);
        jWriter.Formatting = Formatting.Indented;
        jObject.WriteTo(jWriter);
    }

    public static ModDataChangeType Load(ModDataEditor editor, ModCreator creator, Mod mod)
    {
        var metaFile = editor.SaveService.FileNames.ModMetaPath(mod);
        if (!File.Exists(metaFile))
        {
            Penumbra.Log.Debug($"No mod meta found for {mod.ModPath.Name}.");
            return ModDataChangeType.Deletion;
        }

        try
        {
            var text = File.ReadAllText(metaFile);
            var json = JObject.Parse(text);

            var newFileVersion = json[nameof(FileVersion)]?.Value<uint>() ?? 0;

            // Empty name gets checked after loading and is not allowed.
            var newName = json[nameof(Mod.Name)]?.Value<string>() ?? string.Empty;

            var newAuthor      = json[nameof(Mod.Author)]?.Value<string>() ?? string.Empty;
            var newDescription = json[nameof(Mod.Description)]?.Value<string>() ?? string.Empty;
            var newImage       = json[nameof(Mod.Image)]?.Value<string>() ?? string.Empty;
            var newVersion     = json[nameof(Mod.Version)]?.Value<string>() ?? string.Empty;
            var newWebsite     = json[nameof(Mod.Website)]?.Value<string>() ?? string.Empty;
            var modTags        = (json[nameof(Mod.ModTags)] as JArray)?.Values<string>().OfType<string>();
            var defaultItems = (json[nameof(Mod.DefaultPreferredItems)] as JArray)?.Values<ulong>().Select(i => (CustomItemId)i).ToHashSet()
             ?? [];

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

            if (newFileVersion != FileVersion)
                if (ModMigration.Migrate(creator, editor.SaveService, mod, json, ref newFileVersion))
                {
                    changes |= ModDataChangeType.Migration;
                    editor.SaveService.ImmediateSave(new ModMeta(mod));
                }

            changes |= ModLocalData.UpdateTags(mod, modTags, null);

            return changes;
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not load mod meta for {metaFile}:\n{e}");
            return ModDataChangeType.Deletion;
        }
    }
}
