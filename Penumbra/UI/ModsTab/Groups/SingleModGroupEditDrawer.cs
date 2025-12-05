using ImSharp;
using Luna;
using Penumbra.Mods.Groups;

namespace Penumbra.UI.ModsTab.Groups;

public readonly struct SingleModGroupEditDrawer(ModGroupEditDrawer editor, SingleModGroup group) : IModGroupEditDrawer
{
    public void Draw()
    {
        foreach (var (optionIdx, option) in group.OptionData.Index())
        {
            using var id = Im.Id.Push(optionIdx);
            editor.DrawOptionPosition(group, option, optionIdx);

            Im.Line.SameInner();
            editor.DrawOptionDefaultSingleBehaviour(group, option, optionIdx);

            Im.Line.SameInner();
            editor.DrawOptionName(option);

            Im.Line.SameInner();
            editor.DrawOptionDescription(option);

            Im.Line.SameInner();
            editor.DrawOptionDelete(option);

            Im.Line.SameInner();
            Im.Dummy(new Vector2(editor.PriorityWidth, 0));
        }

        DrawNewOption();
        DrawConvertButton();
    }

    private void DrawConvertButton()
    {
        var convertible = group.Options.Count <= IModGroup.MaxMultiOptions;
        var g           = group;
        var e           = editor.ModManager.OptionEditor.SingleEditor;
        if (ImEx.Button("Convert to Multi Group"u8, editor.AvailableWidth, !convertible))
            editor.ActionQueue.Enqueue(() => e.ChangeToMulti(g));
        if (!convertible)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled,
                "Can not convert to multi group since maximum number of options is exceeded."u8);
    }

    private void DrawNewOption()
    {
        var count = group.Options.Count;
        if (count >= int.MaxValue)
            return;

        var name = editor.DrawNewOptionBase(group, count);

        var validName = name.Length > 0;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, validName
                ? "Add a new option to this group."u8
                : "Please enter a name for the new option."u8, !validName))
        {
            editor.ModManager.OptionEditor.SingleEditor.AddOption(group, name);
            editor.NewOptionName = null;
        }
    }
}
