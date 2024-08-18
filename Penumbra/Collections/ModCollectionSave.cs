using Newtonsoft.Json.Linq;
using Penumbra.Services;
using Newtonsoft.Json;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;

namespace Penumbra.Collections;

/// <summary>
/// Handle saving and loading a collection.
/// </summary>
internal readonly struct ModCollectionSave(ModStorage modStorage, ModCollection modCollection) : ISavable
{
    public string ToFilename(FilenameService fileNames)
        => fileNames.CollectionFile(modCollection);

    public string LogName(string _)
        => modCollection.AnonymizedName;

    public string TypeName
        => "Collection";

    public void Save(StreamWriter writer)
    {
        using var j = new JsonTextWriter(writer);
        j.Formatting = Formatting.Indented;
        var x = JsonSerializer.Create(new JsonSerializerSettings { Formatting = Formatting.Indented });
        j.WriteStartObject();
        j.WritePropertyName("Version");
        j.WriteValue(ModCollection.CurrentVersion);
        j.WritePropertyName(nameof(ModCollection.Id));
        j.WriteValue(modCollection.Identifier);
        j.WritePropertyName(nameof(ModCollection.Name));
        j.WriteValue(modCollection.Name);
        j.WritePropertyName(nameof(ModCollection.Settings));

        // Write all used and unused settings by mod directory name.
        j.WriteStartObject();
        var list = new List<(string, ModSettings.SavedSettings)>(modCollection.Settings.Count + modCollection.UnusedSettings.Count);
        for (var i = 0; i < modCollection.Settings.Count; ++i)
        {
            var settings = modCollection.Settings[i];
            if (settings != null)
                list.Add((modStorage[i].ModPath.Name, new ModSettings.SavedSettings(settings, modStorage[i])));
        }

        list.AddRange(modCollection.UnusedSettings.Select(kvp => (kvp.Key, kvp.Value)));
        list.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));

        foreach (var (modDir, settings) in list)
        {
            j.WritePropertyName(modDir);
            x.Serialize(j, settings);
        }

        j.WriteEndObject();

        // Inherit by collection name.
        j.WritePropertyName("Inheritance");
        x.Serialize(j, modCollection.InheritanceByName ?? modCollection.DirectlyInheritsFrom.Select(c => c.Identifier));
        j.WriteEndObject();
    }

    public static bool LoadFromFile(FileInfo file, out Guid id, out string name, out int version, out Dictionary<string, ModSettings.SavedSettings> settings,
        out IReadOnlyList<string> inheritance)
    {
        settings    = [];
        inheritance = [];
        if (!file.Exists)
        {
            Penumbra.Log.Error("Could not read collection because file does not exist.");
            name    = string.Empty;
            id      = Guid.Empty;
            version = 0;
            return false;
        }

        try
        {
            var obj = JObject.Parse(File.ReadAllText(file.FullName));
            version = obj["Version"]?.ToObject<int>() ?? 0;
            name    = obj[nameof(ModCollection.Name)]?.ToObject<string>() ?? string.Empty;
            id      = obj[nameof(ModCollection.Id)]?.ToObject<Guid>() ?? (version == 1 ? Guid.NewGuid() : Guid.Empty);
            // Custom deserialization that is converted with the constructor. 
            settings    = obj[nameof(ModCollection.Settings)]?.ToObject<Dictionary<string, ModSettings.SavedSettings>>() ?? settings;
            inheritance = obj["Inheritance"]?.ToObject<List<string>>() ?? inheritance;
            return true;
        }
        catch (Exception e)
        {
            name    = string.Empty;
            version = 0;
            id      = Guid.Empty;
            Penumbra.Log.Error($"Could not read collection information from file:\n{e}");
            return false;
        }
    }
}
