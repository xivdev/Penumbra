using Luna;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.GameData.Structs;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.Mods;

public readonly struct ModLocalData(Mod mod) : ISavable
{
    public const int FileVersion = 3;

    public string ToFilePath(FilenameService fileNames)
        => fileNames.LocalDataFile(mod);

    public void Save(StreamWriter writer)
    {
        var jObject = new JObject
        {
            { nameof(FileVersion), JToken.FromObject(FileVersion) },
            { nameof(Mod.ImportDate), JToken.FromObject(mod.ImportDate) },
            { nameof(Mod.LocalTags), JToken.FromObject(mod.LocalTags) },
            { nameof(Mod.Note), JToken.FromObject(mod.Note) },
            { nameof(Mod.Favorite), JToken.FromObject(mod.Favorite) },
            { nameof(Mod.PreferredChangedItems), JToken.FromObject(mod.PreferredChangedItems) },
        };

        if (mod.Path.Folder.Length > 0)
            jObject["FileSystemFolder"] = mod.Path.Folder;
        if (mod.Path.SortName is not null)
            jObject["SortOrderName"] = mod.Path.SortName;

        using var jWriter = new JsonTextWriter(writer);
        jWriter.Formatting = Formatting.Indented;
        jObject.WriteTo(jWriter);
    }

    public static ModDataChangeType Load(ModDataEditor editor, Mod mod)
    {
        var dataFile = editor.SaveService.FileNames.LocalDataFile(mod);

        var     importDate       = 0L;
        var     localTags        = Enumerable.Empty<string>();
        var     favorite         = false;
        var     note             = string.Empty;
        var     fileSystemFolder = string.Empty;
        string? sortOrderName    = null;

        HashSet<CustomItemId> preferredChangedItems = [];

        var save = true;
        if (File.Exists(dataFile))
            try
            {
                var text = File.ReadAllText(dataFile);
                var json = JObject.Parse(text);

                importDate = json[nameof(Mod.ImportDate)]?.Value<long>() ?? importDate;
                favorite   = json[nameof(Mod.Favorite)]?.Value<bool>() ?? favorite;
                note       = json[nameof(Mod.Note)]?.Value<string>() ?? note;
                localTags  = (json[nameof(Mod.LocalTags)] as JArray)?.Values<string>().OfType<string>() ?? localTags;
                preferredChangedItems =
                    (json[nameof(Mod.PreferredChangedItems)] as JArray)?.Values<ulong>().Select(i => (CustomItemId)i).ToHashSet()
                 ?? mod.DefaultPreferredItems;
                fileSystemFolder = json["FileSystemFolder"]?.Value<string>() ?? string.Empty;
                sortOrderName    = json["SortOrderName"]?.Value<string>()?.FixName();
                save             = false;
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not load local mod data:\n{e}");
            }
        else
            preferredChangedItems = mod.DefaultPreferredItems;

        if (importDate == 0)
            importDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        ModDataChangeType changes = 0;
        if (mod.ImportDate != importDate)
        {
            mod.ImportDate =  importDate;
            changes        |= ModDataChangeType.ImportDate;
        }

        changes |= UpdateTags(mod, null, localTags);

        if (mod.Favorite != favorite)
        {
            mod.Favorite =  favorite;
            changes      |= ModDataChangeType.Favorite;
        }

        if (mod.Note != note)
        {
            mod.Note =  note;
            changes  |= ModDataChangeType.Note;
        }

        if (!preferredChangedItems.SetEquals(mod.PreferredChangedItems))
        {
            mod.PreferredChangedItems =  preferredChangedItems;
            changes                   |= ModDataChangeType.PreferredChangedItems;
        }

        if (!mod.Path.Folder.Equals(fileSystemFolder, StringComparison.OrdinalIgnoreCase))
        {
            mod.Path.Folder =  fileSystemFolder;
            changes         |= ModDataChangeType.FileSystemFolder;
        }

        if (mod.Path.SortName != sortOrderName)
        {
            mod.Path.SortName =  sortOrderName;
            changes           |= ModDataChangeType.FileSystemSortOrder;
        }

        if (save)
            editor.SaveService.QueueSave(new ModLocalData(mod));

        return changes;
    }

    internal static ModDataChangeType UpdateTags(Mod mod, IEnumerable<string>? newModTags, IEnumerable<string>? newLocalTags)
    {
        if (newModTags == null && newLocalTags == null)
            return 0;

        ModDataChangeType type = 0;
        if (newModTags != null)
        {
            var modTags = newModTags.Where(t => t.Length > 0).Distinct().ToArray();
            if (!modTags.SequenceEqual(mod.ModTags))
            {
                newLocalTags ??= mod.LocalTags;
                mod.ModTags  =   modTags;
                type         |=  ModDataChangeType.ModTags;
            }
        }

        if (newLocalTags != null)
        {
            var localTags = newLocalTags!.Where(t => t.Length > 0 && !mod.ModTags.Contains(t)).Distinct().ToArray();
            if (!localTags.SequenceEqual(mod.LocalTags))
            {
                mod.LocalTags =  localTags;
                type          |= ModDataChangeType.LocalTags;
            }
        }

        return type;
    }
}
