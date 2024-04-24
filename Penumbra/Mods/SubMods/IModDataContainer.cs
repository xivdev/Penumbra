using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.String.Classes;

namespace Penumbra.Mods.SubMods;

public interface IModDataContainer
{
    public IMod Mod { get; }
    public IModGroup? Group { get; }

    public Dictionary<Utf8GamePath, FullPath> Files { get; set; }
    public Dictionary<Utf8GamePath, FullPath> FileSwaps { get; set; }
    public HashSet<MetaManipulation> Manipulations { get; set; }

    public void AddDataTo(Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
    {
        foreach (var (path, file) in Files)
            redirections.TryAdd(path, file);

        foreach (var (path, file) in FileSwaps)
            redirections.TryAdd(path, file);
        manipulations.UnionWith(Manipulations);
    }

    public string GetName()
        => this switch
        {
            IModOption o => o.FullName,
            DefaultSubMod => DefaultSubMod.FullName,
            _ => $"Container {GetDataIndices().DataIndex + 1}",
        };

    public string GetFullName()
        => this switch
        {
            IModOption o => o.FullName,
            DefaultSubMod => DefaultSubMod.FullName,
            _ when Group != null => $"{Group.Name}: Container {GetDataIndices().DataIndex + 1}",
            _ => $"Container {GetDataIndices().DataIndex + 1}",
        };

    public static void Clone(IModDataContainer from, IModDataContainer to)
    {
        to.Files = new Dictionary<Utf8GamePath, FullPath>(from.Files);
        to.FileSwaps = new Dictionary<Utf8GamePath, FullPath>(from.FileSwaps);
        to.Manipulations = [.. from.Manipulations];
    }

    public (int GroupIndex, int DataIndex) GetDataIndices();

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

    internal static void DeleteDeleteList(IEnumerable<string> deleteList, bool delete)
    {
        if (!delete)
            return;

        foreach (var file in deleteList)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e)
            {
                Penumbra.Log.Error($"Could not delete incorporated meta file {file}:\n{e}");
            }
        }
    }
}

public interface IModDataOption : IModOption, IModDataContainer;
