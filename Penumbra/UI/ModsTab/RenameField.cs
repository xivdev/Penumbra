using Luna.Generators;

namespace Penumbra.UI.ModsTab;

[NamedEnum(Utf16: false)]
[TooltipEnum]
public enum RenameField
{
    [Name("None")]
    [Tooltip("Show no rename fields in the context menu for mods.")]
    None,

    [Name("Search Path")]
    [Tooltip("Show only the search path / move field in the context menu for mods.")]
    RenameSearchPath,

    [Name("Mod Name")]
    [Tooltip("Show only the mod name field in the context menu for mods.")]
    RenameData,

    [Name("Both (Focus Search Path)")]
    [Tooltip("Show both rename fields in the context menu for mods, but put the keyboard cursor on the search path field.")]
    BothSearchPathPrio,

    [Name("Both (Focus Mod Name)")]
    [Tooltip("Show both rename fields in the context menu for mods, but put the keyboard cursor on the mod name field")]
    BothDataPrio,
}
