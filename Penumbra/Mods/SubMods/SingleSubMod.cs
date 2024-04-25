using Newtonsoft.Json.Linq;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;

namespace Penumbra.Mods.SubMods;

public class SingleSubMod(Mod mod, SingleModGroup singleGroup) : OptionSubMod<SingleModGroup>(mod, singleGroup)
{
    public SingleSubMod(Mod mod, SingleModGroup singleGroup, JToken json)
        : this(mod, singleGroup)
    {
        SubModHelpers.LoadOptionData(json, this);
        SubModHelpers.LoadDataContainer(json, this, mod.ModPath);
    }

    public SingleSubMod Clone(Mod mod, SingleModGroup group)
    {
        var ret = new SingleSubMod(mod, group)
        {
            Name        = Name,
            Description = Description,
        };
        SubModHelpers.Clone(this, ret);

        return ret;
    }

    public MultiSubMod ConvertToMulti(Mod mod, MultiModGroup group, ModPriority priority)
    {
        var ret = new MultiSubMod(mod, group)
        {
            Name        = Name,
            Description = Description,
            Priority    = priority,
        };
        SubModHelpers.Clone(this, ret);

        return ret;
    }
}
