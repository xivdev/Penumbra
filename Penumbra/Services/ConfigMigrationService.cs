using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui.Filesystem;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.UI.Classes;
using SixLabors.ImageSharp;

namespace Penumbra.Services;

/// <summary>
/// Contains everything to migrate from older versions of the config to the current,
/// including deprecated fields.
/// </summary>
public class ConfigMigrationService
{
    private readonly FilenameService _fileNames;
    private readonly DalamudPluginInterface _pluginInterface;

    private Configuration _config = null!;
    private JObject _data = null!;

    public string CurrentCollection = ModCollection.DefaultCollection;
    public string DefaultCollection = ModCollection.DefaultCollection;
    public string ForcedCollection = string.Empty;
    public Dictionary<string, string> CharacterCollections = new();
    public Dictionary<string, string> ModSortOrder = new();
    public bool InvertModListOrder;
    public bool SortFoldersFirst;
    public SortModeV3 SortMode = SortModeV3.FoldersFirst;

    public ConfigMigrationService(FilenameService fileNames, DalamudPluginInterface pi)
    {
        _fileNames = fileNames;
        _pluginInterface = pi;
    }

    /// <summary> Add missing colors to the dictionary if necessary. </summary>
    private static void AddColors(Configuration config, bool forceSave)
    {
        var save = false;
        foreach (var color in Enum.GetValues<ColorId>())
        {
            save |= config.Colors.TryAdd(color, color.Data().DefaultColor);
        }

        if (save || forceSave)
        {
            config.Save();
        }
    }

    public void Migrate(Configuration config)
    {
        _config = config;
        // Do this on every migration from now on for a while
        // because it stayed alive for a bunch of people for some reason.
        DeleteMetaTmp();

        if (config.Version >= Configuration.Constants.CurrentVersion || !File.Exists(_fileNames.ConfigFile))
        {
            AddColors(config, false);
            return;
        }

        _data = JObject.Parse(File.ReadAllText(_fileNames.ConfigFile));
        CreateBackup();

        Version0To1();
        Version1To2();
        Version2To3();
        Version3To4();
        Version4To5();
        Version5To6();
        Version6To7();
        AddColors(config, true);
    }

    // Gendered special collections were added.
    private void Version6To7()
    {
        if (_config.Version != 6)
            return;

        CollectionManager.MigrateUngenderedCollections(_fileNames);
        _config.Version = 7;
    }


    // A new tutorial step was inserted in the middle.
    // The UI collection and a new tutorial for it was added.
    // The migration for the UI collection itself happens in the ActiveCollections file.
    private void Version5To6()
    {
        if (_config.Version != 5)
            return;

        if (_config.TutorialStep == 25)
            _config.TutorialStep = 27;

        _config.Version = 6;
    }

    // Mod backup extension was changed from .zip to .pmp.
    // Actual migration takes place in ModManager.
    private void Version4To5()
    {
        if (_config.Version != 4)
            return;

        ModManager.MigrateModBackups = true;
        _config.Version = 5;
    }

    // SortMode was changed from an enum to a type.
    private void Version3To4()
    {
        if (_config.Version != 3)
            return;

        SortMode = _data[nameof(SortMode)]?.ToObject<SortModeV3>() ?? SortMode;
        _config.SortMode = SortMode switch
        {
            SortModeV3.FoldersFirst => ISortMode<Mod>.FoldersFirst,
            SortModeV3.Lexicographical => ISortMode<Mod>.Lexicographical,
            SortModeV3.InverseFoldersFirst => ISortMode<Mod>.InverseFoldersFirst,
            SortModeV3.InverseLexicographical => ISortMode<Mod>.InverseLexicographical,
            SortModeV3.FoldersLast => ISortMode<Mod>.FoldersLast,
            SortModeV3.InverseFoldersLast => ISortMode<Mod>.InverseFoldersLast,
            SortModeV3.InternalOrder => ISortMode<Mod>.InternalOrder,
            SortModeV3.InternalOrderInverse => ISortMode<Mod>.InverseInternalOrder,
            _ => ISortMode<Mod>.FoldersFirst,
        };
        _config.Version = 4;
    }

