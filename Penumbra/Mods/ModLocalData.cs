using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.Mods;

public readonly struct ModLocalData(Mod mod) : ISavable
{
    public const int FileVersion = 3;

    public string ToFilename(FilenameService fileNames)
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
        };
        using var jWriter = new JsonTextWriter(writer);
        jWriter.Formatting = Formatting.Indented;
        jObject.WriteTo(jWriter);
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
