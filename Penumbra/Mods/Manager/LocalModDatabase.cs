using System.Text.Json;
using LiteDB;
using Luna;
using Penumbra.GameData.Structs;
using Penumbra.Services;

namespace Penumbra.Mods.Manager;

public sealed class LocalModDatabase(FilenameService filenames) : IDisposable, IReadOnlyCollection<string>, IService
{
    private LiteDatabase?          _database;
    private ILiteCollection<Data>? _collection;

    public void Open()
        => Check();

    [MemberNotNull(nameof(_collection))]
    private ILiteCollection<Data> Check()
    {
        if (_collection is { } collection)
            return collection;

        _database   = new LiteDatabase(filenames.LocalModDatabase);
        _collection = _database.GetCollection<Data>("LocalModData");
        _collection.EnsureIndex(x => x.Id, true);
        return _collection;
    }

    public void Close()
    {
        _database?.Dispose();
        _database   = null;
        _collection = null;
    }

    public void Migrate()
    {
        if (!Directory.Exists(filenames.OldLocalDataDirectory))
            return;

        Check();
        foreach (var file in Directory.GetFiles(filenames.OldLocalDataDirectory, "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            try
            {
                var data   = JsonFunctions.ReadUtf8Bytes(file);
                var reader = new Utf8JsonReader(data, JsonFunctions.ReaderOptions);

                var modData = new Data(id);

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

                _collection!.Upsert(modData);
                Penumbra.Log.Debug($"Migrated local mod data for {id} to database.");
            }
            catch (Exception ex)
            {
                Penumbra.Log.Error($"Could not load local mod data for {id}:\n{ex}");
            }
        }

        try
        {
            Directory.Delete(filenames.OldLocalDataDirectory, true);
            Penumbra.Log.Information($"Deleted old local mod data directory at {filenames.OldLocalDataDirectory}.");
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Failed to delete old local mod data directory at {filenames.OldLocalDataDirectory}:\n{ex}");
        }
    }

    private sealed class Data(string id)
    {
        [BsonId]
        public string Id { get; private set; } = id;

        public long            ImportDate     = DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds();
        public long            LastConfigEdit = DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds();
        public bool            Favorite;
        public string          Note                  = string.Empty;
        public HashSet<string> LocalTags             = [];
        public HashSet<ulong>  PreferredChangedItems = [];
        public string          Folder                = string.Empty;
        public string?         SortOrderName;

        public Data(Mod mod)
            : this(mod.Identifier)
        {
            Update(mod);
        }

        public Data(Data old, string newId)
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

        public Data Update(Mod mod)
        {
            if (!string.Equals(mod.Identifier, Id, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Updating mod database data for {Id} with {mod.Identifier}.");

            ImportDate            = mod.ImportDate;
            LastConfigEdit        = mod.LastConfigEdit;
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

    public void Delete(string id)
        => Check().Delete(id);

    public void Upsert(Mod mod)
    {
        var data = Check().FindById(mod.Identifier)?.Update(mod) ?? new Data(mod);
        _collection.Upsert(data);
    }

    public void Move(string oldId, string newId)
    {
        if (Check().FindById(oldId) is not { } data)
            return;

        _collection.Delete(oldId);
        _collection.Upsert(new Data(data, newId));
    }

    public void DeleteMany(IReadOnlySet<string> ids)
        => Check().DeleteMany(e => ids.Contains(e.Id));

    public void UpsertImportDate(Mod mod)
    {
        if (GetOrCreateData(mod, out var data))
            return;

        data.ImportDate = mod.ImportDate;
        _collection.Upsert(data);
    }

    public void UpsertLastConfigEdit(Mod mod)
    {
        if (GetOrCreateData(mod, out var data))
            return;

        data.LastConfigEdit = mod.LastConfigEdit;
        _collection.Upsert(data);
    }

    public void UpsertFavorite(Mod mod)
    {
        if (GetOrCreateData(mod, out var data))
            return;

        data.Favorite = mod.Favorite;
        _collection.Upsert(data);
    }

    public void UpsertNote(Mod mod)
    {
        if (GetOrCreateData(mod, out var data))
            return;

        data.Note = mod.Note;
        _collection.Upsert(data);
    }

    public void UpsertPath(Mod mod)
    {
        if (GetOrCreateData(mod, out var data))
            return;

        data.Folder        = mod.Path.Folder;
        data.SortOrderName = mod.Path.SortName;
        _collection.Upsert(data);
    }

    public void UpsertChangedItems(Mod mod)
    {
        if (GetOrCreateData(mod, out var data))
            return;

        data.PreferredChangedItems = mod.PreferredChangedItems.Select(i => i.Id).ToHashSet();
        _collection.Upsert(data);
    }

    public void UpsertTags(Mod mod)
    {
        if (GetOrCreateData(mod, out var data))
            return;

        data.LocalTags = mod.LocalTags.ToHashSet();
        _collection.Upsert(data);
    }

    [MemberNotNull(nameof(_collection))]
    private bool GetOrCreateData(Mod mod, out Data data)
    {
        if (Check().FindById(mod.Identifier) is { } d)
        {
            data = d;
            return false;
        }

        data = new Data(mod);
        return true;
    }

    public ModDataChangeType AddData(Mod mod)
    {
        if (Check().FindById(mod.Identifier) is { } data)
            return data.ApplyToMod(mod);

        _collection.Upsert(new Data(mod));
        return ModDataChangeType.None;
    }

    public void AddData(ModStorage mods)
    {
        foreach (var mod in mods)
            AddData(mod);
    }

    public void Dispose()
        => Close();

    public IEnumerator<string> GetEnumerator()
        => Check().FindAll().Select(c => c.Id).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => Check().Count();
}
