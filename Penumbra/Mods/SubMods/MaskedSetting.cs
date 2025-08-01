using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;

namespace Penumbra.Mods.SubMods;

public readonly struct MaskedSetting(Setting mask, Setting value)
{
    public const           int           MaxSettings = IModGroup.MaxMultiOptions;
    public static readonly MaskedSetting Zero        = new(Setting.Zero, Setting.Zero);
    public static readonly MaskedSetting FullMask    = new(Setting.AllBits(IModGroup.MaxComplexOptions), Setting.Zero);

    public readonly Setting Mask  = mask;
    public readonly Setting Value = new(value.Value & mask.Value);

    public MaskedSetting(ulong mask, ulong value)
        : this(new Setting(mask), new Setting(value))
    { }

    public MaskedSetting Limit(int numOptions)
        => new(Mask.Value & Setting.AllBits(numOptions).Value, Value.Value);

    public bool IsZero
        => Mask.Value is 0;

    public bool IsEnabled(Setting input)
        => (input.Value & Mask.Value) == Value.Value;
}
