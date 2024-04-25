using Newtonsoft.Json.Linq;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;

namespace Penumbra.Mods.SubMods;

public class MultiSubMod(Mod mod, MultiModGroup group) : OptionSubMod<MultiModGroup>(mod, group)
{
    public ModPriority Priority { get; set; } = ModPriority.Default;

    public MultiSubMod(Mod mod, MultiModGroup group, JToken json)
        : this(mod, group)
    {
        SubModHelpers.LoadOptionData(json, this);
        SubModHelpers.LoadDataContainer(json, this, mod.ModPath);
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
        SubModHelpers.Clone(this, ret);

        return ret;
    }

    public SingleSubMod ConvertToSingle(Mod mod, SingleModGroup group)
    {
        var ret = new SingleSubMod(mod, group)
        {
            Name        = Name,
            Description = Description,
        };
        SubModHelpers.Clone(this, ret);
        return ret;
    }

    public static MultiSubMod CreateForSaving(string name, string description, ModPriority priority)
        => new(null!, null!)
        {
            Name        = name,
            Description = description,
            Priority    = priority,
        };
}
