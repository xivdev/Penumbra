using ImSharp;
using Luna;
using Penumbra.Mods.Groups;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab.Groups;

public readonly struct CombiningModGroupEditDrawer(ModGroupEditDrawer editor, CombiningModGroup group) : IModGroupEditDrawer
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
        }

        DrawNewOption();
        DrawContainerNames();
    }

    private void DrawNewOption()
    {
        var count = group.OptionData.Count;
        if (count >= IModGroup.MaxCombiningOptions)
            return;

        var name = editor.DrawNewOptionBase(group, count);

        var validName = name.Length > 0;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, validName
                ? "Add a new option to this group."u8
                : "Please enter a name for the new option."u8, !validName))
        {
            editor.ModManager.OptionEditor.CombiningEditor.AddOption(group, name);
            editor.NewOptionName = null;
        }
    }

    private void DrawContainerNames()
    {
        if (ImEx.Button("Edit Container Names"u8, new Vector2(400 * Im.Style.GlobalScale, 0),
                "Add optional names to separate data containers of the combining group.\nThose are just for easier identification while editing the mod, and are not generally displayed to the user."u8))
            Im.Popup.Open("names"u8);

        var sizeX = group.OptionData.Count * (Im.Style.ItemInnerSpacing.X + Im.Style.FrameHeight) + 300 * Im.Style.GlobalScale;
        Im.Window.SetNextSize(new Vector2(sizeX,
            Im.Style.FrameHeightWithSpacing * Math.Min(16, group.Data.Count) + 200 * Im.Style.GlobalScale));
        using var popup = Im.Popup.Begin("names"u8);
        if (!popup)
            return;

        foreach (var option in group.OptionData)
        {
            ImEx.RotatedText(option.Name, true);
            Im.Line.SameInner();
        }

        Im.Line.New();
        Im.Separator();
        using var child = Im.Child.Begin("##Child"u8, Im.ContentRegion.Available);
        Im.ListClipper.Draw(group.Data, DrawRow, Im.Style.FrameHeightWithSpacing);
    }

    private void DrawRow(CombinedDataContainer container, int index)
    {
        using var id = Im.Id.Push(index);
        using (Im.Disabled())
        {
            for (var i = 0; i < group.OptionData.Count; ++i)
            {
                id.Push(i);
                var check = (index & (1 << i)) != 0;
                Im.Checkbox(""u8, ref check);
                Im.Line.SameInner();
                id.Pop();
            }
        }

        if (ImEx.InputOnDeactivation.Text("##Nothing"u8, container.Name, out string newName, "Optional Display Name..."u8))
            editor.ModManager.OptionEditor.CombiningEditor.SetDisplayName(container, newName);
    }
}
