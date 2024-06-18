using OtterGui;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.String.Classes;

namespace Penumbra.Mods.SubMods;

public abstract class OptionSubMod(IModGroup group) : IModOption, IModDataContainer
{
    protected readonly IModGroup Group = group;

    public Mod Mod
        => Group.Mod;

    public string Name        { get; set; } = "Option";
    public string Description { get; set; } = string.Empty;

    public string FullName
        => $"{Group.Name}: {Name}";

    Mod IModOption.Mod
        => Mod;

    IMod IModDataContainer.Mod
        => Mod;

    IModGroup IModDataContainer.Group
        => Group;

    IModGroup IModOption.Group
        => Group;

    public Dictionary<Utf8GamePath, FullPath> Files         { get; set; } = [];
    public Dictionary<Utf8GamePath, FullPath> FileSwaps     { get; set; } = [];
    public MetaDictionary                     Manipulations { get; set; } = new();

    public void AddDataTo(Dictionary<Utf8GamePath, FullPath> redirections, MetaDictionary manipulations)
        => SubMod.AddContainerTo(this, redirections, manipulations);

    public string GetName()
        => Name;

    public string GetFullName()
        => FullName;

    public (int GroupIndex, int DataIndex) GetDataIndices()
        => (Group.GetIndex(), GetDataIndex());

    public int GetIndex()
        => SubMod.GetIndex(this);

    private int GetDataIndex()
    {
        var dataIndex = Group.DataContainers.IndexOf(this);
        if (dataIndex < 0)
            throw new Exception($"Group {Group.Name} from SubMod {Name} does not contain this SubMod.");

        return dataIndex;
    }
}

public abstract class OptionSubMod<T>(T group) : OptionSubMod(group)
    where T : IModGroup
{
    public new T Group
        => (T)base.Group;
}
