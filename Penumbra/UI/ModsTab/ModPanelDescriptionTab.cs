using ImSharp;
using Luna;
using OtterGui.Widgets;
using Penumbra.Mods.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public class ModPanelDescriptionTab(
    ModFileSystemSelector selector,
    TutorialService tutorial,
    ModManager modManager,
    PredefinedTagManager predefinedTagsConfig)
    : ITab<ModPanelTab>
{
    private readonly TagButtons _localTags = new();
    private readonly TagButtons _modTags   = new();

    public ReadOnlySpan<byte> Label
        => "Description"u8;

    public ModPanelTab Identifier
        => ModPanelTab.Description;

    public void DrawContent()
    {
        using var child = Im.Child.Begin("##description"u8);
        if (!child)
            return;

        Im.ScaledDummy(2, 2);
        Im.ScaledDummy(2, 2);
        var (predefinedTagsEnabled, predefinedTagButtonOffset) = predefinedTagsConfig.Enabled
            ? (true, Im.Style.FrameHeight + Im.Style.WindowPadding.X + (Im.Scroll.MaximumY > 0 ? Im.Style.ScrollbarSize : 0))
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
                Im.Font.CalculateSize("Local "u8).X - Im.Font.CalculateSize("Mod "u8).X);

        Im.ScaledDummy(2, 2);
        Im.Separator();

        Im.TextWrapped(selector.Selected!.Description);
    }
}
