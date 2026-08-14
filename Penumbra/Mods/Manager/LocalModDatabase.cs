using System.Text.Json;
using LiteDB;
using Luna;
using Penumbra.Api.Preset;
using Penumbra.Files;
using Penumbra.GameData.Structs;

namespace Penumbra.Mods.Manager;

public sealed class LocalModDatabase(ServiceManager services) : IDisposable, IService
{
    private readonly Lock                         _lock = new();
    private          LiteDatabase?                _database;
    private          ILiteCollection<ModData>?    _collection;
    private          ILiteCollection<PresetData>? _presets;

    public string FilePath
        => field ??= services.GetService<FilenameService>().LocalModDatabase;

    [MemberNotNull(nameof(_presets), nameof(_collection))]
    private (ILiteCollection<ModData> Data, ILiteCollection<PresetData> Presets) Check([CallerMemberName] string callerName = "")
    {
        lock (_lock)
        {
            Log(callerName);
            if (_collection is { } collection && _presets is { } presets)
                return (collection, presets);

            _database = new LiteDatabase(
                $"Filename={FilePath};Connection=Shared;Timeout=00:00:02");
            _database.Mapper.EmptyStringToNull   = false;
            _database.Mapper.IncludeFields       = true;
            _database.Mapper.SerializeNullValues = false;
            _collection                          = _database.GetCollection<ModData>("LocalModData");
            _presets                             = _database.GetCollection<PresetData>("PresetData");
            _collection.EnsureIndex(x => x.Id, true);
            _presets.EnsureIndex(x => x.Id, true);
            _presets.EnsureIndex(x => x.Mod);
        }

        return (_collection, _presets);
    }

    public IBackupFile CreateBackupFile(string filePath)
        => new DatabaseBackup(this, filePath);

    public void Close()
    {
        lock (_lock)
        {
            _database?.Dispose();
            _database   = null;
            _collection = null;
            _presets    = null;
        }
    }

    public bool Delete(string id)
    {
        lock (_lock)
        {
            Check();
            return _presets.Delete(id) | _collection.Delete(id);
        }
    }

    public TransactionDisposable Transaction()
    {
        lock (_lock)
        {
            Check();
            return new TransactionDisposable(this);
        }
    }

    public void UpsertFullPreset(string modIdentifier, SettingPreset preset)
    {
        lock (_lock)
        {
            Check().Presets.Upsert(new PresetData(preset, modIdentifier));
        }
    }

    internal void UpsertPresetProperty(string modIdentifier, SettingPreset preset,
        System.Linq.Expressions.Expression<Func<PresetData, PresetData>> expression)
    {
        lock (_lock)
        {
            if (Check().Presets.UpdateMany(expression, p => p.Id == preset.Identifier) is not 1)
                _presets.Insert(new PresetData(preset, modIdentifier));
        }
    }

    internal void DeletePreset(Guid identifier)
    {
        lock (_lock)
        {
            Check().Presets.Delete(identifier);
        }
    }

    public void UpsertFullMod(Mod mod)
    {
        lock (_lock)
        {
            Check().Data.Upsert(new ModData(mod));
        }
    }

    internal void UpsertModProperty(Mod mod, System.Linq.Expressions.Expression<Func<ModData, ModData>> expression)
    {
        lock (_lock)
        {
            if (Check().Data.UpdateMany(expression, p => p.Id == mod.Identifier) is not 1)
                _collection.Insert(new ModData(mod));
        }
    }

    public void Move(string oldId, string newId)
    {
        lock (_lock)
        {
            Check();
            if (_collection.FindById(oldId) is { } data)
            {
                _collection.Delete(oldId);
                _collection.Upsert(new ModData(data, newId));
            }

            _presets.UpdateMany(_ => new PresetData { Mod = newId }, p => p.Mod == oldId);
        }
    }

    public ModDataChangeType AddData(Mod mod)
    {
        lock (_lock)
        {
            var ret = ModDataChangeType.None;
            Check();
            try
            {
                if (_collection.FindById(mod.Identifier) is { } data)
                {
                    ret |= data.ApplyToMod(mod);
                }
                else
                {
                    _collection.Upsert(new ModData(mod));
                    Penumbra.Log.Debug($"Added new local mod data data for {mod.Identifier}.");
                }
            }
            catch (Exception ex)
            {
                Penumbra.Log.Debug($"Failure to read local mod data for {mod.Identifier}:\n{ex}");
            }

            try
            {
                mod.Presets.Clear();
                mod.Presets.AddRange(_presets.Find(m => m.Mod == mod.Identifier).Select(p => p.ToPreset()));
            }
            catch (Exception ex)
            {
                Penumbra.Log.Debug($"Failure to read mod setting presets for {mod.Identifier}:\n{ex}");
            }

            return ret;
        }
    }

    public List<SettingPreset> GetGenericPresets()
    {
        var list = new List<SettingPreset>();
        lock (_lock)
        {
            list.AddRange(Check().Presets.Find(m => m.Mod.Length == 0).Select(p => p.ToPreset()));
            return list;
        }
    }

