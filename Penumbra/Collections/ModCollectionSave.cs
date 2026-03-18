using System.Text.Json;
using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.Services;
using Newtonsoft.Json;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Penumbra.Collections;

/// <summary>
/// Handle saving and loading a collection.
/// </summary>
internal readonly struct ModCollectionSave(ModStorage modStorage, ModCollection modCollection) : ISavable
{
    public string ToFilePath(FilenameService fileNames)
        => fileNames.CollectionFile(modCollection);

    public string LogName(string _)
        => modCollection.Identity.AnonymizedName;

    public string TypeName
        => "Collection";

    public void Save(Stream stream)
    {
        using var j = new Utf8JsonWriter(stream, JsonFunctions.WriterOptions);
        j.WriteStartObject();
        j.WriteNumber("Version"u8, ModCollection.CurrentVersion);
        j.WriteString("Id"u8, modCollection.Identity.Identifier);
        j.WriteString("Name"u8, modCollection.Identity.Name);
        j.WritePropertyName("Settings"u8);

        // Write all used and unused settings by mod directory name.
        j.WriteStartObject();
        var list = new List<(string, ModSettings.SavedSettings)>(modCollection.Settings.Count + modCollection.Settings.Unused.Count);
        for (var i = 0; i < modCollection.Settings.Count; ++i)
        {
            var settings = modCollection.GetOwnSettings(i);
            if (settings is not null)
                list.Add((modStorage[i].ModPath.Name, new ModSettings.SavedSettings(settings, modStorage[i])));
        }

        list.AddRange(modCollection.Settings.Unused.Select(kvp => (kvp.Key, kvp.Value)));
        list.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));

        foreach (var (modDir, settings) in list)
        {
            j.WritePropertyName(modDir);
            settings.Write(j);
        }

        j.WriteEndObject();

        // Inherit by collection name.
        j.WritePropertyName("Inheritance"u8);
        j.WriteStartArray();
        foreach(var i in modCollection.Inheritance.Identifiers)
            j.WriteStringValue(i);
        j.WriteEndArray();
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
            name    = obj[nameof(ModCollectionIdentity.Name)]?.ToObject<string>() ?? string.Empty;
            id      = obj[nameof(ModCollectionIdentity.Id)]?.ToObject<Guid>() ?? (version is 1 ? Guid.NewGuid() : Guid.Empty);
            // Custom deserialization that is converted with the constructor. 
            settings    = obj["Settings"]?.ToObject<Dictionary<string, ModSettings.SavedSettings>>() ?? settings;
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
