using Luna;
using Penumbra.Mods.Groups;

namespace Penumbra.Mods.SubMods;

[Flags]
public enum ModSettingsLayout : ulong
{
    None    = 0,
    Disable = 0x01, // Disable the option or group instead of hiding it when the conditions are not fulfilled.
    Indent  = 0x02, // Indent the group if it is placed under another option or group.
}

public interface IModOption
{
    public Mod       Mod   { get; }
    public IModGroup Group { get; }

    public string                         Name        { get; set; }
    public string                         FullName    { get; }
    public string                         Description { get; set; }
    public ModSettingsLayout              Layout      { get; set; }
    public ICondition<ModSettingContext>? Condition   { get; set; }

    public int GetIndex();
}
