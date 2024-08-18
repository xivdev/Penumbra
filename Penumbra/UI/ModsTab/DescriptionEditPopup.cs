using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab;

public class DescriptionEditPopup(ModManager modManager) : IUiService
{
    private static ReadOnlySpan<byte> PopupId
        => "PenumbraEditDescription"u8;

    private bool   _hasBeenEdited;
    private string _description = string.Empty;

    private object? _current;
    private bool    _opened;

    public void Open(Mod mod)
    {
        _current       = mod;
        _opened        = true;
        _hasBeenEdited = false;
        _description   = mod.Description;
    }

    public void Open(IModGroup group)
    {
        _current       = group;
        _opened        = true;
        _hasBeenEdited = false;
        _description   = group.Description;
    }

    public void Open(IModOption option)
    {
        _current       = option;
        _opened        = true;
        _hasBeenEdited = false;
        _description   = option.Description;
    }

    public void Draw()
    {
        if (_current == null)
            return;

        if (_opened)
        {
            _opened = false;
            ImUtf8.OpenPopup(PopupId);
        }

        var       inputSize = ImGuiHelpers.ScaledVector2(800);
        using var popup     = ImUtf8.Popup(PopupId);
        if (!popup)
            return;

        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere();

        ImUtf8.InputMultiLineOnDeactivated("##editDescription"u8, ref _description, inputSize);
        _hasBeenEdited |= ImGui.IsItemEdited();
        UiHelpers.DefaultLineSpace();

        var buttonSize = new Vector2(ImUtf8.GlobalScale * 100, 0);

        var width = 2 * buttonSize.X
          + 4 * ImUtf8.FramePadding.X
          + ImUtf8.ItemSpacing.X;

        ImGui.SetCursorPosX((inputSize.X - width) / 2);
        DrawSaveButton(buttonSize);
        ImGui.SameLine();
        DrawCancelButton(buttonSize);
    }

    private void DrawSaveButton(Vector2 buttonSize)
    {
        if (!ImUtf8.ButtonEx("Save"u8, _hasBeenEdited ? [] : "No changes made yet."u8, buttonSize, !_hasBeenEdited))
            return;

        switch (_current)
        {
            case Mod mod:
                modManager.DataEditor.ChangeModDescription(mod, _description);
                break;
            case IModGroup group:
                modManager.OptionEditor.ChangeGroupDescription(group, _description);
                break;
            case IModOption option:
                modManager.OptionEditor.ChangeOptionDescription(option, _description);
                break;
        }

        _description   = string.Empty;
        _hasBeenEdited = false;
        ImGui.CloseCurrentPopup();
    }

    private void DrawCancelButton(Vector2 buttonSize)
    {
        if (!ImUtf8.Button("Cancel"u8, buttonSize) && !ImGui.IsKeyPressed(ImGuiKey.Escape))
            return;

        _description   = string.Empty;
        _hasBeenEdited = false;
        ImGui.CloseCurrentPopup();
    }
}
