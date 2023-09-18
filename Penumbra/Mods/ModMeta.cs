using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Services;

namespace Penumbra.Mods;

public readonly struct ModMeta : ISavable
{
    public const uint FileVersion = 3;

    private readonly Mod _mod;

    public ModMeta(Mod mod)
        => _mod = mod;

    public string ToFilename(FilenameService fileNames)
        => fileNames.ModMetaPath(_mod);

    public void Save(StreamWriter writer)
    {
        var jObject = new JObject
        {
            { nameof(FileVersion), JToken.FromObject(FileVersion) },
            { nameof(Mod.Name), JToken.FromObject(_mod.Name) },
            { nameof(Mod.Author), JToken.FromObject(_mod.Author) },
            { nameof(Mod.Description), JToken.FromObject(_mod.Description) },
            { nameof(Mod.Version), JToken.FromObject(_mod.Version) },
            { nameof(Mod.Website), JToken.FromObject(_mod.Website) },
            { nameof(Mod.ModTags), JToken.FromObject(_mod.ModTags) },
        };
        using var jWriter = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
        jObject.WriteTo(jWriter);
    }
}
