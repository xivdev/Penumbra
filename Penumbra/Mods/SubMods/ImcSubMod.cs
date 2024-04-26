using Penumbra.Mods.Groups;

namespace Penumbra.Mods.SubMods;

public class ImcSubMod(ImcModGroup group) : IModOption
{
    public readonly ImcModGroup Group = group;

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
