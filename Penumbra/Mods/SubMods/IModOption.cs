using Penumbra.Mods.Groups;

namespace Penumbra.Mods.SubMods;

[Flags]
public enum ModSettingsLayout : ulong
{
    None         = 0,
    Disable      = 0x01, // Disable the option or group instead of hiding it when the conditions are not fulfilled.
    Indent       = 0x02, // Indent the group if it is placed under another option or group.
    ParentHeader = 0x04, // Show the groups name or just its options if it is placed under another option or group.
}

public interface IModOption : IModObject
{
    public string    FullName { get; }
}
