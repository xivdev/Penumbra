using Newtonsoft.Json;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Subclasses;

public interface ISubMod
{
    public string Name        { get; }
    public string FullName    { get; }
    public string Description { get; }

    public IReadOnlyDictionary<Utf8GamePath, FullPath> Files         { get; }
    public IReadOnlyDictionary<Utf8GamePath, FullPath> FileSwaps     { get; }
    public IReadOnlySet<MetaManipulation>              Manipulations { get; }

    public void AddData(Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
    {
        foreach (var (path, file) in Files)
            redirections.TryAdd(path, file);

        foreach (var (path, file) in FileSwaps)
            redirections.TryAdd(path, file);
        manipulations.UnionWith(Manipulations);
    }

    public bool IsDefault { get; }

    public static void WriteSubMod(JsonWriter j, JsonSerializer serializer, ISubMod mod, DirectoryInfo basePath, ModPriority? priority)
    {
        j.WriteStartObject();
        j.WritePropertyName(nameof(Name));
        j.WriteValue(mod.Name);
        j.WritePropertyName(nameof(Description));
        j.WriteValue(mod.Description);
        if (priority != null)
        {
            j.WritePropertyName(nameof(IModGroup.Priority));
            j.WriteValue(priority.Value.Value);
        }

        j.WritePropertyName(nameof(mod.Files));
        j.WriteStartObject();
        foreach (var (gamePath, file) in mod.Files)
        {
            if (file.ToRelPath(basePath, out var relPath))
            {
                j.WritePropertyName(gamePath.ToString());
                j.WriteValue(relPath.ToString());
            }
        }

        j.WriteEndObject();
        j.WritePropertyName(nameof(mod.FileSwaps));
        j.WriteStartObject();
        foreach (var (gamePath, file) in mod.FileSwaps)
        {
            j.WritePropertyName(gamePath.ToString());
            j.WriteValue(file.ToString());
        }

        j.WriteEndObject();
        j.WritePropertyName(nameof(mod.Manipulations));
        serializer.Serialize(j, mod.Manipulations);
        j.WriteEndObject();
    }
}
