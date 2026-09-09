using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class SetQuickMoveFoldersButtons(ModFileSystemDrawer drawer) : BaseButton<IFileSystemFolder>
{
    public override ReadOnlySpan<byte> Label(in IFileSystemFolder data)
        => throw new NotImplementedException();

    public override bool DrawMenuItem(in IFileSystemFolder data)
    {
        for (var i = 0; i < UiConfig.NumQuickMoveFolders; ++i)
        {
            if (Im.Menu.Item($"Set as Quick Move Folder #{i + 1}"))
                drawer.Config.Ui.SetQuickMoveFolder(i, data.FullPath);
            var value = drawer.Config.Ui.QuickMoveFolder(i);
            Im.Tooltip.OnHover(value.Length is 0
                ? "Set this folder as a quick move location."u8
                : $"Set this folder as a quick move location instead of {value}.");
        }

        return false;
    }
}
