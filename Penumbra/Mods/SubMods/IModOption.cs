using Luna;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.UI.Classes;

namespace Penumbra.Mods.SubMods;

[Flags]
public enum ModSettingsLayout : ulong
{
    None         = 0,
    Disable      = 0x01, // Disable the option or group instead of hiding it when the conditions are not fulfilled.
    Indent       = 0x02, // Indent the group if it is placed under another option or group.
    ParentHeader = 0x04, // Show the groups name or just its options if it is placed under another option or group.
}

public interface IModObject
{
    public Mod                            Mod         { get; }
    public IModGroup                      Group       { get; }
    public Guid                           Id          { get; set; }
    public string                         Name        { get; set; }
    public string                         Description { get; set; }
    public ModSettingsLayout              Layout      { get; set; }
    public ICondition<ModSettingContext>? Condition   { get; set; }
}

public interface IModOption : IModObject, IIndexed
{
    public string FullName { get; }
    public int GroupIndex
        => Group.Index;

    public ColorId Color     { get; set; }
    public bool    Separator { get; set; }
}

public static class ModSettingsLayoutExtensions
{
    extension(ModSettingsLayout layout)
    {
        public IEnumerable<ModSettingsLayout> Iterate()
        {
            if (layout.HasFlag(ModSettingsLayout.Disable))
                yield return ModSettingsLayout.Disable;
            if (layout.HasFlag(ModSettingsLayout.Indent))
                yield return ModSettingsLayout.Indent;
            if (layout.HasFlag(ModSettingsLayout.ParentHeader))
                yield return ModSettingsLayout.ParentHeader;
        }
    }
}
