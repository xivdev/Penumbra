namespace Penumbra.UI.ModsTab;

public enum RenameField
{
    None,
    RenameSearchPath,
    RenameData,
    BothSearchPathPrio,
    BothDataPrio,
}

public static class RenameFieldExtensions
{
    public static (string Name, string Desc) GetData(this RenameField value)
        => value switch
        {
            RenameField.None             => ("None", "Show no rename fields in the context menu for mods."),
            RenameField.RenameSearchPath => ("Search Path", "Show only the search path / move field in the context menu for mods."),
            RenameField.RenameData       => ("Mod Name", "Show only the mod name field in the context menu for mods."),
            RenameField.BothSearchPathPrio => ("Both (Focus Search Path)",
                "Show both rename fields in the context menu for mods, but put the keyboard cursor on the search path field."),
            RenameField.BothDataPrio => ("Both (Focus Mod Name)",
                "Show both rename fields in the context menu for mods, but put the keyboard cursor on the mod name field"),
            _ => (string.Empty, string.Empty),
        };
}
