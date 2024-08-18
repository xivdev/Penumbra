using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using OtterGui.Services;
using OtterGui.Widgets;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ModsTab;

public class ModPanelDescriptionTab(
    ModFileSystemSelector selector,
    TutorialService tutorial,
    ModManager modManager,
    PredefinedTagManager predefinedTagsConfig)
    : ITab, IUiService
{
    private readonly TagButtons _localTags = new();
    private readonly TagButtons _modTags   = new();

    public ReadOnlySpan<byte> Label
        => "Description"u8;

    public void DrawContent()
    {
        using var child = ImRaii.Child("##description");
        if (!child)
            return;

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));
        var (predefinedTagsEnabled, predefinedTagButtonOffset) = predefinedTagsConfig.Count > 0
            ? (true, ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.X + (ImGui.GetScrollMaxY() > 0 ? ImGui.GetStyle().ScrollbarSize : 0))
            : (false, 0);
        var tagIdx = _localTags.Draw("Local Tags: ",
            "Custom tags you can set personally that will not be exported to the mod data but only set for you.\n"
          + "If the mod already contains a local tag in its own tags, the local tag will be ignored.", selector.Selected!.LocalTags,
            out var editedTag, rightEndOffset: predefinedTagButtonOffset);
        tutorial.OpenTutorial(BasicTutorialSteps.Tags);
        if (tagIdx >= 0)
            modManager.DataEditor.ChangeLocalTag(selector.Selected!, tagIdx, editedTag);

        if (predefinedTagsEnabled)
            predefinedTagsConfig.DrawAddFromSharedTagsAndUpdateTags(selector.Selected!.LocalTags, selector.Selected!.ModTags, true,
                selector.Selected!);

        if (selector.Selected!.ModTags.Count > 0)
            _modTags.Draw("Mod Tags: ", "Tags assigned by the mod creator and saved with the mod data. To edit these, look at Edit Mod.",
                selector.Selected!.ModTags, out _, false,
                ImGui.CalcTextSize("Local ").X - ImGui.CalcTextSize("Mod ").X);

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));
        ImGui.Separator();

        ImGuiUtil.TextWrapped(selector.Selected!.Description);
    }
}
