using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Subclasses;

public class SingleSubMod(Mod mod, SingleModGroup group) : IModOption, IModDataContainer
{
    internal readonly Mod            Mod   = mod;
    internal readonly SingleModGroup Group = group;

    public string Name { get; set; } = "Option";

    public string FullName
        => $"{Group.Name}: {Name}";

    public string Description { get; set; } = string.Empty;

    public Dictionary<Utf8GamePath, FullPath> Files         { get; set; } = [];
    public Dictionary<Utf8GamePath, FullPath> FileSwaps     { get; set; } = [];
    public HashSet<MetaManipulation>          Manipulations { get; set; } = [];
}

public class MultiSubMod(Mod mod, MultiModGroup group) : IModOption, IModDataContainer
{
    internal readonly Mod           Mod   = mod;
    internal readonly MultiModGroup Group = group;

    public string Name { get; set; } = "Option";

    public string FullName
        => $"{Group.Name}: {Name}";

    public string      Description { get; set; } = string.Empty;
    public ModPriority Priority    { get; set; } = ModPriority.Default;

    public Dictionary<Utf8GamePath, FullPath> Files         { get; set; } = [];
    public Dictionary<Utf8GamePath, FullPath> FileSwaps     { get; set; } = [];
    public HashSet<MetaManipulation>          Manipulations { get; set; } = [];
}

public class DefaultSubMod(IMod mod) : IModDataContainer
{
    public string FullName
        => "Default Option";

    public string Description
        => string.Empty;

    internal readonly IMod Mod = mod;

    public Dictionary<Utf8GamePath, FullPath> Files         { get; set; } = [];
    public Dictionary<Utf8GamePath, FullPath> FileSwaps     { get; set; } = [];
    public HashSet<MetaManipulation>          Manipulations { get; set; } = [];
}

/// <summary>
/// A sub mod is a collection of
/// - file replacements
///   - file swaps
///   - meta manipulations
/// that can be used either as an option or as the default data for a mod.
/// It can be loaded and reloaded from Json.
/// Nothing is checked for existence or validity when loading.
/// Objects are also not checked for uniqueness, the first appearance of a game path or meta path decides.
/// </summary>
public sealed class SubMod(IMod mod, IModGroup group) : IModOption
{
    public string Name { get; set; } = "Default";

    public string FullName
        => Group == null ? "Default Option" : $"{Group.Name}: {Name}";

    public string Description { get; set; } = string.Empty;

    internal readonly IMod       Mod   = mod;
    internal readonly IModGroup? Group = group;

    internal (int GroupIdx, int OptionIdx) GetIndices()
    {
        if (IsDefault)
            return (-1, 0);

        var groupIdx = Mod.Groups.IndexOf(Group);
        if (groupIdx < 0)
            throw new Exception($"Group {Group.Name} from SubMod {Name} is not contained in Mod {Mod.Name}.");

        return (groupIdx, GetOptionIndex());
    }

    private int GetOptionIndex()
    {
        var optionIndex = Group switch
        {
            null                  => 0,
            SingleModGroup single => single.OptionData.IndexOf(this),
            MultiModGroup multi   => multi.PrioritizedOptions.IndexOf(p => p.Mod == this),
            _                     => throw new Exception($"Group {Group.Name} from SubMod {Name} has unknown type {typeof(Group)}"),
        };
        if (optionIndex < 0)
            throw new Exception($"Group {Group!.Name} from SubMod {Name} does not contain this SubMod.");

        return optionIndex;
    }

    public static SubMod CreateDefault(IMod mod)
        => new(mod, null!);

    [MemberNotNullWhen(false, nameof(Group))]
    public bool IsDefault
        => Group == null;

    public void AddData(Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
    {
        foreach (var (path, file) in Files)
            redirections.TryAdd(path, file);

        foreach (var (path, file) in FileSwaps)
            redirections.TryAdd(path, file);
        manipulations.UnionWith(Manipulations);
    }

    public Dictionary<Utf8GamePath, FullPath> FileData         = [];
    public Dictionary<Utf8GamePath, FullPath> FileSwapData     = [];
    public HashSet<MetaManipulation>          ManipulationData = [];

    public IReadOnlyDictionary<Utf8GamePath, FullPath> Files
        => FileData;

    public IReadOnlyDictionary<Utf8GamePath, FullPath> FileSwaps
        => FileSwapData;

    public IReadOnlySet<MetaManipulation> Manipulations
        => ManipulationData;

    public void Load(DirectoryInfo basePath, JToken json, out ModPriority priority)
    {
        FileData.Clear();
        FileSwapData.Clear();
        ManipulationData.Clear();

        // Every option has a name, but priorities are only relevant for multi group options.
        Name        = json[nameof(Name)]?.ToObject<string>() ?? string.Empty;
        Description = json[nameof(Description)]?.ToObject<string>() ?? string.Empty;
        priority    = json[nameof(IModGroup.Priority)]?.ToObject<ModPriority>() ?? ModPriority.Default;

        var files = (JObject?)json[nameof(Files)];
        if (files != null)
            foreach (var property in files.Properties())
            {
                if (Utf8GamePath.FromString(property.Name, out var p, true))
                    FileData.TryAdd(p, new FullPath(basePath, property.Value.ToObject<Utf8RelPath>()));
            }

        var swaps = (JObject?)json[nameof(FileSwaps)];
        if (swaps != null)
            foreach (var property in swaps.Properties())
            {
                if (Utf8GamePath.FromString(property.Name, out var p, true))
                    FileSwapData.TryAdd(p, new FullPath(property.Value.ToObject<string>()!));
            }

        var manips = json[nameof(Manipulations)];
        if (manips != null)
            foreach (var s in manips.Children().Select(c => c.ToObject<MetaManipulation>())
                         .Where(m => m.Validate()))
                ManipulationData.Add(s);
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

    /// <summary> Create a sub mod without a mod or group only for saving it in the creator. </summary>
    internal static SubMod CreateForSaving(string name)
        => new(null!, null!)
        {
            Name = name,
        };


    public static void WriteSubMod(JsonWriter j, JsonSerializer serializer, SubMod mod, DirectoryInfo basePath, ModPriority? priority)
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