    public void Dispose()
        => Close();

    public HashSet<string> GetIds()
    {
        lock (_lock)
        {
            var set = Check().Data.FindAll().Select(c => c.Id).ToHashSet();
            return set;
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return Check().Data.Count();
            }
        }
    }

    public ref struct TransactionDisposable(LocalModDatabase db)
    {
        private bool _enabled = db._database!.BeginTrans();

        public void Dispose()
        {
            if (!_enabled)
                return;

            lock (db._lock)
            {
                db.Log();
                _enabled = !db._database!.Commit();
            }
        }
    }

    public void Migrate()
    {
        var oldPath = services.GetService<FilenameService>().Migration.LocalDataDirectory;
        if (!Directory.Exists(oldPath))
            return;

        using (Transaction())
        {
            foreach (var file in Directory.GetFiles(oldPath, "*.json"))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var data   = JsonFunctions.ReadUtf8Bytes(file, out _);
                    var reader = new Utf8JsonReader(data.Span, JsonFunctions.ReaderOptions);

                    var modData = new ModData(id);

                    while (reader.Read())
                    {
                        if (reader.TokenType is not JsonTokenType.PropertyName)
                            continue;

                        if (reader.ValueTextEquals("ImportDate"u8))
                        {
                            reader.Read();

                            modData.ImportDate = reader.GetInt64();
                        }
                        else if (reader.ValueTextEquals("LastConfigEdit"u8))
                        {
                            reader.Read();
                            modData.LastConfigEdit = reader.GetInt64();
                        }
                        else if (reader.ValueTextEquals("Favorite"u8))
                        {
                            reader.Read();
                            modData.Favorite = reader.GetBoolean();
                        }
                        else if (reader.ValueTextEquals("Note"u8))
                        {
                            reader.Read();
                            modData.Note = reader.GetString() ?? string.Empty;
                        }
                        else if (reader.ValueTextEquals("LocalTags"u8))
                        {
                            reader.Read();
                            if (reader.TokenType is not JsonTokenType.StartArray)
                                continue;

                            while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray && reader.GetString() is { Length: > 0 } tag)
                                modData.LocalTags.Add(tag);
                        }
                        else if (reader.ValueTextEquals("PreferredItems"u8))
                        {
                            reader.Read();
                            if (reader.TokenType is not JsonTokenType.StartArray)
                                continue;

                            while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
                                modData.PreferredChangedItems.Add(reader.GetUInt64());
                        }
                        else if (reader.ValueTextEquals("FileSystemFolder"u8))
                        {
                            reader.Read();
                            modData.Folder = reader.GetString() ?? string.Empty;
                        }
                        else if (reader.ValueTextEquals("SortOrderName"u8))
                        {
                            reader.Read();
                            modData.SortOrderName = reader.GetString()?.FixName();
                        }
                    }

                    if (modData.LastConfigEdit < modData.ImportDate)
                        modData.LastConfigEdit = modData.ImportDate;

                    lock (_lock)
                    {
                        _collection!.Upsert(modData);
                        Log();
                    }

                    Penumbra.Log.Debug($"Migrated local mod data for {id} to database.");
                }
                catch (Exception ex)
                {
                    Penumbra.Log.Error($"Could not load local mod data for {id}:\n{ex}");
                }
            }
        }

        try
        {
            Directory.Delete(oldPath, true);
            Penumbra.Log.Information($"Deleted old local mod data directory at {oldPath}.");
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Failed to delete old local mod data directory at {oldPath}:\n{ex}");
        }
    }

    
    internal class PresetData()
    {
        [BsonId]
        public Guid Id = Guid.Empty;

        public string        Mod  = string.Empty;
        public string        Name = string.Empty;
        public long          LastEdit;
        public long          LastApplication;
        public int?          Priority;
        public short         Version;
        public ModState      State;
        public SettingData[] Settings = [];

        public readonly struct SettingData(in ModObjectIdentifier group, in GroupSettingData data)
        {
            public readonly ModObjectIdentifier Group = group;

            public readonly (ModObjectIdentifier Option, OptionState State)[] Options =
                data.Options.Select(kvp => (kvp.Key, (OptionState)kvp.Value)).ToArray();

            public readonly bool DisableAllUnknown = data.DisableAllUnknown;
        }

        public SettingPreset ToPreset()
        {
            var data = SettingPresetData.Create();
            data.Version                        = Version;
            (data._hasPriority, data._priority) = Priority.HasValue ? (true, Priority.Value) : (false, 0);
            data._state                         = (byte)State;
            foreach (var group in Settings)
            {
                var groupData = GroupSettingData.Create();
                groupData.DisableAllUnknown = group.DisableAllUnknown;
                foreach (var (option, state) in group.Options)
                    groupData.Options.TryAdd(option, (byte)state);
                data.Settings.TryAdd(group.Group, groupData);
            }

            var ret = new SettingPreset(Id, data)
            {
                Name            = Name,
                LastEdit        = DateTimeOffset.FromUnixTimeMilliseconds(LastEdit),
                LastApplication = DateTimeOffset.FromUnixTimeMilliseconds(LastApplication),
            };
            return ret;
        }

        public PresetData(SettingPreset preset, string mod = "")
            : this()
        {
            Id              = preset.Identifier;
            Version         = preset.Data.Version;
            Mod             = mod;
            LastEdit        = preset.LastEdit.ToUnixTimeMilliseconds();
            LastApplication = preset.LastApplication.ToUnixTimeMilliseconds();
            Name            = preset.Name;
            Priority        = preset.Data.Priority;
            State           = preset.Data.State;
            Settings        = preset.Data.Settings.Select(kvp => new SettingData(kvp.Key, kvp.Value)).ToArray();
        }
    }

    internal class ModData()
    {
        
        [BsonId]
        public string Id { get; private set; } = string.Empty;

        public long            ImportDate     = DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds();
        public long            LastConfigEdit = DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds();
        public bool            Favorite;
        public string          Note                  = string.Empty;
        public HashSet<string> LocalTags             = [];
        public HashSet<ulong>  PreferredChangedItems = [];
        public string          Folder                = string.Empty;
        public string?         SortOrderName;

        public ModData(string id)
            : this()
            => Id = id;

        public ModData(Mod mod)
            : this(mod.Identifier)
        {
            Update(mod);
        }

        public ModData(ModData old, string newId)
            : this(newId)
        {
            ImportDate            = old.ImportDate;
            LastConfigEdit        = old.LastConfigEdit;
            Favorite              = old.Favorite;
            Note                  = old.Note;
            LocalTags             = old.LocalTags.ToHashSet();
            PreferredChangedItems = old.PreferredChangedItems.ToHashSet();
            Folder                = old.Folder;
            SortOrderName         = old.SortOrderName;
        }

        public ModData Update(Mod mod)
        {
            if (!string.Equals(mod.Identifier, Id, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Updating mod database data for {Id} with {mod.Identifier}.");

            ImportDate            = mod.ImportDate     = mod.ImportDate > 0 ? mod.ImportDate : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            LastConfigEdit        = mod.LastConfigEdit = ImportDate > mod.LastConfigEdit ? ImportDate : mod.LastConfigEdit;
            Favorite              = mod.Favorite;
            Note                  = mod.Note;
            LocalTags             = mod.LocalTags.ToHashSet();
            PreferredChangedItems = mod.PreferredChangedItems.Select(i => i.Id).ToHashSet();
            Folder                = mod.Path.Folder;
            SortOrderName         = mod.Path.SortName;
            return this;
        }

        public ModDataChangeType ApplyToMod(Mod mod)
        {
            var changes = ModDataChangeType.None;
            if (mod.ImportDate != ImportDate)
            {
                mod.ImportDate =  ImportDate;
                changes        |= ModDataChangeType.ImportDate;
            }

            if (mod.LastConfigEdit != LastConfigEdit)
            {
                mod.LastConfigEdit =  LastConfigEdit;
                changes            |= ModDataChangeType.LastConfigEdit;
            }

            if (mod.Favorite != Favorite)
            {
                mod.Favorite =  Favorite;
                changes      |= ModDataChangeType.Favorite;
            }

            if (mod.Note != Note)
            {
                mod.Note =  Note;
                changes  |= ModDataChangeType.Note;
            }

            if (!mod.Path.Folder.Equals(Folder, StringComparison.OrdinalIgnoreCase))
            {
                mod.Path.Folder =  Folder;
                changes         |= ModDataChangeType.FileSystemFolder;
            }

            if (mod.Path.SortName != SortOrderName)
            {
                mod.Path.SortName =  SortOrderName;
                changes           |= ModDataChangeType.FileSystemSortOrder;
            }

            if (!mod.PreferredChangedItems.SetEquals(PreferredChangedItems.Select(i => new CustomItemId(i))))
            {
                mod.PreferredChangedItems =  PreferredChangedItems.Select(i => new CustomItemId(i)).ToHashSet();
                changes                   |= ModDataChangeType.PreferredChangedItems;
            }

            changes |= ModDataEditor.UpdateTags(mod, null, LocalTags);

            return changes;
        }
    }

    private sealed class DatabaseBackup(LocalModDatabase db, string filePath) : IBackupFile
    {
        public bool Exists
            => File.Exists(filePath);

        public string Path
            => filePath;

        public bool Equals(Stream other)
        {
            lock (db._lock)
            {
                using var currentData = File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return IBackupFile.Equals(currentData, other);
            }
        }

        public void CreateEntry(ZipArchive archive, string rootDirectory)
        {
            lock (db._lock)
            {
                archive.CreateEntryFromFile(filePath, System.IO.Path.GetRelativePath(rootDirectory, filePath), CompressionLevel.Optimal);
            }
        }
    }

    [Conditional("false")]
    private void Log([CallerMemberName] string callerName = "")
        => Penumbra.Log.Information(
            $"[{Environment.CurrentManagedThreadId}] {callerName} Lock: {_lock.IsHeldByCurrentThread}, DB: {RuntimeHelpers.GetHashCode(_database)}");
}
