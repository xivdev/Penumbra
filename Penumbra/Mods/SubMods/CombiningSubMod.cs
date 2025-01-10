using Newtonsoft.Json.Linq;
using Penumbra.Mods.Groups;

namespace Penumbra.Mods.SubMods;

public class CombiningSubMod(IModGroup group) : IModOption
{
    public IModGroup Group { get; } = group;

    public Mod Mod
        => Group.Mod;

    public string Name        { get; set; } = "Option";
    public string Description { get; set; } = string.Empty;

    public string FullName
        => $"{Group.Name}: {Name}";

    public int GetIndex()
        => SubMod.GetIndex(this);

    public CombiningSubMod(CombiningModGroup group, JToken json)
        : this(group)
        => SubMod.LoadOptionData(json, this);
}
