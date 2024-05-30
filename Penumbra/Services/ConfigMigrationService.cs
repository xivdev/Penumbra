using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui.Filesystem;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Enums;
using Penumbra.Interop.Services;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.UI;
using Penumbra.UI.Classes;
using Penumbra.UI.ResourceWatcher;
using Penumbra.UI.Tabs;

namespace Penumbra.Services;

/// <summary>
/// Contains everything to migrate from older versions of the config to the current,
/// including deprecated fields.
/// </summary>
public class ConfigMigrationService(SaveService saveService, BackupService backupService) : IService
{
    private Configuration _config = null!;
    private JObject       _data   = null!;

    public string                     CurrentCollection    = ModCollection.DefaultCollectionName;
    public string                     DefaultCollection    = ModCollection.DefaultCollectionName;
    public string                     ForcedCollection     = string.Empty;
    public Dictionary<string, string> CharacterCollections = [];
    public Dictionary<string, string> ModSortOrder         = [];
    public bool                       InvertModListOrder;
    public bool                       SortFoldersFirst;
    public SortModeV3                 SortMode = SortModeV3.FoldersFirst;

    /// <summary> Add missing colors to the dictionary if necessary. </summary>
    private static void AddColors(Configuration config, bool forceSave)
    {
        var save = false;
        foreach (var color in Enum.GetValues<ColorId>())
            save |= config.Colors.TryAdd(color, color.Data().DefaultColor);

        if (save || forceSave)
            config.Save();

        Colors.SetColors(config);
    }

    public void Migrate(CharacterUtility utility, Configuration config)
    {
        _config = config;
        // Do this on every migration from now on for a while
        // because it stayed alive for a bunch of people for some reason.
        DeleteMetaTmp();

        if (config.Version >= Configuration.Constants.CurrentVersion || !File.Exists(saveService.FileNames.ConfigFile))
        {
            AddColors(config, false);
            return;
        }

        _data = JObject.Parse(File.ReadAllText(saveService.FileNames.ConfigFile));
        CreateBackup();

        Version0To1();
        Version1To2(utility);
        Version2To3();
        Version3To4();
        Version4To5();
        Version5To6();
        Version6To7();
        Version7To8();
        Version8To9();
        AddColors(config, true);
    }

    // Migrate to ephemeral config.
    private void Version8To9()
    {
        if (_config.Version != 8)
            return;

        backupService.CreateMigrationBackup("pre_collection_identifiers");
        _config.Version           = 9;
        _config.Ephemeral.Version = 9;
        _config.Save();
        _config.Ephemeral.Save();
    }

    // Migrate to ephemeral config.
    private void Version7To8()
    {
        if (_config.Version != 7)
            return;

        _config.Version           = 8;
        _config.Ephemeral.Version = 8;

        _config.Ephemeral.LastSeenVersion       = _data["LastSeenVersion"]?.ToObject<int>() ?? _config.Ephemeral.LastSeenVersion;
        _config.Ephemeral.DebugSeparateWindow   = _data["DebugSeparateWindow"]?.ToObject<bool>() ?? _config.Ephemeral.DebugSeparateWindow;
        _config.Ephemeral.TutorialStep          = _data["TutorialStep"]?.ToObject<int>() ?? _config.Ephemeral.TutorialStep;
        _config.Ephemeral.EnableResourceLogging = _data["EnableResourceLogging"]?.ToObject<bool>() ?? _config.Ephemeral.EnableResourceLogging;
        _config.Ephemeral.ResourceLoggingFilter = _data["ResourceLoggingFilter"]?.ToObject<string>() ?? _config.Ephemeral.ResourceLoggingFilter;
        _config.Ephemeral.EnableResourceWatcher = _data["EnableResourceWatcher"]?.ToObject<bool>() ?? _config.Ephemeral.EnableResourceWatcher;
        _config.Ephemeral.OnlyAddMatchingResources =
            _data["OnlyAddMatchingResources"]?.ToObject<bool>() ?? _config.Ephemeral.OnlyAddMatchingResources;
        _config.Ephemeral.ResourceWatcherResourceTypes = _data["ResourceWatcherResourceTypes"]?.ToObject<ResourceTypeFlag>()
         ?? _config.Ephemeral.ResourceWatcherResourceTypes;
        _config.Ephemeral.ResourceWatcherResourceCategories = _data["ResourceWatcherResourceCategories"]?.ToObject<ResourceCategoryFlag>()
         ?? _config.Ephemeral.ResourceWatcherResourceCategories;
        _config.Ephemeral.ResourceWatcherRecordTypes =
            _data["ResourceWatcherRecordTypes"]?.ToObject<RecordType>() ?? _config.Ephemeral.ResourceWatcherRecordTypes;
        _config.Ephemeral.CollectionPanel = _data["CollectionPanel"]?.ToObject<CollectionsTab.PanelMode>() ?? _config.Ephemeral.CollectionPanel;
        _config.Ephemeral.SelectedTab     = _data["SelectedTab"]?.ToObject<TabType>() ?? _config.Ephemeral.SelectedTab;
        _config.Ephemeral.ChangedItemFilter = _data["ChangedItemFilter"]?.ToObject<ChangedItemDrawer.ChangedItemIcon>()
         ?? _config.Ephemeral.ChangedItemFilter;
        _config.Ephemeral.FixMainWindow = _data["FixMainWindow"]?.ToObject<bool>() ?? _config.Ephemeral.FixMainWindow;
        _config.Ephemeral.Save();
    }

