using Dalamud.Interface.Utility;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using Penumbra.UI.Classes;
using OtterGui.Raii;

namespace Penumbra.UI;

public class IncognitoService(TutorialService tutorial)
{
    public bool IncognitoMode;

    public void DrawToggle(float? buttonWidth = null)
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale);
        using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.FolderExpanded.Value())
            .Push(ImGuiCol.Border, ColorId.FolderExpanded.Value());
        if (ImGuiUtil.DrawDisabledButton(
                $"{(IncognitoMode ? FontAwesomeIcon.Eye : FontAwesomeIcon.EyeSlash).ToIconString()}###IncognitoMode",
                new Vector2(buttonWidth ?? ImGui.GetFrameHeightWithSpacing(), ImGui.GetFrameHeight()), string.Empty, false, true))
            IncognitoMode = !IncognitoMode;
        var hovered = ImGui.IsItemHovered();
        tutorial.OpenTutorial(BasicTutorialSteps.Incognito);
        color.Pop(2);
        if (hovered)
            ImGui.SetTooltip(IncognitoMode ? "Toggle incognito mode off." : "Toggle incognito mode on.");
    }
}
