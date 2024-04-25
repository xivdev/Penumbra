using OtterGui;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.String.Classes;

namespace Penumbra.Mods.SubMods;

public interface IModDataOption : IModDataContainer, IModOption;

public abstract class OptionSubMod<T>(Mod mod, T group) : IModDataOption
    where T : IModGroup
{
    internal readonly Mod       Mod   = mod;
    internal readonly IModGroup Group = group;

    public string Name { get; set; } = "Option";

    public string FullName
        => $"{Group!.Name}: {Name}";

    public string Description { get; set; } = string.Empty;

    IMod IModDataContainer.Mod
        => Mod;

    IModGroup IModDataContainer.Group
        => Group;

    public Dictionary<Utf8GamePath, FullPath> Files         { get; set; } = [];
    public Dictionary<Utf8GamePath, FullPath> FileSwaps     { get; set; } = [];
    public HashSet<MetaManipulation>          Manipulations { get; set; } = [];

    public void AddDataTo(Dictionary<Utf8GamePath, FullPath> redirections, HashSet<MetaManipulation> manipulations)
        => SubModHelpers.AddContainerTo(this, redirections, manipulations);

    public string GetName()
        => Name;

    public string GetFullName()
        => FullName;

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
