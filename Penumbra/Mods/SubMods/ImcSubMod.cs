using Newtonsoft.Json.Linq;
using Penumbra.GameData.Structs;
using Penumbra.Mods.Groups;

namespace Penumbra.Mods.SubMods;

public class ImcSubMod(ImcModGroup group) : IModOption
{
    public readonly ImcModGroup Group = group;

    public ImcSubMod(ImcModGroup group, JToken json)
        : this(group)
    {
        SubMod.LoadOptionData(json, this);
        AttributeMask   = (ushort)((json[nameof(AttributeMask)]?.ToObject<ushort>() ?? 0) & ImcEntry.AttributesMask);
        IsDisableSubMod = json[nameof(IsDisableSubMod)]?.ToObject<bool>() ?? false;
    }

    public static ImcSubMod DisableSubMod(ImcModGroup group)
        => new(group)
        {
            Name            = "Disable",
            AttributeMask   = 0,
            IsDisableSubMod = true,
        };

    public Mod Mod
        => Group.Mod;

    public ushort AttributeMask;
    public bool   IsDisableSubMod { get; private init; }

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
