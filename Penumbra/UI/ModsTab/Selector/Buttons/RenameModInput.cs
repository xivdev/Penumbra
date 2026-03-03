using ImSharp;
using Luna;
using Penumbra.Mods;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class RenameModInput(ModFileSystemDrawer fileSystem) : BaseButton<IFileSystemData>
{
    /// <inheritdoc/>
    public override ReadOnlySpan<byte> Label(in IFileSystemData _)
        => "##Rename"u8;

    /// <summary> Replaces the normal menu item handling for a text input, so the other fields are not used. </summary>
    /// <inheritdoc/>
    public override bool DrawMenuItem(in IFileSystemData data)
    {
        var       mod         = (Mod)data.Value;
        var       currentName = mod.Name;
        using var style       = Im.Style.PushDefault(ImStyleDouble.FramePadding);
        MenuSeparator.DrawSeparator();
        Im.Text("Rename Mod:"u8);
        if (Im.Window.Appearing)
            Im.Keyboard.SetFocusHere();
        var ret = Im.Input.Text(Label(data), ref currentName, flags: InputTextFlags.EnterReturnsTrue);
        Im.Tooltip.OnHover("Enter a new name here to rename the changed mod."u8);
        if (!ret)
            return false;

        fileSystem.ModManager.DataEditor.ChangeModName(mod, currentName);
        Im.Popup.CloseCurrent();

        return ret;
    }
}
