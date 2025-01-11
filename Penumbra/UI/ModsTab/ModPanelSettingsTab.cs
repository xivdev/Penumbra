using ImGuiNET;
using OtterGui;
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
    ModGroupDrawer modGroupDrawer,
    Configuration config)
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
        using var table = ImUtf8.Table("##settings"u8, 1, ImGuiTableFlags.ScrollY, ImGui.GetContentRegionAvail());
        if (!table)
            return;

        _inherited = selection.Collection != collectionManager.Active.Current;
        _temporary = selection.TemporarySettings != null;
        _locked    = (selection.TemporarySettings?.Lock ?? 0) > 0;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableNextColumn();
        DrawTemporaryWarning();
        DrawInheritedWarning();
        ImGui.Dummy(Vector2.Zero);
        communicator.PreSettingsPanelDraw.Invoke(selection.Mod!.Identifier);
        DrawEnabledInput();
        tutorial.OpenTutorial(BasicTutorialSteps.EnablingMods);
        ImGui.SameLine();
        DrawPriorityInput();
        tutorial.OpenTutorial(BasicTutorialSteps.Priority);
        DrawRemoveSettings();

        ImGui.TableNextColumn();
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
        if (ImUtf8.ButtonEx($"These settings are temporarily set by {selection.TemporarySettings!.Source}{(_locked ? " and locked." : ".")}",
                width,
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
        var drawInherited = !_inherited && selection.Settings != ModSettings.Empty;
        var scroll        = ImGui.GetScrollMaxY() > 0 ? ImGui.GetStyle().ScrollbarSize + ImGui.GetStyle().ItemInnerSpacing.X : 0;
        var buttonSize    = ImUtf8.CalcTextSize("Turn Permanent_"u8).X;
        var offset = drawInherited
            ? buttonSize + ImUtf8.CalcTextSize("Inherit Settings"u8).X + ImGui.GetStyle().FramePadding.X * 4 + ImGui.GetStyle().ItemSpacing.X
            : buttonSize + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine(ImGui.GetWindowWidth() - offset - scroll);
        var enabled = config.DeleteModModifier.IsActive();
        if (drawInherited)
        {
            var inherit = (enabled, _locked) switch
            {
                (true, false) => ImUtf8.ButtonEx("Inherit Settings"u8,
                    "Remove current settings from this collection so that it can inherit them.\n"u8
                  + "If no inherited collection has settings for this mod, it will be disabled."u8, default, false),
                (false, false) => ImUtf8.ButtonEx("Inherit Settings"u8,
                    $"Remove current settings from this collection so that it can inherit them.\nHold {config.DeleteModModifier} to inherit.",
                    default, true),
                (_, true) => ImUtf8.ButtonEx("Inherit Settings"u8,
                    "Remove current settings from this collection so that it can inherit them.\nThe settings are currently locked and can not be changed."u8,
                    default, true),
            };
            if (inherit)
            {
                if (_temporary)
                {
                    selection.TemporarySettings!.ForceInherit = true;
                    collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!,
                        selection.TemporarySettings);
                }
                else
                {
                    collectionManager.Editor.SetModInheritance(collectionManager.Active.Current, selection.Mod!, true);
                }
            }

            ImGui.SameLine();
        }

        if (_temporary)
        {
            var overwrite = enabled
                ? ImUtf8.ButtonEx("Turn Permanent"u8,
                    "Overwrite the actual settings for this mod in this collection with the current temporary settings."u8,
                    new Vector2(buttonSize, 0))
                : ImUtf8.ButtonEx("Turn Permanent"u8,
                    $"Overwrite the actual settings for this mod in this collection with the current temporary settings.\nHold {config.DeleteModModifier} to overwrite.",
                    new Vector2(buttonSize, 0), true);
            if (overwrite)
            {
                var settings = collectionManager.Active.Current.GetTempSettings(selection.Mod!.Index)!;
                if (settings.ForceInherit)
                {
                    collectionManager.Editor.SetModInheritance(collectionManager.Active.Current, selection.Mod, true);
                }
                else
                {
                    collectionManager.Editor.SetModState(collectionManager.Active.Current, selection.Mod, settings.Enabled);
                    collectionManager.Editor.SetModPriority(collectionManager.Active.Current, selection.Mod, settings.Priority);
                    foreach (var (setting, index) in settings.Settings.WithIndex())
                        collectionManager.Editor.SetModSetting(collectionManager.Active.Current, selection.Mod, index, setting);
                }

                collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod, null);
            }
        }
        else
        {
            var actual = collectionManager.Active.Current.GetActualSettings(selection.Mod!.Index).Settings;
            if (ImUtf8.ButtonEx("Turn Temporary"u8, "Copy the current settings over to temporary settings to experiment with them."u8))
                collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!,
                    new TemporaryModSettings(actual, "yourself"));
        }
    }
}