    // Gendered special collections were added.
    private void Version6To7()
    {
        if (_config.Version != 6)
            return;

        ActiveCollectionMigration.MigrateUngenderedCollections(saveService.FileNames);
        _config.Version = 7;
    }


    // A new tutorial step was inserted in the middle.
    // The UI collection and a new tutorial for it was added.
    // The migration for the UI collection itself happens in the ActiveCollections file.
    private void Version5To6()
    {
        if (_config.Version != 5)
            return;

        if (_config.Ephemeral.TutorialStep == 25)
            _config.Ephemeral.TutorialStep = 27;

        _config.Version = 6;
    }

    // Mod backup extension was changed from .zip to .pmp.
    // Actual migration takes place in ModManager.
    private void Version4To5()
    {
        if (_config.Version != 4)
            return;

        ModBackup.MigrateModBackups = true;
        _config.Version             = 5;
    }

    // SortMode was changed from an enum to a type.
    private void Version3To4()
    {
        if (_config.Version != 3)
            return;

        SortMode = _data[nameof(SortMode)]?.ToObject<SortModeV3>() ?? SortMode;
        _config.SortMode = SortMode switch
        {
            SortModeV3.FoldersFirst           => ISortMode<Mod>.FoldersFirst,
            SortModeV3.Lexicographical        => ISortMode<Mod>.Lexicographical,
            SortModeV3.InverseFoldersFirst    => ISortMode<Mod>.InverseFoldersFirst,
            SortModeV3.InverseLexicographical => ISortMode<Mod>.InverseLexicographical,
            SortModeV3.FoldersLast            => ISortMode<Mod>.FoldersLast,
            SortModeV3.InverseFoldersLast     => ISortMode<Mod>.InverseFoldersLast,
            SortModeV3.InternalOrder          => ISortMode<Mod>.InternalOrder,
            SortModeV3.InternalOrderInverse   => ISortMode<Mod>.InverseInternalOrder,
            _                                 => ISortMode<Mod>.FoldersFirst,
        };
        _config.Version = 4;
    }

    // SortFoldersFirst was changed from a bool to the enum SortMode.
    private void Version2To3()
    {
        if (_config.Version != 2)
            return;

        SortFoldersFirst = _data[nameof(SortFoldersFirst)]?.ToObject<bool>() ?? false;
        SortMode         = SortFoldersFirst ? SortModeV3.FoldersFirst : SortModeV3.Lexicographical;
        _config.Version  = 3;
    }

