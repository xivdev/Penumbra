using ImSharp;
using Luna;
using Penumbra.UI.Classes;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
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
    : ITab<ModPanelTab>
{
    private bool _inherited;
    private bool _temporary;
    private bool _locked;
    private int? _currentPriority;

    public ReadOnlySpan<byte> Label
        => "Settings"u8;

    public ModPanelTab Identifier
        => ModPanelTab.Settings;

    public void PostTabButton()
        => tutorial.OpenTutorial(BasicTutorialSteps.ModOptions);

    public void Reset()
        => _currentPriority = null;

    public void DrawContent()
    {
        using var table = Im.Table.Begin("##settings"u8, 1, TableFlags.ScrollY, Im.ContentRegion.Available);
        if (!table)
            return;

        _inherited = selection.Collection != collectionManager.Active.Current;
        _temporary = selection.TemporarySettings != null;
        _locked    = (selection.TemporarySettings?.Lock ?? 0) > 0;

        table.SetupScrollFreeze(0, 1);
        table.NextColumn();
        DrawTemporaryWarning();
        DrawInheritedWarning();
        Im.Dummy(Vector2.Zero);
        communicator.PreSettingsPanelDraw.Invoke(new PreSettingsPanelDraw.Arguments(selection.Mod!));
        DrawEnabledInput();
        tutorial.OpenTutorial(BasicTutorialSteps.EnablingMods);
        Im.Line.Same();
        DrawPriorityInput();
        tutorial.OpenTutorial(BasicTutorialSteps.Priority);
        DrawRemoveSettings();

        table.NextColumn();
        communicator.PostEnabledDraw.Invoke(new PostEnabledDraw.Arguments(selection.Mod!));

        modGroupDrawer.Draw(selection.Mod!, selection.Settings, selection.TemporarySettings);
        UiHelpers.DefaultLineSpace();
        communicator.PostSettingsPanelDraw.Invoke(new PostSettingsPanelDraw.Arguments(selection.Mod!));
    }

    /// <summary> Draw a big tinted bar if the current setting is temporary. </summary>
    private void DrawTemporaryWarning()
    {
        if (!_temporary)
            return;

        using var color =
            ImGuiColor.Button.Push(Rgba32.TintColor(Im.Style[ImGuiColor.Button], ColorId.TemporaryModSettingsTint.Value().ToVector()));
        var width = Im.ContentRegion.Available with { Y = 0 };
        if (ImEx.Button($"These settings are temporarily set by {selection.TemporarySettings!.Source}{(_locked ? " and locked." : ".")}",
                width, _locked))
            collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!, null);

        Im.Tooltip.OnHover("Changing settings in temporary settings will not save them across sessions.\n"u8
          + "You can click this button to remove the temporary settings and return to your normal settings."u8);
    }

    /// <summary> Draw a big red bar if the current setting is inherited. </summary>
    private void DrawInheritedWarning()
    {
        if (!_inherited)
            return;

        using var color = ImGuiColor.Button.Push(Colors.PressEnterWarningBg);
        var       width = Im.ContentRegion.Available with { Y = 0 };
        if (ImEx.Button($"These settings are inherited from {selection.Collection.Identity.Name}.", width, _locked))
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

        Im.Tooltip.OnHover("You can click this button to copy the current settings to the current selection.\n"u8
          + "You can also just change any setting, which will copy the settings with the single setting changed to the current selection."u8);
    }

    /// <summary> Draw a checkbox for the enabled status of the mod. </summary>
    private void DrawEnabledInput()
    {
        var       enabled  = selection.Settings.Enabled;
        using var disabled = Im.Disabled(_locked);
        if (!Im.Checkbox("Enabled"u8, ref enabled))
            return;

        modManager.SetKnown(selection.Mod!);
        if (_temporary || config.DefaultTemporaryMode)
        {
            var temporarySettings = selection.TemporarySettings ?? new TemporaryModSettings(selection.Mod!, selection.Settings);
            temporarySettings.ForceInherit = false;
            temporarySettings.Enabled      = enabled;
            collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!, temporarySettings);
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
        using var group    = Im.Group();
        var       settings = selection.Settings;
        var       priority = _currentPriority ?? settings.Priority.Value;
        Im.Item.SetNextWidth(50 * Im.Style.GlobalScale);
        using var disabled = Im.Disabled(_locked);
        if (Im.Input.Scalar("##Priority"u8, ref priority))
            _currentPriority = priority;
        if (new ModPriority(priority).IsHidden)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled,
                $"This priority is special-cased to hide this mod in conflict tabs ({ModPriority.HiddenMin}, {ModPriority.HiddenMax}).");


        if (Im.Item.DeactivatedAfterEdit && _currentPriority.HasValue)
        {
            if (_currentPriority != settings.Priority.Value)
            {
                if (_temporary || config.DefaultTemporaryMode)
                {
                    var temporarySettings = selection.TemporarySettings ?? new TemporaryModSettings(selection.Mod!, selection.Settings);
                    temporarySettings.ForceInherit = false;
                    temporarySettings.Priority     = new ModPriority(_currentPriority.Value);
                    collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!,
                        temporarySettings);
                }
                else
                {
                    collectionManager.Editor.SetModPriority(collectionManager.Active.Current, selection.Mod!,
                        new ModPriority(_currentPriority.Value));
                }
            }

            _currentPriority = null;
        }

        var hovered = LunaStyle.DrawHelpMarker();
        Im.Line.SameInner();
        Im.Text("Priority"u8);
        if (hovered || Im.Item.Hovered())
            Im.Tooltip.Set("Mods with a higher number here take precedence before Mods with a lower number.\n"u8
              + "That means, if Mod A should overwrite changes from Mod B, Mod A should have a higher priority number than Mod B."u8);
    }

    /// <summary>
    /// Draw a button to remove the current settings and inherit them instead
    /// in the top-right corner of the window/tab.
    /// </summary>
    private void DrawRemoveSettings()
    {
        var drawInherited = !_inherited && !selection.Settings.IsEmpty;
        var scroll        = Im.Scroll.MaximumY > 0 ? Im.Style.ScrollbarSize + Im.Style.ItemInnerSpacing.X : 0;
        var buttonSize    = Im.Font.CalculateSize("Turn Permanent_"u8).X;
        var offset = drawInherited
            ? buttonSize + Im.Font.CalculateSize("Inherit Settings"u8).X + Im.Style.FramePadding.X * 4 + Im.Style.ItemSpacing.X
            : buttonSize + Im.Style.FramePadding.X * 2;
        Im.Line.Same(Im.Window.Width - offset - scroll);
        var enabled = config.DeleteModModifier.IsActive();
        if (drawInherited)
        {
            var inherit = (enabled, _locked) switch
            {
                (true, false) => ImEx.Button("Inherit Settings"u8,
                    "Remove current settings from this collection so that it can inherit them.\n"u8
                  + "If no inherited collection has settings for this mod, it will be disabled."u8),
                (false, false) => ImEx.Button("Inherit Settings"u8, default,
                    $"Remove current settings from this collection so that it can inherit them.\nHold {config.DeleteModModifier} to inherit.",
                    true),
                (_, true) => ImEx.Button("Inherit Settings"u8, default,
                    "Remove current settings from this collection so that it can inherit them.\nThe settings are currently locked and can not be changed."u8,
                    true),
            };
            if (inherit)
            {
                if (_temporary || config.DefaultTemporaryMode)
                {
                    var temporarySettings = selection.TemporarySettings ?? new TemporaryModSettings(selection.Mod!, selection.Settings);
                    temporarySettings.ForceInherit = true;
                    collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!,
                        temporarySettings);
                }
                else
                {
                    collectionManager.Editor.SetModInheritance(collectionManager.Active.Current, selection.Mod!, true);
                }
            }

            Im.Line.Same();
        }

        if (_temporary)
        {
            var overwrite = enabled
                ? ImEx.Button("Turn Permanent"u8, new Vector2(buttonSize, 0),
                    "Overwrite the actual settings for this mod in this collection with the current temporary settings."u8)
                : ImEx.Button("Turn Permanent"u8, new Vector2(buttonSize, 0),
                    $"Overwrite the actual settings for this mod in this collection with the current temporary settings.\nHold {config.DeleteModModifier} to overwrite.",
                    true);
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
                    foreach (var (index, setting) in settings.Settings.Index())
                        collectionManager.Editor.SetModSetting(collectionManager.Active.Current, selection.Mod, index, setting);
                }

                collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod, null);
            }
        }
        else
        {
            var actual = collectionManager.Active.Current.GetActualSettings(selection.Mod!.Index).Settings;
            if (ImEx.Button("Turn Temporary"u8, "Copy the current settings over to temporary settings to experiment with them."u8))
                collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!,
                    new TemporaryModSettings(selection.Mod!, actual));
        }
    }
}
