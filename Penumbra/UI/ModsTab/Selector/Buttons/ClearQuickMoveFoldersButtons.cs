using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class ClearQuickMoveFoldersButtons(ModFileSystemDrawer drawer) : BaseButton
{
    public override ReadOnlySpan<byte> Label
        => throw new NotImplementedException();

    public override bool DrawMenuItem()
    {
        if (drawer.Config.QuickMoveFolder1.Length > 0)
        {
            if (Im.Menu.Item("Clear Quick Move Folder #1"u8))
            {
                drawer.Config.QuickMoveFolder1 = string.Empty;
                drawer.Config.Save();
            }

            Im.Tooltip.OnHover($"Clear the current quick move assignment of {drawer.Config.QuickMoveFolder1}.");
        }


        if (drawer.Config.QuickMoveFolder2.Length > 0)
        {
            if (Im.Menu.Item("Clear Quick Move Folder #2"u8))
            {
                drawer.Config.QuickMoveFolder2 = string.Empty;
                drawer.Config.Save();
            }

            Im.Tooltip.OnHover($"Clear the current quick move assignment of {drawer.Config.QuickMoveFolder2}.");
        }


        if (drawer.Config.QuickMoveFolder3.Length > 0)
        {
            if (Im.Menu.Item("Clear Quick Move Folder #3"u8))
            {
                drawer.Config.QuickMoveFolder3 = string.Empty;
                drawer.Config.Save();
            }

            Im.Tooltip.OnHover($"Clear the current quick move assignment of {drawer.Config.QuickMoveFolder3}.");
        }

        return false;
    }
}
