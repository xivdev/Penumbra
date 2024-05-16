using Newtonsoft.Json.Linq;
using Penumbra.Mods.Groups;

namespace Penumbra.Mods.SubMods;

public class ImcSubMod(ImcModGroup group) : IModOption
{
    public readonly ImcModGroup Group = group;

    public ImcSubMod(ImcModGroup group, JToken json)
        : this(group)
    {
        SubMod.LoadOptionData(json, this);
    }

    public Mod Mod
        => Group.Mod;

    public byte AttributeIndex;

    public ushort Attribute
        => (ushort)(1 << AttributeIndex);

    Mod IModOption.Mod
        => Mod;

    IModGroup IModOption.Group
        => Group;

    public string Name { get; set; } = "Part";

    public string FullName
        => $"{Group.Name}: {Name}";

    public string Description { get; set; } = string.Empty;

    public int GetIndex()
        => SubMod.GetIndex(this);
}
