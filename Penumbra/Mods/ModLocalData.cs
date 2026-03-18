using System.Text.Json;
using Luna;
using Penumbra.GameData.Structs;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.Mods;

public readonly struct ModLocalData(Mod mod) : ISavable
{
    public const int FileVersion = 3;

    public string ToFilePath(FilenameService fileNames)
        => fileNames.LocalDataFile(mod);

    public void Save(Stream stream)
    {
        using var j = new Utf8JsonWriter(stream, JsonFunctions.WriterOptions);
        j.WriteStartObject();

        j.WriteNumber("FileVersion"u8,    FileVersion);
        j.WriteNumber("ImportDate"u8,     mod.ImportDate);
        j.WriteNumber("LastConfigEdit"u8, mod.LastConfigEdit);
        j.WriteBoolean("Favorite"u8, mod.Favorite);
        if (mod.Note.Length > 0)
            j.WriteString("Note"u8, mod.Note);

        if (mod.LocalTags.Count > 0)
        {
            j.WritePropertyName("LocalTags"u8);
            j.WriteStartArray();
            foreach (var tag in mod.LocalTags)
                j.WriteStringValue(tag);
            j.WriteEndArray();
        }

        if (mod.PreferredChangedItems.Count > 0)
        {
            j.WritePropertyName("PreferredChangedItems"u8);
            j.WriteStartArray();
            foreach (var item in mod.PreferredChangedItems)
                j.WriteNumberValue(item);
            j.WriteEndArray();
        }

        if (mod.Path.Folder.Length > 0)
            j.WriteString("FileSystemFolder"u8, mod.Path.Folder);

        if (mod.Path.SortName is not null)
            j.WriteString("SortOrderName"u8, mod.Path.SortName);

        j.WriteEndObject();
        j.Flush();
    }

    public static ModDataChangeType Load(ModDataEditor editor, Mod mod)
    {
        var dataFile = editor.SaveService.FileNames.LocalDataFile(mod);

        var                   now                   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var                   importDate            = now;
        var                   localTags             = Enumerable.Empty<string>();
        HashSet<CustomItemId> preferredChangedItems = [];
        var                   favorite              = false;
        var                   note                  = string.Empty;
        var                   fileSystemFolder      = string.Empty;
        string?               sortOrderName         = null;
        var                   lastConfigEdit        = now;


        var save = true;
        if (File.Exists(dataFile))
            try
            {
                var data   = JsonFunctions.ReadUtf8Bytes(dataFile);
                var reader = new Utf8JsonReader(data, JsonFunctions.ReaderOptions);

                while (reader.Read())
                {
                    if (reader.TokenType is not JsonTokenType.PropertyName)
                        continue;

                    if (reader.ValueTextEquals("ImportDate"u8))
                    {
                        reader.Read();
                        importDate = reader.GetInt64();
                    }
                    else if (reader.ValueTextEquals("LastConfigEdit"u8))
                    {
                        reader.Read();
                        lastConfigEdit = reader.GetInt64();
                    }
                    else if (reader.ValueTextEquals("Favorite"u8))
                    {
                        reader.Read();
                        favorite = reader.GetBoolean();
                    }
                    else if (reader.ValueTextEquals("Note"u8))
                    {
                        reader.Read();
                        note = reader.GetString() ?? string.Empty;
                    }
                    else if (reader.ValueTextEquals("LocalTags"u8))
                    {
                        reader.Read();
                        if (reader.TokenType is not JsonTokenType.StartArray)
                            continue;

                        var tags = new HashSet<string>();
                        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray && reader.GetString() is { Length: > 0 } tag)
                            tags.Add(tag);
                        localTags = tags;
                    }
                    else if (reader.ValueTextEquals("PreferredItems"u8))
                    {
                        reader.Read();
                        if (reader.TokenType is not JsonTokenType.StartArray)
                            continue;

                        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
                            preferredChangedItems.Add(reader.GetUInt64());
                    }
                    else if (reader.ValueTextEquals("FileSystemFolder"u8))
                    {
                        reader.Read();
                        fileSystemFolder = reader.GetString() ?? string.Empty;
                    }
                    else if (reader.ValueTextEquals("SortOrderName"u8))
                    {
                        reader.Read();
                        sortOrderName = reader.GetString()?.FixName();
                    }
                }

                save = false;
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not load local mod data:\n{e}");
            }
        else
            preferredChangedItems = mod.DefaultPreferredItems;

        if (importDate is 0)
            importDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (lastConfigEdit == now)
            save = true;

        ModDataChangeType changes = 0;
        if (mod.ImportDate != importDate)
        {
            mod.ImportDate =  importDate;
            changes        |= ModDataChangeType.ImportDate;
        }

        if (mod.LastConfigEdit != lastConfigEdit)
        {
            mod.LastConfigEdit =  lastConfigEdit;
            changes            |= ModDataChangeType.LastConfigEdit;
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
        if (newModTags is null && newLocalTags is null)
            return 0;

        ModDataChangeType type = 0;
        if (newModTags is not null)
        {
            var modTags = newModTags.Where(t => t.Length > 0).Distinct().ToArray();
            if (!modTags.SequenceEqual(mod.ModTags))
            {
                newLocalTags ??= mod.LocalTags;
                mod.ModTags  =   modTags;
                type         |=  ModDataChangeType.ModTags;
            }
        }

        if (newLocalTags is not null)
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
