using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;

namespace Penumbra.Mods.SubMods;

public class MultiSubMod(MultiModGroup group) : OptionSubMod<MultiModGroup>(group)
{
    public ModPriority Priority { get; set; } = ModPriority.Default;

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
}
