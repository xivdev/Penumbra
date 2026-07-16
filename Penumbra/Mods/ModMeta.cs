using System.Text.Json;
using Luna;
using Penumbra.Files;

namespace Penumbra.Mods;

public readonly struct ModMeta(SaveService files, Mod mod) : ISavable
{
    public const uint CurrentFileVersion = 4;

    public string ToFilePath(FilenameService fileNames)
        => fileNames.ModMetaPath(mod);

    public void Save(Stream stream)
    {
        using var j = new Utf8JsonWriter(stream, JsonFunctions.WriterOptions);
        ModSerialization.UpdateModOnSave(files, j, mod);
    }
}
