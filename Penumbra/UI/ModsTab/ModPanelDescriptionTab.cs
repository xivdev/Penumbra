using ImSharp;
using Luna;
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
        var tagIdx = TagButtons.Draw("Local Tags: "u8,
            "Custom tags you can set personally that will not be exported to the mod data but only set for you.\n"u8
          + "If the mod already contains a local tag in its own tags, the local tag will be ignored."u8, selector.Selected!.LocalTags,
            out var editedTag, rightEndOffset: predefinedTagButtonOffset);
        tutorial.OpenTutorial(BasicTutorialSteps.Tags);
        if (tagIdx >= 0)
            modManager.DataEditor.ChangeLocalTag(selector.Selected!, tagIdx, editedTag);

        if (predefinedTagsEnabled)
            predefinedTagsConfig.DrawAddFromSharedTagsAndUpdateTags(selector.Selected!.LocalTags, selector.Selected!.ModTags, true,
                selector.Selected!);

        if (selector.Selected!.ModTags.Count > 0)
            TagButtons.Draw("Mod Tags: "u8, "Tags assigned by the mod creator and saved with the mod data. To edit these, look at Edit Mod."u8,
                selector.Selected!.ModTags, out _, false,
                Im.Font.CalculateSize("Local "u8).X - Im.Font.CalculateSize("Mod "u8).X);

        Im.ScaledDummy(2, 2);
        Im.Separator();

        Im.TextWrapped(selector.Selected!.Description);
    }
}
