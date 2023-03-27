using System;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using OtterGui.Widgets;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public class ModPanelDescriptionTab : ITab
{
    private readonly ModFileSystemSelector _selector;
    private readonly TutorialService       _tutorial;
    private readonly ModManager           _modManager;
    private readonly TagButtons            _localTags = new();
    private readonly TagButtons            _modTags   = new();

    public ModPanelDescriptionTab(ModFileSystemSelector selector, TutorialService tutorial, ModManager modManager)
    {
        _selector   = selector;
        _tutorial   = tutorial;
        _modManager = modManager;
    }

    public ReadOnlySpan<byte> Label
        => "Description"u8;

    public void DrawContent()
    {
        using var child = ImRaii.Child("##description");
        if (!child)
            return;

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));
        var tagIdx = _localTags.Draw("Local Tags: ",
            "Custom tags you can set personally that will not be exported to the mod data but only set for you.\n"
          + "If the mod already contains a local tag in its own tags, the local tag will be ignored.", _selector.Selected!.LocalTags,
            out var editedTag);
        _tutorial.OpenTutorial(BasicTutorialSteps.Tags);
        if (tagIdx >= 0)
            _modManager.DataEditor.ChangeLocalTag(_selector.Selected!, tagIdx, editedTag);

        if (_selector.Selected!.ModTags.Count > 0)
            _modTags.Draw("Mod Tags: ", "Tags assigned by the mod creator and saved with the mod data. To edit these, look at Edit Mod.",
                _selector.Selected!.ModTags, out var _, false,
                ImGui.CalcTextSize("Local ").X - ImGui.CalcTextSize("Mod ").X);

        ImGui.Dummy(ImGuiHelpers.ScaledVector2(2));
        ImGui.Separator();

        ImGuiUtil.TextWrapped(_selector.Selected!.Description);
    }
}
