using Newtonsoft.Json.Linq;
using OtterGui.Extensions;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.String.Classes;

namespace Penumbra.Mods.SubMods;

public class CombinedDataContainer(IModGroup group) : IModDataContainer
{
    public IMod Mod
        => Group.Mod;

    public IModGroup Group { get; } = group;

    public string                             Name          { get; set; } = string.Empty;
    public Dictionary<Utf8GamePath, FullPath> Files         { get; set; } = [];
    public Dictionary<Utf8GamePath, FullPath> FileSwaps     { get; set; } = [];
    public MetaDictionary                     Manipulations { get; set; } = new();

    public void AddDataTo(Dictionary<Utf8GamePath, FullPath> redirections, MetaDictionary manipulations)
        => SubMod.AddContainerTo(this, redirections, manipulations);

    public string GetName()
    {
        if (Name.Length > 0)
            return Name;

        var index = GetDataIndex();
        if (index == 0)
            return "None";

        var sb = new StringBuilder(128);
        for (var i = 0; i < IModGroup.MaxCombiningOptions; ++i)
        {
            if ((index & 1) != 0)
            {
                sb.Append(Group.Options[i].Name);
                sb.Append(' ').Append('+').Append(' ');
            }

            index >>= 1;
            if (index == 0)
                break;
        }

        return sb.ToString(0, sb.Length - 3);
    }

    public unsafe string GetDirectoryName()
    {
        if (Name.Length > 0)
            return Name;

        var index = GetDataIndex();
        if (index == 0)
            return "None";

        var text = stackalloc char[IModGroup.MaxCombiningOptions].Slice(0, Group.Options.Count);
        for (var i = 0; i < Group.Options.Count; ++i)
        {
            text[Group.Options.Count - 1 - i] =   (index & 1) is 0 ? '0' : '1';
            index   >>= 1;
        }

        return new string(text);
    }

    public string GetFullName()
        => $"{Group.Name}: {GetName()}";

    public (int GroupIndex, int DataIndex) GetDataIndices()
        => (Group.GetIndex(), GetDataIndex());

    private int GetDataIndex()
    {
        var dataIndex = Group.DataContainers.IndexOf(this);
        if (dataIndex < 0)
            throw new Exception($"Group {Group.Name} from SubMod {Name} does not contain this SubMod.");

        return dataIndex;
    }

    public CombinedDataContainer(CombiningModGroup group, JToken token)
        : this(group)
    {
        SubMod.LoadDataContainer(token, this, group.Mod.ModPath);
        Name = token["Name"]?.ToObject<string>() ?? string.Empty;
    }
}
