using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.Mods;

public readonly struct ModLocalData : ISavable
{
    public const int FileVersion = 3;

    private readonly Mod _mod;

    public ModLocalData(Mod mod)
        => _mod = mod;

    public string ToFilename(FilenameService fileNames)
        => fileNames.LocalDataFile(_mod);

    public void Save(StreamWriter writer)
    {
        var jObject = new JObject
        {
            { nameof(FileVersion), JToken.FromObject(FileVersion) },
            { nameof(Mod.ImportDate), JToken.FromObject(_mod.ImportDate) },
            { nameof(Mod.LocalTags), JToken.FromObject(_mod.LocalTags) },
            { nameof(Mod.Note), JToken.FromObject(_mod.Note) },
            { nameof(Mod.Favorite), JToken.FromObject(_mod.Favorite) },
        };
        using var jWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
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
