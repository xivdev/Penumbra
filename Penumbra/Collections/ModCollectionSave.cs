using Newtonsoft.Json.Linq;
using Penumbra.Mods;
using Penumbra.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Penumbra.Mods.Manager;
using Penumbra.Util;

namespace Penumbra.Collections;

/// <summary>
/// Handle saving and loading a collection.
/// </summary>
internal readonly struct ModCollectionSave : ISavable
{
    private readonly ModStorage    _modStorage;
    private readonly ModCollection _modCollection;

    public ModCollectionSave(ModStorage modStorage, ModCollection modCollection)
    {
        _modStorage    = modStorage;
        _modCollection = modCollection;
    }

    public string ToFilename(FilenameService fileNames)
        => fileNames.CollectionFile(_modCollection);

    public string LogName(string _)
        => _modCollection.AnonymizedName;

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
        j.WritePropertyName(nameof(ModCollection.Name));
        j.WriteValue(_modCollection.Name);
        j.WritePropertyName(nameof(ModCollection.Settings));

        // Write all used and unused settings by mod directory name.
        j.WriteStartObject();
        for (var i = 0; i < _modCollection.Settings.Count; ++i)
        {
            var settings = _modCollection.Settings[i];
            if (settings != null)
            {
                j.WritePropertyName(_modStorage[i].ModPath.Name);
                x.Serialize(j, new ModSettings.SavedSettings(settings, _modStorage[i]));
            }
        }

        foreach (var (modDir, settings) in _modCollection.UnusedSettings)
        {
            j.WritePropertyName(modDir);
            x.Serialize(j, settings);
        }

        j.WriteEndObject();

        // Inherit by collection name.
        j.WritePropertyName("Inheritance");
        x.Serialize(j, _modCollection.DirectlyInheritsFrom.Select(c => c.Name));
        j.WriteEndObject();
    }

    public static bool LoadFromFile(FileInfo file, out string name, out int version, out Dictionary<string, ModSettings.SavedSettings> settings,
        out IReadOnlyList<string> inheritance)
    {
        settings    = new Dictionary<string, ModSettings.SavedSettings>();
        inheritance = Array.Empty<string>();
        if (!file.Exists)
        {
            Penumbra.Log.Error("Could not read collection because file does not exist.");
            name = string.Empty;

            version = 0;
            return false;
        }

        try
        {
            var obj = JObject.Parse(File.ReadAllText(file.FullName));
            name    = obj[nameof(ModCollection.Name)]?.ToObject<string>() ?? string.Empty;
            version = obj["Version"]?.ToObject<int>() ?? 0;
            // Custom deserialization that is converted with the constructor. 
            settings    = obj[nameof(ModCollection.Settings)]?.ToObject<Dictionary<string, ModSettings.SavedSettings>>() ?? settings;
            inheritance = obj["Inheritance"]?.ToObject<List<string>>() ?? inheritance;
            return true;
        }
        catch (Exception e)
        {
            name    = string.Empty;
            version = 0;
            Penumbra.Log.Error($"Could not read collection information from file:\n{e}");
            return false;
        }
    }
}
