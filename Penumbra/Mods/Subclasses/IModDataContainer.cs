using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Subclasses;

public interface IModDataContainer
{
    public Dictionary<Utf8GamePath, FullPath> Files         { get; set; }
    public Dictionary<Utf8GamePath, FullPath> FileSwaps     { get; set; }
    public HashSet<MetaManipulation>          Manipulations { get; set; }

    public void AddDataTo(Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
    {
        foreach (var (path, file) in Files)
            redirections.TryAdd(path, file);

        foreach (var (path, file) in FileSwaps)
            redirections.TryAdd(path, file);
        manipulations.UnionWith(Manipulations);
    }

    public static void Load(JToken json, IModDataContainer data, DirectoryInfo basePath)
    {
        data.Files.Clear();
        data.FileSwaps.Clear();
        data.Manipulations.Clear();

        var files = (JObject?)json[nameof(Files)];
        if (files != null)
            foreach (var property in files.Properties())
            {
                if (Utf8GamePath.FromString(property.Name, out var p, true))
                    data.Files.TryAdd(p, new FullPath(basePath, property.Value.ToObject<Utf8RelPath>()));
            }

        var swaps = (JObject?)json[nameof(FileSwaps)];
        if (swaps != null)
            foreach (var property in swaps.Properties())
            {
                if (Utf8GamePath.FromString(property.Name, out var p, true))
                    data.FileSwaps.TryAdd(p, new FullPath(property.Value.ToObject<string>()!));
            }

        var manips = json[nameof(Manipulations)];
        if (manips != null)
            foreach (var s in manips.Children().Select(c => c.ToObject<MetaManipulation>())
                         .Where(m => m.Validate()))
                data.Manipulations.Add(s);
    }

    public static void WriteModData(JsonWriter j, JsonSerializer serializer, IModDataContainer data, DirectoryInfo basePath)
    {
        j.WritePropertyName(nameof(data.Files));
        j.WriteStartObject();
        foreach (var (gamePath, file) in data.Files)
        {
            if (file.ToRelPath(basePath, out var relPath))
            {
                j.WritePropertyName(gamePath.ToString());
                j.WriteValue(relPath.ToString());
            }
        }

        j.WriteEndObject();
        j.WritePropertyName(nameof(data.FileSwaps));
        j.WriteStartObject();
        foreach (var (gamePath, file) in data.FileSwaps)
        {
            j.WritePropertyName(gamePath.ToString());
            j.WriteValue(file.ToString());
        }

        j.WriteEndObject();
        j.WritePropertyName(nameof(data.Manipulations));
        serializer.Serialize(j, data.Manipulations);
        j.WriteEndObject();
    }
}
