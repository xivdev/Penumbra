using ImSharp;
using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab;

public sealed class
    DescriptionEditPopup(ModManager modManager) : ObjectEditPopup, IUiService
{
    protected override ReadOnlySpan<byte> PopupId
        => "EditDesc"u8;

    private StringU8 _description = StringU8.Empty;

    public void Open(Mod mod)
    {
        Open((object)mod);
        _description = new StringU8(mod.Description);
    }

    public void Open(IModGroup group)
    {
        Open((object)group);
        _description = new StringU8(group.Description);
    }

    public void Open(IModOption option)
    {
        Open((object)option);
        _description = new StringU8(option.Description);
    }

    protected override void DrawInternal()
    {
        if (Im.Window.Appearing)
            Im.Keyboard.SetFocusHere();

        var inputSize = ImEx.ScaledVector(800);
        if (Im.Input.MultiLine("##editDescription"u8, ref _description, inputSize))
            Edited = true;
        UiHelpers.DefaultLineSpace();

        var buttonSize = ImEx.ScaledVectorX(100);

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
        if (!ImEx.Button("Save"u8, buttonSize, Edited ? StringU8.Empty : "No changes made yet."u8, !Edited))
            return;

        switch (Current)
        {
            case Mod mod:           modManager.DataEditor.ChangeModDescription(mod, _description.ToString()); break;
            case IModGroup group:   modManager.OptionEditor.ChangeGroupDescription(group, _description.ToString()); break;
            case IModOption option: modManager.OptionEditor.ChangeOptionDescription(option, _description.ToString()); break;
        }

        _description = StringU8.Empty;
        Close();
    }

    private void DrawCancelButton(Vector2 buttonSize)
    {
        if (!Im.Button("Cancel"u8, buttonSize) && !Im.Keyboard.IsPressed(Key.Escape))
            return;

        _description = StringU8.Empty;
        Close();
    }
}