    // The forced collection was removed due to general inheritance.
    // Sort Order was moved to a separate file and may contain empty folders.
    // Active collections in general were moved to their own file.
    // Delete the penumbrametatmp folder if it exists.
    private void Version1To2(CharacterUtility utility)
    {
        if (_config.Version != 1)
            return;

        // Ensure the right meta files are loaded.
        DeleteMetaTmp();
        if (utility.Ready)
            utility.LoadCharacterResources();
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
        foreach (var collection in saveService.FileNames.CollectionFiles)
        {
            try
            {
                var jObject = JObject.Parse(File.ReadAllText(collection.FullName));
                if (jObject["Name"]?.ToObject<string>() == ForcedCollection)
                    continue;

                jObject[nameof(ModCollection.DirectlyInheritsFrom)] = JToken.FromObject(new List<string> { ForcedCollection });
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
        var       file   = saveService.FileNames.FilesystemFile;
        using var stream = File.Open(file, File.Exists(file) ? FileMode.Truncate : FileMode.CreateNew);
        using var writer = new StreamWriter(stream);
        using var j      = new JsonTextWriter(writer);
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
        CurrentCollection    = _data[nameof(CurrentCollection)]?.ToObject<string>() ?? CurrentCollection;
        DefaultCollection    = _data[nameof(DefaultCollection)]?.ToObject<string>() ?? DefaultCollection;
        CharacterCollections = _data[nameof(CharacterCollections)]?.ToObject<Dictionary<string, string>>() ?? CharacterCollections;
        SaveActiveCollectionsV0(DefaultCollection,                    CurrentCollection, DefaultCollection,
            CharacterCollections.Select(kvp => (kvp.Key, kvp.Value)), Array.Empty<(CollectionType, string)>());
    }

    // Outdated saving using the Characters list.
    private void SaveActiveCollectionsV0(string def, string ui, string current, IEnumerable<(string, string)> characters,
        IEnumerable<(CollectionType, string)> special)
    {
        var file = saveService.FileNames.ActiveCollectionsFile;
        try
        {
            using var stream = File.Open(file, File.Exists(file) ? FileMode.Truncate : FileMode.CreateNew);
            using var writer = new StreamWriter(stream);
            using var j      = new JsonTextWriter(writer);
            j.Formatting = Formatting.Indented;
            j.WriteStartObject();
            j.WritePropertyName(nameof(ActiveCollectionData.Default));
            j.WriteValue(def);
            j.WritePropertyName(nameof(ActiveCollectionData.Interface));
            j.WriteValue(ui);
            j.WritePropertyName(nameof(ActiveCollectionData.Current));
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
        _config.Version      = 1;
        ResettleCollectionJson();
    }

    /// <summary> Move the previous mod configurations to a new default collection file. </summary>
    private void ResettleCollectionJson()
    {
        var collectionJson = new FileInfo(Path.Combine(_config.ModDirectory, "collection.json"));
        if (!collectionJson.Exists)
            return;

        var defaultCollectionFile = new FileInfo(saveService.FileNames.CollectionFile(ModCollection.DefaultCollectionName));
        if (defaultCollectionFile.Exists)
            return;

        try
        {
            var text = File.ReadAllText(collectionJson.FullName);
            var data = JArray.Parse(text);

            var maxPriority = ModPriority.Default;
            var dict        = new Dictionary<string, ModSettings.SavedSettings>();
            foreach (var setting in data.Cast<JObject>())
            {
                var modName  = setting["FolderName"]?.ToObject<string>()!;
                var enabled  = setting["Enabled"]?.ToObject<bool>() ?? false;
                var priority = setting["Priority"]?.ToObject<ModPriority>() ?? ModPriority.Default;
                var settings = setting["Settings"]!.ToObject<Dictionary<string, Setting>>()
                 ?? setting["Conf"]!.ToObject<Dictionary<string, Setting>>();

                dict[modName] = new ModSettings.SavedSettings()
                {
                    Enabled  = enabled,
                    Priority = priority,
                    Settings = settings!,
                };
                maxPriority = maxPriority.Max(priority);
            }

            InvertModListOrder = _data[nameof(InvertModListOrder)]?.ToObject<bool>() ?? InvertModListOrder;
            if (!InvertModListOrder)
                dict = dict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value with { Priority = maxPriority - kvp.Value.Priority });

            var emptyStorage = new ModStorage();
            // Only used for saving and immediately discarded, so the local collection id here is irrelevant.
            var collection   = ModCollection.CreateFromData(saveService, emptyStorage, Guid.NewGuid(), ModCollection.DefaultCollectionName, LocalCollectionId.Zero, 0, 1, dict, []);
            saveService.ImmediateSaveSync(new ModCollectionSave(emptyStorage, collection));
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
        var name    = saveService.FileNames.ConfigFile;
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
        FoldersFirst           = 0x00,
        Lexicographical        = 0x01,
        InverseFoldersFirst    = 0x02,
        InverseLexicographical = 0x03,
        FoldersLast            = 0x04,
        InverseFoldersLast     = 0x05,
        InternalOrder          = 0x06,
        InternalOrderInverse   = 0x07,
    }
}
