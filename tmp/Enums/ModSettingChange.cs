namespace Penumbra.Api.Enums;

// Different types a mod setting can change:
public enum ModSettingChange
{
    Inheritance,      // it was set to inherit from other collections or not inherit anymore
    EnableState,      // it was enabled or disabled
    Priority,         // its priority was changed
    Setting,          // a specific setting was changed
    MultiInheritance, // multiple mods were set to inherit from other collections or not inherit anymore.
    MultiEnableState, // multiple mods were enabled or disabled at once.
}