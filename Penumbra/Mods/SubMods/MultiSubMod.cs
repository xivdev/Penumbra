using Newtonsoft.Json.Linq;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;

namespace Penumbra.Mods.SubMods;

public class MultiSubMod(MultiModGroup group) : OptionSubMod<MultiModGroup>(group)
{
    public ModPriority Priority { get; set; } = ModPriority.Default;

    public MultiSubMod(MultiModGroup group, JToken json)
        : this(group)
    {
        SubMod.LoadOptionData(json, this);
        SubMod.LoadDataContainer(json, this, group.Mod.ModPath);
        Priority = json[nameof(IModGroup.Priority)]?.ToObject<ModPriority>() ?? ModPriority.Default;
    }

    public MultiSubMod Clone(MultiModGroup group)
    {
        var ret = new MultiSubMod(group)
        {
            Name        = Name,
            Description = Description,
            Priority    = Priority,
        };
        SubMod.Clone(this, ret);

        return ret;
    }

    public SingleSubMod ConvertToSingle(SingleModGroup group)
    {
        var ret = new SingleSubMod(group)
        {
            Name        = Name,
            Description = Description,
        };
        SubMod.Clone(this, ret);
        return ret;
    }

    public static MultiSubMod WithoutGroup(string name, string description, ModPriority priority)
        => new(null!)
        {
            Name        = name,
            Description = description,
            Priority    = priority,
        };
}