    // SortFoldersFirst was changed from a bool to the enum SortMode.
    private void Version2To3()
    {
        if (_config.Version != 2)
            return;

        SortFoldersFirst = _data[nameof(SortFoldersFirst)]?.ToObject<bool>() ?? false;
        SortMode = SortFoldersFirst ? SortModeV3.FoldersFirst : SortModeV3.Lexicographical;
        _config.Version = 3;
    }

    // The forced collection was removed due to general inheritance.
    // Sort Order was moved to a separate file and may contain empty folders.
    // Active collections in general were moved to their own file.
    // Delete the penumbrametatmp folder if it exists.
    private void Version1To2()
    {
        if (_config.Version != 1)
            return;

        // Ensure the right meta files are loaded.
        DeleteMetaTmp();
        Penumbra.CharacterUtility.LoadCharacterResources();
        ResettleSortOrder();
        ResettleCollectionSettings();
        ResettleForcedCollection();
        _config.Version = 2;
    }

    private void DeleteMetaTmp()
    {
        var path = Path.Combine(_config.ModDirectory, "penumbrametatmp");
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not delete the outdated penumbrametatmp folder:\n{e}");
        }
    }

    private void ResettleForcedCollection()
    {
        ForcedCollection = _data[nameof(ForcedCollection)]?.ToObject<string>() ?? ForcedCollection;
        if (ForcedCollection.Length <= 0)
            return;

        // Add the previous forced collection to all current collections except itself as an inheritance.
        foreach (var collection in _fileNames.CollectionFiles)
        {
            try
            {
                var jObject = JObject.Parse(File.ReadAllText(collection.FullName));
                if (jObject[nameof(ModCollection.Name)]?.ToObject<string>() == ForcedCollection)
                    continue;

                jObject[nameof(ModCollection.Inheritance)] = JToken.FromObject(new List<string> { ForcedCollection });
                File.WriteAllText(collection.FullName, jObject.ToString());
            }
            catch (Exception e)
            {
                Penumbra.Log.Error(
                    $"Could not transfer forced collection {ForcedCollection} to inheritance of collection {collection}:\n{e}");
            }
        }
    }

    // Move the current sort order to its own file.
    private void ResettleSortOrder()
    {
        ModSortOrder = _data[nameof(ModSortOrder)]?.ToObject<Dictionary<string, string>>() ?? ModSortOrder;
        var file = _fileNames.FilesystemFile;
        using var stream = File.Open(file, File.Exists(file) ? FileMode.Truncate : FileMode.CreateNew);
        using var writer = new StreamWriter(stream);
        using var j = new JsonTextWriter(writer);
        j.Formatting = Formatting.Indented;
        j.WriteStartObject();
        j.WritePropertyName("Data");
        j.WriteStartObject();
        foreach (var (mod, path) in ModSortOrder.Where(kvp => Directory.Exists(Path.Combine(_config.ModDirectory, kvp.Key))))
        {
            j.WritePropertyName(mod, true);
            j.WriteValue(path);
        }

        j.WriteEndObject();
        j.WritePropertyName("EmptyFolders");
        j.WriteStartArray();
        j.WriteEndArray();
        j.WriteEndObject();
    }

    // Move the active collections to their own file.
    private void ResettleCollectionSettings()
    {
        CurrentCollection = _data[nameof(CurrentCollection)]?.ToObject<string>() ?? CurrentCollection;
        DefaultCollection = _data[nameof(DefaultCollection)]?.ToObject<string>() ?? DefaultCollection;
        CharacterCollections = _data[nameof(CharacterCollections)]?.ToObject<Dictionary<string, string>>() ?? CharacterCollections;
        SaveActiveCollectionsV0(DefaultCollection, CurrentCollection, DefaultCollection,
            CharacterCollections.Select(kvp => (kvp.Key, kvp.Value)), Array.Empty<(CollectionType, string)>());
    }

    // Outdated saving using the Characters list.
    private void SaveActiveCollectionsV0(string def, string ui, string current, IEnumerable<(string, string)> characters,
        IEnumerable<(CollectionType, string)> special)
    {
        var file = _fileNames.ActiveCollectionsFile;
        try
        {
            using var stream = File.Open(file, File.Exists(file) ? FileMode.Truncate : FileMode.CreateNew);
            using var writer = new StreamWriter(stream);
            using var j = new JsonTextWriter(writer);
            j.Formatting = Formatting.Indented;
            j.WriteStartObject();
            j.WritePropertyName(nameof(CollectionManager.Default));
            j.WriteValue(def);
            j.WritePropertyName(nameof(CollectionManager.Interface));
            j.WriteValue(ui);
            j.WritePropertyName(nameof(CollectionManager.Current));
            j.WriteValue(current);
            foreach (var (type, collection) in special)
            {
                j.WritePropertyName(type.ToString());
                j.WriteValue(collection);
            }

            j.WritePropertyName("Characters");
            j.WriteStartObject();
            foreach (var (character, collection) in characters)
            {
                j.WritePropertyName(character, true);
                j.WriteValue(collection);
            }

            j.WriteEndObject();
            j.WriteEndObject();
            Penumbra.Log.Verbose("Active Collections saved.");
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not save active collections to file {file}:\n{e}");
        }
    }

    // Collections were introduced and the previous CurrentCollection got put into ModDirectory.
    private void Version0To1()
    {
        if (_config.Version != 0)
            return;

        _config.ModDirectory = _data[nameof(CurrentCollection)]?.ToObject<string>() ?? string.Empty;
        _config.Version = 1;
        ResettleCollectionJson();
    }

    // Move the previous mod configurations to a new default collection file.
    private void ResettleCollectionJson()
    {
        var collectionJson = new FileInfo(Path.Combine(_config.ModDirectory, "collection.json"));
        if (!collectionJson.Exists)
            return;

        var defaultCollection = ModCollection.CreateNewEmpty(ModCollection.DefaultCollection);
        var defaultCollectionFile = new FileInfo(_fileNames.CollectionFile(defaultCollection));
        if (defaultCollectionFile.Exists)
            return;

        try
        {
            var text = File.ReadAllText(collectionJson.FullName);
            var data = JArray.Parse(text);

            var maxPriority = 0;
            var dict = new Dictionary<string, ModSettings.SavedSettings>();
            foreach (var setting in data.Cast<JObject>())
            {
                var modName = (string)setting["FolderName"]!;
                var enabled = (bool)setting["Enabled"]!;
                var priority = (int)setting["Priority"]!;
                var settings = setting["Settings"]!.ToObject<Dictionary<string, long>>()
                 ?? setting["Conf"]!.ToObject<Dictionary<string, long>>();

                dict[modName] = new ModSettings.SavedSettings()
                {
                    Enabled = enabled,
                    Priority = priority,
                    Settings = settings!,
                };
                maxPriority = Math.Max(maxPriority, priority);
            }

            InvertModListOrder = _data[nameof(InvertModListOrder)]?.ToObject<bool>() ?? InvertModListOrder;
            if (!InvertModListOrder)
                dict = dict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value with { Priority = maxPriority - kvp.Value.Priority });

            defaultCollection = ModCollection.MigrateFromV0(ModCollection.DefaultCollection, dict);
            Penumbra.SaveService.ImmediateSave(defaultCollection);
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not migrate the old collection file to new collection files:\n{e}");
            throw;
        }
    }

    // Create a backup of the configuration file specifically.
    private void CreateBackup()
    {
        var name = _fileNames.ConfigFile;
        var bakName = name + ".bak";
        try
        {
            File.Copy(name, bakName, true);
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not create backup copy of config at {bakName}:\n{e}");
        }
    }

    public enum SortModeV3 : byte
    {
        FoldersFirst = 0x00,
        Lexicographical = 0x01,
        InverseFoldersFirst = 0x02,
        InverseLexicographical = 0x03,
        FoldersLast = 0x04,
        InverseFoldersLast = 0x05,
        InternalOrder = 0x06,
        InternalOrderInverse = 0x07,
    }
}
