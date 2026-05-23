using Luna;
using Newtonsoft.Json.Linq;
using Penumbra.Mods.Groups;
using Penumbra.UI.Classes;

namespace Penumbra.Mods.SubMods;

public class CombiningSubMod(IModGroup group) : IModOption
{
    public IModGroup Group { get; } = group;

    public int Index { get; private set; } = -1;

    public void SetIndex(int index)
        => Index = index;

    public Mod Mod
        => Group.Mod;

    public Guid              Id          { get; set; } = Guid.NewGuid();
    public string            Name        { get; set; } = "Option";
    public string            Description { get; set; } = string.Empty;
    public ModSettingsLayout Layout      { get; set; }
    public ColorId           Color       { get; set; }
    public bool              Separator   { get; set; }

    public ICondition<ModSettingContext>? Condition { get; set; }

    public string FullName
        => $"{Group.Name}: {Name}";

    public CombiningSubMod(CombiningModGroup group, JToken json)
        : this(group)
        => SubMod.LoadOptionData(json, this);
}
