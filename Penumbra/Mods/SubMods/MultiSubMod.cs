using Newtonsoft.Json.Linq;
using OtterGui;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.String.Classes;

namespace Penumbra.Mods.SubMods;

public class MultiSubMod(Mod mod, MultiModGroup group) : IModDataOption
{
    internal readonly Mod Mod = mod;
    internal readonly MultiModGroup Group = group;

    public string Name { get; set; } = "Option";

    public string FullName
        => $"{Group.Name}: {Name}";

    public string Description { get; set; } = string.Empty;
    public ModPriority Priority { get; set; } = ModPriority.Default;

    public Dictionary<Utf8GamePath, FullPath> Files { get; set; } = [];
    public Dictionary<Utf8GamePath, FullPath> FileSwaps { get; set; } = [];
    public HashSet<MetaManipulation> Manipulations { get; set; } = [];

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
            Name = Name,
            Description = Description,
            Priority = Priority,
        };
        IModDataContainer.Clone(this, ret);

        return ret;
    }

    public SingleSubMod ConvertToSingle(Mod mod, SingleModGroup group)
    {
        var ret = new SingleSubMod(mod, group)
        {
            Name = Name,
            Description = Description,
        };
        IModDataContainer.Clone(this, ret);
        return ret;
    }

    public void AddDataTo(Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
        => IModDataContainer.AddDataTo(this, redirections, manipulations);

    public static MultiSubMod CreateForSaving(string name, string description, ModPriority priority)
        => new(null!, null!)
        {
            Name = name,
            Description = description,
            Priority = priority,
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
