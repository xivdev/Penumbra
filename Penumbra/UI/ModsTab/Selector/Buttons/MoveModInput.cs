using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class MoveModInput(ModFileSystemDrawer fileSystem) : BaseButton<IFileSystemData>
{
    /// <inheritdoc/>
    public override ReadOnlySpan<byte> Label(in IFileSystemData _)
        => "##Move"u8;

    /// <summary> Replaces the normal menu item handling for a text input, so the other fields are not used. </summary>
    /// <inheritdoc/>
    public override bool DrawMenuItem(in IFileSystemData data)
    {
        var       currentPath = data.FullPath;
        using var style       = Im.Style.PushDefault(ImStyleDouble.FramePadding);
        MenuSeparator.DrawSeparator();
        Im.Text("Move Mod:"u8);
        if (Im.Window.Appearing)
            Im.Keyboard.SetFocusHere();
        var ret = Im.Input.Text(Label(data), ref currentPath, flags: InputTextFlags.EnterReturnsTrue);
        Im.Tooltip.OnHover(
            "Enter a full path here to move the mod or change its search path. Creates all required parent directories, if possible."u8);
        if (!ret)
            return false;

        fileSystem.FileSystem.RenameAndMove(data, currentPath);
        fileSystem.FileSystem.ExpandAllAncestors(data);
        Im.Popup.CloseCurrent();

        return ret;
    }
}
