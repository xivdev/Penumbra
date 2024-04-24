using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Subclasses;

public interface IModDataOption : IModOption, IModDataContainer;

public class SingleSubMod(Mod mod, SingleModGroup group) : IModDataOption
{
    internal readonly Mod            Mod   = mod;
    internal readonly SingleModGroup Group = group;

    public string Name { get; set; } = "Option";

    public string FullName
        => $"{Group.Name}: {Name}";

    public string Description { get; set; } = string.Empty;

    IMod IModDataContainer.Mod
        => Mod;

    IModGroup IModDataContainer.Group
        => Group;

    public Dictionary<Utf8GamePath, FullPath> Files         { get; set; } = [];
    public Dictionary<Utf8GamePath, FullPath> FileSwaps     { get; set; } = [];
    public HashSet<MetaManipulation>          Manipulations { get; set; } = [];

    public SingleSubMod(Mod mod, SingleModGroup group, JToken json)
        : this(mod, group)
    {
        IModOption.Load(json, this);
        IModDataContainer.Load(json, this, mod.ModPath);
    }

    public SingleSubMod Clone(Mod mod, SingleModGroup group)
    {
        var ret = new SingleSubMod(mod, group)
        {
            Name        = Name,
            Description = Description,
        };
        IModDataContainer.Clone(this, ret);

        return ret;
    }

    public MultiSubMod ConvertToMulti(Mod mod, MultiModGroup group, ModPriority priority)
    {
        var ret = new MultiSubMod(mod, group)
        {
            Name        = Name,
            Description = Description,
            Priority    = priority,
        };
        IModDataContainer.Clone(this, ret);

        return ret;
    }

    public void AddDataTo(Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
        => ((IModDataContainer)this).AddDataTo(redirections, manipulations);

    public (int GroupIndex, int DataIndex) GetDataIndices()
        => (Group.GetIndex(), GetDataIndex());

    public (int GroupIndex, int OptionIndex) GetOptionIndices()
        => (Group.GetIndex(), GetDataIndex());

    private int GetDataIndex()
    {
        var dataIndex = Group.DataContainers.IndexOf(this);
        if (dataIndex < 0)
            throw new Exception($"Group {Group.Name} from SubMod {Name} does not contain this SubMod.");

        return dataIndex;
    }
}

public class MultiSubMod(Mod mod, MultiModGroup group) : IModDataOption
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

    IMod IModDataContainer.Mod
        => Mod;

    IModGroup IModDataContainer.Group
        => Group;


    public MultiSubMod(Mod mod, MultiModGroup group, JToken json)
        : this(mod, group)
    {
        IModOption.Load(json, this);
        IModDataContainer.Load(json, this, mod.ModPath);
        Priority = json[nameof(IModGroup.Priority)]?.ToObject<ModPriority>() ?? ModPriority.Default;
    }

    public MultiSubMod Clone(Mod mod, MultiModGroup group)
    {
        var ret = new MultiSubMod(mod, group)
        {
            Name        = Name,
            Description = Description,
            Priority    = Priority,
        };
        IModDataContainer.Clone(this, ret);

        return ret;
    }

    public SingleSubMod ConvertToSingle(Mod mod, SingleModGroup group)
    {
        var ret = new SingleSubMod(mod, group)
        {
            Name        = Name,
            Description = Description,
        };
        IModDataContainer.Clone(this, ret);
        return ret;
    }

    public void AddDataTo(Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
        => ((IModDataContainer)this).AddDataTo(redirections, manipulations);

    public static MultiSubMod CreateForSaving(string name, string description, ModPriority priority)
        => new(null!, null!)
        {
            Name        = name,
            Description = description,
            Priority    = priority,
        };

    public (int GroupIndex, int DataIndex) GetDataIndices()
        => (Group.GetIndex(), GetDataIndex());

    public (int GroupIndex, int OptionIndex) GetOptionIndices()
        => (Group.GetIndex(), GetDataIndex());

    private int GetDataIndex()
    {
        var dataIndex = Group.DataContainers.IndexOf(this);
        if (dataIndex < 0)
            throw new Exception($"Group {Group.Name} from SubMod {Name} does not contain this SubMod.");

        return dataIndex;
    }
}

public class DefaultSubMod(IMod mod) : IModDataContainer
{
    public const string FullName = "Default Option";

    public string Description
        => string.Empty;

    internal readonly IMod Mod = mod;

    public Dictionary<Utf8GamePath, FullPath> Files         { get; set; } = [];
    public Dictionary<Utf8GamePath, FullPath> FileSwaps     { get; set; } = [];
    public HashSet<MetaManipulation>          Manipulations { get; set; } = [];

    IMod IModDataContainer.Mod
        => Mod;

    IModGroup? IModDataContainer.Group
        => null;


    public DefaultSubMod(Mod mod, JToken json)
        : this(mod)
    {
        IModDataContainer.Load(json, this, mod.ModPath);
    }

