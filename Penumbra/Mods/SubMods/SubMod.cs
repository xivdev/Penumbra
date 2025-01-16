using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods.SubMods;

public static class SubMod
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static int GetIndex(IModOption option)
    {
        var dataIndex = option.Group.Options.IndexOf(option);
        if (dataIndex < 0)
            throw new Exception($"Group {option.Group.Name} from option {option.Name} does not contain this option.");

        return dataIndex;
    }

    /// <summary> Add all unique meta manipulations, file redirections and then file swaps from a ModDataContainer to the given sets. Skip any keys that are already contained. </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static void AddContainerTo(IModDataContainer container, Dictionary<Utf8GamePath, FullPath> redirections,
        MetaDictionary manipulations)
    {
        foreach (var (path, file) in container.Files)
            redirections.TryAdd(path, file);

        foreach (var (path, file) in container.FileSwaps)
            redirections.TryAdd(path, file);
        manipulations.UnionWith(container.Manipulations);
    }

    /// <summary> Replace all data of <paramref name="to"/> with the data of <paramref name="from"/>. </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static void Clone(IModDataContainer from, IModDataContainer to)
    {
        to.Files         = new Dictionary<Utf8GamePath, FullPath>(from.Files);
        to.FileSwaps     = new Dictionary<Utf8GamePath, FullPath>(from.FileSwaps);
        to.Manipulations = from.Manipulations.Clone();
    }

    /// <summary> Load all file redirections, file swaps and meta manipulations from a JToken of that option into a data container. </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static void LoadDataContainer(JToken json, IModDataContainer data, DirectoryInfo basePath)
    {
        data.Files.Clear();
        data.FileSwaps.Clear();
        data.Manipulations.Clear();

        var files = (JObject?)json[nameof(data.Files)];
        if (files != null)
            foreach (var property in files.Properties())
            {
                if (Utf8GamePath.FromString(property.Name, out var p))
                    data.Files.TryAdd(p, new FullPath(basePath, property.Value.ToObject<Utf8RelPath>()));
            }

        var swaps = (JObject?)json[nameof(data.FileSwaps)];
        if (swaps != null)
            foreach (var property in swaps.Properties())
            {
                if (Utf8GamePath.FromString(property.Name, out var p))
                    data.FileSwaps.TryAdd(p, new FullPath(property.Value.ToObject<string>()!));
            }

        var manips = json[nameof(data.Manipulations)]?.ToObject<MetaDictionary>();
        if (manips != null)
            data.Manipulations.UnionWith(manips);
    }

    /// <summary> Load the relevant data for a selectable option from a JToken of that option. </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static void LoadOptionData(JToken json, IModOption option)
    {
        option.Name        = json[nameof(option.Name)]?.ToObject<string>() ?? string.Empty;
        option.Description = json[nameof(option.Description)]?.ToObject<string>() ?? string.Empty;
    }

    /// <summary> Write file redirections, file swaps and meta manipulations from a data container on a JsonWriter. </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static void WriteModContainer(JsonWriter j, JsonSerializer serializer, IModDataContainer data, DirectoryInfo basePath)
    {
        // #TODO: remove comments when TexTools updated.
        //if (data.Files.Count > 0)
        //{
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
        //}

        //if (data.FileSwaps.Count > 0)
        //{
            j.WritePropertyName(nameof(data.FileSwaps));
            j.WriteStartObject();
            foreach (var (gamePath, file) in data.FileSwaps)
            {
                j.WritePropertyName(gamePath.ToString());
                j.WriteValue(file.ToString());
            }

            j.WriteEndObject();
        //}

        //if (data.Manipulations.Count > 0)
        //{
            j.WritePropertyName(nameof(data.Manipulations));
            serializer.Serialize(j, data.Manipulations);
        //}
    }

    /// <summary> Write the data for a selectable mod option on a JsonWriter. </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static void WriteModOption(JsonWriter j, IModOption option)
    {
        j.WritePropertyName(nameof(option.Name));
        j.WriteValue(option.Name);
        j.WritePropertyName(nameof(option.Description));
        j.WriteValue(option.Description);
    }
}
