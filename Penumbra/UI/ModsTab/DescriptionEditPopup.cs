using ImSharp;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab;

public class DescriptionEditPopup(ModManager modManager) : Luna.IUiService
{
    private static ReadOnlySpan<byte> PopupId
        => "EditDesc"u8;

    private bool   _hasBeenEdited;
    private StringU8 _description = StringU8.Empty;

    private object? _current;
    private bool    _opened;

    public void Open(Mod mod)
    {
        _current       = mod;
        _opened        = true;
        _hasBeenEdited = false;
        _description   = new StringU8(mod.Description);
    }

    public void Open(IModGroup group)
    {
        _current       = group;
        _opened        = true;
        _hasBeenEdited = false;
        _description   = new StringU8(group.Description);
    }

    public void Open(IModOption option)
    {
        _current       = option;
        _opened        = true;
        _hasBeenEdited = false;
        _description   = new StringU8(option.Description);
    }

    public void Draw()
    {
        if (_current == null)
            return;

        if (_opened)
        {
            _opened = false;
            Im.Popup.Open(PopupId);
        }

        var       inputSize = ImEx.ScaledVector(800);
        using var popup     = Im.Popup.Begin(PopupId);
        if (!popup)
            return;

        if (Im.Window.Appearing)
            Im.Keyboard.SetFocusHere();

        if (Im.Input.MultiLine("##editDescription"u8, ref _description, inputSize))
            _hasBeenEdited = true;
        UiHelpers.DefaultLineSpace();

        var buttonSize = new Vector2(Im.Style.GlobalScale * 100, 0);

        var width = 2 * buttonSize.X
          + 4 * Im.Style.FramePadding.X
          + Im.Style.ItemSpacing.X;

        Im.Cursor.X = (inputSize.X - width) / 2;
        DrawSaveButton(buttonSize);
        Im.Line.Same();
        DrawCancelButton(buttonSize);
    }

    private void DrawSaveButton(Vector2 buttonSize)
    {
        if (!ImEx.Button("Save"u8, buttonSize, _hasBeenEdited ? StringU8.Empty : "No changes made yet."u8, !_hasBeenEdited))
            return;

        switch (_current)
        {
            case Mod mod:           modManager.DataEditor.ChangeModDescription(mod, _description.ToString()); break;
            case IModGroup group:   modManager.OptionEditor.ChangeGroupDescription(group, _description.ToString()); break;
            case IModOption option: modManager.OptionEditor.ChangeOptionDescription(option, _description.ToString()); break;
        }

        _description   = StringU8.Empty;
        _hasBeenEdited = false;
        Im.Popup.CloseCurrent();
    }

    private void DrawCancelButton(Vector2 buttonSize)
    {
        if (!Im.Button("Cancel"u8, buttonSize) && !Im.Keyboard.IsPressed(Key.Escape))
            return;

        _description   = StringU8.Empty;
        _hasBeenEdited = false;
        Im.Popup.CloseCurrent();
    }
}
