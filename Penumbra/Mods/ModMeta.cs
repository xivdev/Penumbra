using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Services;

namespace Penumbra.Mods;

public readonly struct ModMeta(Mod mod) : ISavable
{
    public const uint FileVersion = 3;

    public string ToFilename(FilenameService fileNames)
        => fileNames.ModMetaPath(mod);

    public void Save(StreamWriter writer)
    {
        var jObject = new JObject
        {
            { nameof(FileVersion), JToken.FromObject(FileVersion) },
            { nameof(Mod.Name), JToken.FromObject(mod.Name) },
            { nameof(Mod.Author), JToken.FromObject(mod.Author) },
            { nameof(Mod.Description), JToken.FromObject(mod.Description) },
            { nameof(Mod.Image), JToken.FromObject(mod.Image) },
            { nameof(Mod.Version), JToken.FromObject(mod.Version) },
            { nameof(Mod.Website), JToken.FromObject(mod.Website) },
            { nameof(Mod.ModTags), JToken.FromObject(mod.ModTags) },
        };
        using var jWriter = new JsonTextWriter(writer);
        jWriter.Formatting = Formatting.Indented;
        jObject.WriteTo(jWriter);
    }
}