    public void AddDataTo(Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
        => ((IModDataContainer)this).AddDataTo(redirections, manipulations);

    public (int GroupIndex, int DataIndex) GetDataIndices()
        => (-1, 0);
}


//public sealed class SubMod(IMod mod, IModGroup group) : IModOption
//{
//    public string Name { get; set; } = "Default";
//
//    public string FullName
//        => Group == null ? "Default Option" : $"{Group.Name}: {Name}";
//
//    public string Description { get; set; } = string.Empty;
//
//    internal readonly IMod       Mod   = mod;
//    internal readonly IModGroup? Group = group;
//
//    internal (int GroupIdx, int OptionIdx) GetIndices()
//    {
//        if (IsDefault)
//            return (-1, 0);
//
//        var groupIdx = Mod.Groups.IndexOf(Group);
//        if (groupIdx < 0)
//            throw new Exception($"Group {Group.Name} from SubMod {Name} is not contained in Mod {Mod.Name}.");
//
//        return (groupIdx, GetOptionIndex());
//    }
//
//    private int GetOptionIndex()
//    {
//        var optionIndex = Group switch
//        {
//            null                  => 0,
//            SingleModGroup single => single.OptionData.IndexOf(this),
//            MultiModGroup multi   => multi.OptionData.IndexOf(p => p.Mod == this),
//            _                     => throw new Exception($"Group {Group.Name} from SubMod {Name} has unknown type {typeof(Group)}"),
//        };
//        if (optionIndex < 0)
//            throw new Exception($"Group {Group!.Name} from SubMod {Name} does not contain this SubMod.");
//
//        return optionIndex;
//    }
//
//    public static SubMod CreateDefault(IMod mod)
//        => new(mod, null!);
//
//    [MemberNotNullWhen(false, nameof(Group))]
//    public bool IsDefault
//        => Group == null;
//
//    public void AddData(Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
//    {
//        foreach (var (path, file) in Files)
//            redirections.TryAdd(path, file);
//
//        foreach (var (path, file) in FileSwaps)
//            redirections.TryAdd(path, file);
//        manipulations.UnionWith(Manipulations);
//    }
//
//    public Dictionary<Utf8GamePath, FullPath> FileData         = [];
//    public Dictionary<Utf8GamePath, FullPath> FileSwapData     = [];
//    public HashSet<MetaManipulation>          ManipulationData = [];
//
//    public IReadOnlyDictionary<Utf8GamePath, FullPath> Files
//        => FileData;
//
//    public IReadOnlyDictionary<Utf8GamePath, FullPath> FileSwaps
//        => FileSwapData;
//
//    public IReadOnlySet<MetaManipulation> Manipulations
//        => ManipulationData;
//
//    public void Load(DirectoryInfo basePath, JToken json, out ModPriority priority)
//    {
//        FileData.Clear();
//        FileSwapData.Clear();
//        ManipulationData.Clear();
//
//        // Every option has a name, but priorities are only relevant for multi group options.
//        Name        = json[nameof(Name)]?.ToObject<string>() ?? string.Empty;
//        Description = json[nameof(Description)]?.ToObject<string>() ?? string.Empty;
//        priority    = json[nameof(IModGroup.Priority)]?.ToObject<ModPriority>() ?? ModPriority.Default;
//
//        var files = (JObject?)json[nameof(Files)];
//        if (files != null)
//            foreach (var property in files.Properties())
//            {
//                if (Utf8GamePath.FromString(property.Name, out var p, true))
//                    FileData.TryAdd(p, new FullPath(basePath, property.Value.ToObject<Utf8RelPath>()));
//            }
//
//        var swaps = (JObject?)json[nameof(FileSwaps)];
//        if (swaps != null)
//            foreach (var property in swaps.Properties())
//            {
//                if (Utf8GamePath.FromString(property.Name, out var p, true))
//                    FileSwapData.TryAdd(p, new FullPath(property.Value.ToObject<string>()!));
//            }
//
//        var manips = json[nameof(Manipulations)];
//        if (manips != null)
//            foreach (var s in manips.Children().Select(c => c.ToObject<MetaManipulation>())
//                         .Where(m => m.Validate()))
//                ManipulationData.Add(s);
//    }
//
//
//    /// <summary> Create a sub mod without a mod or group only for saving it in the creator. </summary>
//    internal static SubMod CreateForSaving(string name)
//        => new(null!, null!)
//        {
//            Name = name,
//        };
//
//
//    public static void WriteSubMod(JsonWriter j, JsonSerializer serializer, SubMod mod, DirectoryInfo basePath, ModPriority? priority)
//    {
//        j.WriteStartObject();
//        j.WritePropertyName(nameof(Name));
//        j.WriteValue(mod.Name);
//        j.WritePropertyName(nameof(Description));
//        j.WriteValue(mod.Description);
//        if (priority != null)
//        {
//            j.WritePropertyName(nameof(IModGroup.Priority));
//            j.WriteValue(priority.Value.Value);
//        }
//
//        j.WritePropertyName(nameof(mod.Files));
//        j.WriteStartObject();
//        foreach (var (gamePath, file) in mod.Files)
//        {
//            if (file.ToRelPath(basePath, out var relPath))
//            {
//                j.WritePropertyName(gamePath.ToString());
//                j.WriteValue(relPath.ToString());
//            }
//        }
//
//        j.WriteEndObject();
//        j.WritePropertyName(nameof(mod.FileSwaps));
//        j.WriteStartObject();
//        foreach (var (gamePath, file) in mod.FileSwaps)
//        {
//            j.WritePropertyName(gamePath.ToString());
//            j.WriteValue(file.ToString());
//        }
//
//        j.WriteEndObject();
//        j.WritePropertyName(nameof(mod.Manipulations));
//        serializer.Serialize(j, mod.Manipulations);
//        j.WriteEndObject();
//    }
//}
