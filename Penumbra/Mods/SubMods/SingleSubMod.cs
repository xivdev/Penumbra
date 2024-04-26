using Newtonsoft.Json.Linq;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;

namespace Penumbra.Mods.SubMods;

public class SingleSubMod(SingleModGroup singleGroup) : OptionSubMod<SingleModGroup>(singleGroup)
{
    public SingleSubMod(SingleModGroup singleGroup, JToken json)
        : this(singleGroup)
    {
        SubMod.LoadOptionData(json, this);
        SubMod.LoadDataContainer(json, this, singleGroup.Mod.ModPath);
    }

    public SingleSubMod Clone(SingleModGroup group)
    {
        var ret = new SingleSubMod(group)
        {
            Name        = Name,
            Description = Description,
        };
        SubMod.Clone(this, ret);

        return ret;
    }

    public MultiSubMod ConvertToMulti(MultiModGroup group, ModPriority priority)
    {
        var ret = new MultiSubMod(group)
        {
            Name        = Name,
            Description = Description,
            Priority    = priority,
        };
        SubMod.Clone(this, ret);

        return ret;
    }
}
