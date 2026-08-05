using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class ClearQuickMoveFoldersButtons(ModFileSystemDrawer drawer) : BaseButton
{
    public override ReadOnlySpan<byte> Label
        => throw new NotImplementedException();

    public override bool DrawMenuItem()
    {
        for (var i = 0; i < UiConfig.NumQuickMoveFolders; ++i)
        {
            var value = drawer.Config.Ui.QuickMoveFolder(i);
            if (value.Length <= 0)
                continue;

            if (Im.Menu.Item($"Clear Quick Move Folder #{i + 1}"))
                drawer.Config.Ui.SetQuickMoveFolder(i, string.Empty);
            Im.Tooltip.OnHover($"Clear the current quick move assignment of {value}.");
        }

        return false;
    }
}
