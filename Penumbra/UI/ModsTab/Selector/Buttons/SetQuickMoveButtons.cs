using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class SetQuickMoveFoldersButtons(ModFileSystemDrawer drawer) : BaseButton<IFileSystemFolder>
{
    public override ReadOnlySpan<byte> Label(in IFileSystemFolder data)
        => throw new NotImplementedException();

    public override bool DrawMenuItem(in IFileSystemFolder data)
    {
        if (Im.Menu.Item("Set as Quick Move Folder #1"u8))
        {
            drawer.Config.QuickMoveFolder1 = data.FullPath;
            drawer.Config.Save();
        }
        Im.Tooltip.OnHover(drawer.Config.QuickMoveFolder1.Length is 0 ? "Set this folder as a quick move location."u8 : $"Set this folder as a quick move location instead of {drawer.Config.QuickMoveFolder1}.");

        if (Im.Menu.Item("Set as Quick Move Folder #2"u8))
        {
            drawer.Config.QuickMoveFolder2 = data.FullPath;
            drawer.Config.Save();
        }
        Im.Tooltip.OnHover(drawer.Config.QuickMoveFolder2.Length is 0 ? "Set this folder as a quick move location."u8 : $"Set this folder as a quick move location instead of {drawer.Config.QuickMoveFolder2}.");

        if (Im.Menu.Item("Set as Quick Move Folder #3"u8))
        {
            drawer.Config.QuickMoveFolder3 = data.FullPath;
            drawer.Config.Save();
        }
        Im.Tooltip.OnHover(drawer.Config.QuickMoveFolder3.Length is 0 ? "Set this folder as a quick move location."u8 : $"Set this folder as a quick move location instead of {drawer.Config.QuickMoveFolder3}.");
        return false;
    }
}
