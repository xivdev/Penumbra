using Dalamud.Interface;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.Mods.Groups;

namespace Penumbra.UI.ModsTab.Groups;

public readonly struct MultiModGroupEditDrawer(ModGroupEditDrawer editor, MultiModGroup group) : IModGroupEditDrawer
{
    public void Draw()
    {
        foreach (var (option, optionIdx) in group.OptionData.WithIndex())
        {
            using var id = ImRaii.PushId(optionIdx);
            editor.DrawOptionPosition(group, option, optionIdx);

            ImUtf8.SameLineInner();
            editor.DrawOptionDefaultMultiBehaviour(group, option, optionIdx);

            ImUtf8.SameLineInner();
            editor.DrawOptionName(option);

            ImUtf8.SameLineInner();
            editor.DrawOptionDescription(option);

            ImUtf8.SameLineInner();
            editor.DrawOptionDelete(option);

            ImUtf8.SameLineInner();
            editor.DrawOptionPriority(option);
        }

        DrawNewOption();
        DrawConvertButton();
    }

    private void DrawConvertButton()
    {
        var g = group;
        var e = editor.ModManager.OptionEditor.MultiEditor;
        if (ImUtf8.Button("Convert to Single Group"u8, editor.AvailableWidth))
            editor.ActionQueue.Enqueue(() => e.ChangeToSingle(g));
    }

    private void DrawNewOption()
    {
        var count = group.Options.Count;
        if (count >= IModGroup.MaxMultiOptions)
            return;

        var name = editor.DrawNewOptionBase(group, count);

        var validName = name.Length > 0;
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, validName
                ? "Add a new option to this group."u8
                : "Please enter a name for the new option."u8, default, !validName))
        {
            editor.ModManager.OptionEditor.MultiEditor.AddOption(group, name);
            editor.NewOptionName = null;
        }
    }
}
