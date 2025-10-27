using Dalamud.Interface;
using Dalamud.Bindings.ImGui;
using ImSharp;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.Mods.Groups;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab.Groups;

public readonly struct CombiningModGroupEditDrawer(ModGroupEditDrawer editor, CombiningModGroup group) : IModGroupEditDrawer
{
    public void Draw()
    {
        foreach (var (optionIdx, option) in group.OptionData.Index())
        {
            using var id = ImUtf8.PushId(optionIdx);
            editor.DrawOptionPosition(group, option, optionIdx);

            ImUtf8.SameLineInner();
            editor.DrawOptionDefaultMultiBehaviour(group, option, optionIdx);

            ImUtf8.SameLineInner();
            editor.DrawOptionName(option);

            ImUtf8.SameLineInner();
            editor.DrawOptionDescription(option);

            ImUtf8.SameLineInner();
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
        if (ImUtf8.IconButton(FontAwesomeIcon.Plus, validName
                ? "Add a new option to this group."u8
                : "Please enter a name for the new option."u8, default, !validName))
        {
            editor.ModManager.OptionEditor.CombiningEditor.AddOption(group, name);
            editor.NewOptionName = null;
        }
    }

    private unsafe void DrawContainerNames()
    {
        if (ImUtf8.ButtonEx("Edit Container Names"u8,
                "Add optional names to separate data containers of the combining group.\nThose are just for easier identification while editing the mod, and are not generally displayed to the user."u8,
                new Vector2(400 * ImUtf8.GlobalScale, 0)))
            ImUtf8.OpenPopup("DataContainerNames"u8);

        var sizeX = group.OptionData.Count * (ImGui.GetStyle().ItemInnerSpacing.X + Im.Style.FrameHeight) + 300 * ImUtf8.GlobalScale;
        ImGui.SetNextWindowSize(new Vector2(sizeX, Im.Style.FrameHeightWithSpacing * Math.Min(16, group.Data.Count) + 200 * ImUtf8.GlobalScale));
        using var popup = ImUtf8.Popup("DataContainerNames"u8);
        if (!popup)
            return;

        foreach (var option in group.OptionData)
        {
            ImUtf8.RotatedText(option.Name, true);
            ImUtf8.SameLineInner();
        }

        Im.Line.New();
        ImGui.Separator();
        using var child = ImUtf8.Child("##Child"u8, Im.ContentRegion.Available);
        ImGuiClip.ClippedDraw(group.Data, DrawRow, Im.Style.FrameHeightWithSpacing);
    }

    private void DrawRow(CombinedDataContainer container, int index)
    {
        using var id = ImUtf8.PushId(index);
        using (ImRaii.Disabled())
        {
            for (var i = 0; i < group.OptionData.Count; ++i)
            {
                id.Push(i);
                var check = (index & (1 << i)) != 0;
                ImUtf8.Checkbox(""u8, ref check);
                ImUtf8.SameLineInner();
                id.Pop();
            }
        }

        var name = editor.CombiningDisplayIndex == index ? editor.CombiningDisplayName ?? container.Name : container.Name;
        if (ImUtf8.InputText("##Nothing"u8, ref name, "Optional Display Name..."u8))
        {
            editor.CombiningDisplayIndex = index;
            editor.CombiningDisplayName  = name;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
            editor.ModManager.OptionEditor.CombiningEditor.SetDisplayName(container, name);

        if (ImGui.IsItemDeactivated())
        {
            editor.CombiningDisplayIndex = -1;
            editor.CombiningDisplayName  = null;
        }
    }
}
