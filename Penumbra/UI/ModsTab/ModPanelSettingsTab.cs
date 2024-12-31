using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.UI.Classes;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.Mods.Settings;
using Penumbra.UI.ModsTab.Groups;

namespace Penumbra.UI.ModsTab;

public class ModPanelSettingsTab(
    CollectionManager collectionManager,
    ModManager modManager,
    ModSelection selection,
    TutorialService tutorial,
    CommunicatorService communicator,
    ModGroupDrawer modGroupDrawer)
    : ITab, IUiService
{
    private bool _inherited;
    private bool _temporary;
    private bool _locked;
    private int? _currentPriority;

    public ReadOnlySpan<byte> Label
        => "Settings"u8;

    public void DrawHeader()
        => tutorial.OpenTutorial(BasicTutorialSteps.ModOptions);

    public void Reset()
        => _currentPriority = null;

    public void DrawContent()
    {
        using var child = ImUtf8.Child("##settings"u8, default);
        if (!child)
            return;

        _inherited = selection.Collection != collectionManager.Active.Current;
        _temporary = selection.TemporarySettings != null;
        _locked    = (selection.TemporarySettings?.Lock ?? 0) > 0;
        DrawTemporaryWarning();
        DrawInheritedWarning();
        UiHelpers.DefaultLineSpace();
        communicator.PreSettingsPanelDraw.Invoke(selection.Mod!.Identifier);
        DrawEnabledInput();
        tutorial.OpenTutorial(BasicTutorialSteps.EnablingMods);
        ImGui.SameLine();
        DrawPriorityInput();
        tutorial.OpenTutorial(BasicTutorialSteps.Priority);
        DrawRemoveSettings();

        communicator.PostEnabledDraw.Invoke(selection.Mod!.Identifier);

        modGroupDrawer.Draw(selection.Mod!, selection.Settings, selection.TemporarySettings);
        UiHelpers.DefaultLineSpace();
        communicator.PostSettingsPanelDraw.Invoke(selection.Mod!.Identifier);
    }

    /// <summary> Draw a big tinted bar if the current setting is temporary. </summary>
    private void DrawTemporaryWarning()
    {
        if (!_temporary)
            return;

        using var color = ImRaii.PushColor(ImGuiCol.Button, ImGuiCol.Button.Tinted(ColorId.TemporaryModSettingsTint));
        var       width = new Vector2(ImGui.GetContentRegionAvail().X, 0);
        if (ImUtf8.ButtonEx($"These settings are temporary from {selection.TemporarySettings!.Source}{(_locked ? " and locked." : ".")}", width,
                _locked))
            collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!, null);

        ImUtf8.HoverTooltip("Changing settings in temporary settings will not save them across sessions.\n"u8
          + "You can click this button to remove the temporary settings and return to your normal settings."u8);
    }

    /// <summary> Draw a big red bar if the current setting is inherited. </summary>
    private void DrawInheritedWarning()
    {
        if (!_inherited)
            return;

        using var color = ImRaii.PushColor(ImGuiCol.Button, Colors.PressEnterWarningBg);
        var       width = new Vector2(ImGui.GetContentRegionAvail().X, 0);
        if (ImUtf8.ButtonEx($"These settings are inherited from {selection.Collection.Identity.Name}.", width, _locked))
        {
            if (_temporary)
            {
                selection.TemporarySettings!.ForceInherit = false;
                collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!, selection.TemporarySettings);
            }
            else
            {
                collectionManager.Editor.SetModInheritance(collectionManager.Active.Current, selection.Mod!, false);
            }
        }

        ImUtf8.HoverTooltip("You can click this button to copy the current settings to the current selection.\n"u8
          + "You can also just change any setting, which will copy the settings with the single setting changed to the current selection."u8);
    }

    /// <summary> Draw a checkbox for the enabled status of the mod. </summary>
    private void DrawEnabledInput()
    {
        var       enabled  = selection.Settings.Enabled;
        using var disabled = ImRaii.Disabled(_locked);
        if (!ImUtf8.Checkbox("Enabled"u8, ref enabled))
            return;

        modManager.SetKnown(selection.Mod!);
        if (_temporary)
        {
            selection.TemporarySettings!.ForceInherit = false;
            selection.TemporarySettings!.Enabled      = enabled;
            collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!, selection.TemporarySettings);
        }
        else
        {
            collectionManager.Editor.SetModState(collectionManager.Active.Current, selection.Mod!, enabled);
        }
    }

    /// <summary>
    /// Draw a priority input.
    /// Priority is changed on deactivation of the input box.
    /// </summary>
    private void DrawPriorityInput()
    {
        using var group    = ImUtf8.Group();
        var       settings = selection.Settings;
        var       priority = _currentPriority ?? settings.Priority.Value;
        ImGui.SetNextItemWidth(50 * UiHelpers.Scale);
        using var disabled = ImRaii.Disabled(_locked);
        if (ImUtf8.InputScalar("##Priority"u8, ref priority))
            _currentPriority = priority;
        if (new ModPriority(priority).IsHidden)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled,
                $"This priority is special-cased to hide this mod in conflict tabs ({ModPriority.HiddenMin}, {ModPriority.HiddenMax}).");


        if (ImGui.IsItemDeactivatedAfterEdit() && _currentPriority.HasValue)
        {
            if (_currentPriority != settings.Priority.Value)
            {
                if (_temporary)
                {
                    selection.TemporarySettings!.ForceInherit = false;
                    selection.TemporarySettings!.Priority     = new ModPriority(_currentPriority.Value);
                    collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!,
                        selection.TemporarySettings);
                }
                else
                {
                    collectionManager.Editor.SetModPriority(collectionManager.Active.Current, selection.Mod!,
                        new ModPriority(_currentPriority.Value));
                }
            }

            _currentPriority = null;
        }

        ImUtf8.LabeledHelpMarker("Priority"u8, "Mods with a higher number here take precedence before Mods with a lower number.\n"u8
          + "That means, if Mod A should overwrite changes from Mod B, Mod A should have a higher priority number than Mod B."u8);
    }

    /// <summary>
    /// Draw a button to remove the current settings and inherit them instead
    /// in the top-right corner of the window/tab.
    /// </summary>
    private void DrawRemoveSettings()
    {
        if (_inherited || selection.Settings == ModSettings.Empty)
            return;

        var scroll = ImGui.GetScrollMaxY() > 0 ? ImGui.GetStyle().ScrollbarSize : 0;
        ImGui.SameLine(ImGui.GetWindowWidth() - ImUtf8.CalcTextSize("Inherit Settings"u8).X - ImGui.GetStyle().FramePadding.X * 2 - scroll);
        if (!ImUtf8.ButtonEx("Inherit Settings"u8, "Remove current settings from this collection so that it can inherit them.\n"u8
              + "If no inherited collection has settings for this mod, it will be disabled."u8, default, _locked))
            return;

        if (_temporary)
        {
            selection.TemporarySettings!.ForceInherit = true;
            collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!, selection.TemporarySettings);
        }
        else
        {
            collectionManager.Editor.SetModInheritance(collectionManager.Active.Current, selection.Mod!, true);
        }
    }
}
