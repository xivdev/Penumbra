using ImSharp;
using Luna;
using Penumbra.Mods.Groups;

namespace Penumbra.UI.ModsTab.Groups;

public readonly struct MultiModGroupEditDrawer(ModGroupEditDrawer editor, MultiModGroup group) : IModGroupEditDrawer
{
    public void Draw()
    {
        foreach (var (optionIdx, option) in group.OptionData.Index())
        {
            using var id = Im.Id.Push(optionIdx);
            editor.DrawOptionPosition(group, option, optionIdx);

            Im.Line.SameInner();
            editor.DrawOptionDefaultMultiBehaviour(group, option, optionIdx);

            Im.Line.SameInner();
            editor.DrawOptionName(option);

            Im.Line.SameInner();
            editor.DrawOptionDescription(option);

            Im.Line.SameInner();
            editor.DrawOptionDelete(option);

            Im.Line.SameInner();
            editor.DrawOptionPriority(option);
        }

        DrawNewOption();
        DrawConvertButton();
    }

    private void DrawConvertButton()
    {
        var g = group;
        var e = editor.ModManager.OptionEditor.MultiEditor;
        if (Im.Button("Convert to Single Group"u8, editor.AvailableWidth))
            editor.ActionQueue.Enqueue(() => e.ChangeToSingle(g));
    }

    private void DrawNewOption()
    {
        var count = group.Options.Count;
        if (count >= IModGroup.MaxMultiOptions)
            return;

        var name = editor.DrawNewOptionBase(group, count);

        var validName = name.Length > 0;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, validName
                ? "Add a new option to this group."u8
                : "Please enter a name for the new option."u8, !validName))
        {
            editor.ModManager.OptionEditor.MultiEditor.AddOption(group, name);
            editor.NewOptionName = null;
        }
    }
}
