using ImSharp;
using Luna;
using Penumbra.Api.Preset;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.GameData.Gui;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab.Settings;

namespace Penumbra.UI.ModsTab;

public class ModPanelSettingsTab(
    CollectionManager collectionManager,
    ModManager modManager,
    ModSelection selection,
    TutorialService tutorial,
    CommunicatorService communicator,
    ModGroupDrawer modGroupDrawer,
    Configuration config,
    PresetCombo presets)
    : ITab<ModPanelTab>
{
    private bool _temporary;
    private bool _locked;
    private int? _currentPriority;
    private bool _editPresetMode;
    private bool _actualEditPresetMode;

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
        using var id = Im.Id.Push(selection.ModName);
        var cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current,
            () => new ModSettingsCache(selection, config.Ui, communicator, Im.State.Storage));

        _actualEditPresetMode = _editPresetMode && presets.Selected is not null;
        _temporary            = selection.TemporarySettings is not null;
        _locked               = (selection.TemporarySettings?.Lock ?? 0) > 0;

        if (cache.VisiblePages.Count > 1 && config.Ui.DisplayPages)
        {
            DrawPreamble();
            if (_actualEditPresetMode)
            {
                DrawEditPresetMode();
            }
            else
            {
                communicator.PostEnabledDraw.Invoke(new PostEnabledDraw.Arguments(selection.Mod!));
                modGroupDrawer.Draw(cache, selection.Mod!, selection.Settings, selection.TemporarySettings);
            }
        }
        else
        {
            using var style = ImStyleDouble.CellPadding.PushY(0);
            using var table = Im.Table.Begin("##settings"u8, 1, TableFlags.ScrollY, Im.ContentRegion.Available);
            if (!table)
                return;

            table.SetupScrollFreeze(0, 1);
            table.NextColumn();
            style.Pop();
            DrawPreamble();
            Im.Dummy(0);
            table.NextColumn();
            if (_actualEditPresetMode)
            {
                DrawEditPresetMode();
            }
            else
            {
                communicator.PostEnabledDraw.Invoke(new PostEnabledDraw.Arguments(selection.Mod!));
                modGroupDrawer.Draw(cache, selection.Mod!, selection.Settings, selection.TemporarySettings);
            }
        }
    }

    private void DrawPreamble()
    {
        if (!_actualEditPresetMode)
        {
            DrawTemporaryWarning();
            DrawInheritedWarning();
        }

        Im.Dummy(Vector2.Zero);
        DrawPresetRow();
        if (!_actualEditPresetMode)
        {
            communicator.PreSettingsPanelDraw.Invoke(new PreSettingsPanelDraw.Arguments(selection.Mod!));
            DrawEnabledInput();
            tutorial.OpenTutorial(BasicTutorialSteps.EnablingMods);
            Im.Line.Same();
            DrawPriorityInput();
            tutorial.OpenTutorial(BasicTutorialSteps.Priority);
            DrawRemoveSettings();
        }
    }

    /// <summary> Draw a big tinted bar if the current setting is temporary. </summary>
    private void DrawTemporaryWarning()
    {
        if (!_temporary)
            return;

        using var color =
            ImGuiColor.Button.Push(Rgba32.TintColor(Im.Style[ImGuiColor.Button], ColorId.TemporaryModSettingsTint.Vector));
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
        if (!selection.Inherited)
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
        if (_temporary || config.Main.DefaultTemporaryMode)
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

    private void DrawPresetRow()
    {
        if (config.Ui.HidePresetBar)
            return;

        using var id = Im.Id.Push("presets"u8);
        if (ImEx.Icon.Button(LunaStyle.FromClipboardIcon, "Try to import a setting preset from the clipboard."u8))
            if (SettingPresetData.FromClipboard(out var data))
                collectionManager.Editor.ApplyPreset(collectionManager.Active.Current, selection.Mod!, data,
                    config.Main.DefaultTemporaryMode || _temporary);
        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.ToClipboardIcon, "Copy the current settings to clipboard as a sharable preset."u8))
            SettingPresetData.FromMod(selection.Mod!, selection.Settings).ToClipboard();

        var buttonSize      = Im.Font.CalculateButtonSize("Turn Permanent"u8).X;
        var smallButtonSize = new Vector2((buttonSize - Im.Style.ItemInnerSpacing.X) / 2, 0);
        Im.Line.Same(Im.ContentRegion.Available.X
          - (2 * Im.Style.FrameHeight + 2 * Im.Style.ItemInnerSpacing.X + Im.Style.ItemSpacing.X + 250 * Im.Style.GlobalScale + buttonSize));
        if (ImEx.Icon.Button(LunaStyle.SaveIcon, "Save the current settings as a new preset for this mod."u8))
            Im.Popup.Open("presetName"u8);

        Im.Line.SameInner();
        presets.Draw(StringU8.Empty, 250 * Im.Style.GlobalScale);
        Im.Line.SameInner();

        using (ImGuiColor.Button.Push(ImGuiColor.ButtonActive.Vector, _editPresetMode && presets.Selected is not null))
        {
            if (ImEx.Icon.Button(LunaStyle.EditIcon, "Edit the currently selected preset."u8, presets.Selected is null))
                _editPresetMode ^= true;
        }

        Im.Line.Same();
        if (ImEx.Button("Apply"u8, smallButtonSize, "Apply this preset to the current collection. This respects temporary settings mode."u8,
                _locked || presets.Selected is null))
        {
            collectionManager.Editor.ApplyPreset(collectionManager.Active.Current, selection.Mod!, presets.Selected!.Data,
                config.Main.DefaultTemporaryMode || _temporary);
            presets.PresetManager.ChangeLastApply(presets.ModIdentifier, presets.Selected!, DateTimeOffset.UtcNow);
        }

        var hovered = Im.Item.Hovered();

        Im.Line.SameInner();
        if (ImEx.Button("Copy"u8, smallButtonSize, "Copy this preset's data to your clipboard to share."u8, presets.Selected is null))
        {
            presets.Selected!.Data.ToClipboard();
            presets.PresetManager.ChangeLastApply(presets.ModIdentifier, presets.Selected!, DateTimeOffset.UtcNow);
        }

        if (hovered || Im.Item.Hovered())
        {
            using var _  = Im.Style.PushDefault();
            using var tt = Im.Tooltip.Begin();
            LunaStyle.DrawSeparator();

            presets.DrawTooltip(presets.Selected!);
        }


        if (InputPopup.OpenName("presetName"u8, out var name))
            presets.PresetManager.AddPreset(selection.Mod!, selection.Settings, name);
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
                if (_temporary || config.Main.DefaultTemporaryMode)
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

        LunaStyle.DrawAlignedHelpMarkerLabel("Priority"u8, "Mods with a higher number here take precedence before Mods with a lower number.\n"u8
          + "That means, if Mod A should overwrite changes from Mod B, Mod A should have a higher priority number than Mod B."u8);
    }

    /// <summary>
    /// Draw a button to remove the current settings and inherit them instead
    /// in the top-right corner of the window/tab.
    /// </summary>
    private void DrawRemoveSettings()
    {
        var drawInherited = !selection.Inherited && !selection.Settings.IsEmpty;
        var buttonSize    = Im.Font.CalculateButtonSize("Turn Permanent"u8).X;
        var offset = drawInherited
            ? buttonSize + Im.Font.CalculateButtonSize("Inherit Settings"u8).X + Im.Style.ItemSpacing.X
            : buttonSize;
        Im.Line.Same(Im.ContentRegion.Available.X - offset);
        var enabled = LunaStyle.Modifier.Destructive.Active;
        if (drawInherited)
        {
            var inherit = (enabled, _locked) switch
            {
                (true, false) => ImEx.Button("Inherit Settings"u8,
                    "Remove current settings from this collection so that it can inherit them.\n"u8
                  + "If no inherited collection has settings for this mod, it will be disabled."u8),
                (false, false) => ImEx.Button("Inherit Settings"u8, default,
                    $"Remove current settings from this collection so that it can inherit them.\nHold {LunaStyle.Modifier.Destructive} to inherit.",
                    true),
                (_, true) => ImEx.Button("Inherit Settings"u8, default,
                    "Remove current settings from this collection so that it can inherit them.\nThe settings are currently locked and can not be changed."u8,
                    true),
            };
            if (inherit)
            {
                if (_temporary || config.Main.DefaultTemporaryMode)
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
                    $"Overwrite the actual settings for this mod in this collection with the current temporary settings.\nHold {LunaStyle.Modifier.Destructive} to overwrite.",
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
            if (ImEx.Button("Turn Temporary"u8, new Vector2(buttonSize, 0),
                    "Copy the current settings over to temporary settings to experiment with them."u8))
                collectionManager.Editor.SetTemporarySettings(collectionManager.Active.Current, selection.Mod!,
                    new TemporaryModSettings(selection.Mod!, actual));
        }
    }


    private string _groupNameInput        = string.Empty;
    private string _optionNameInput       = string.Empty;
    private Guid   _groupIdentifierInput  = Guid.Empty;
    private Guid   _optionIdentifierInput = Guid.Empty;

    private void DrawEditPresetMode()
    {
        if (presets.Selected is not { } preset || selection.Mod is not { } mod)
            return;

        Im.Line.New();
        ImEx.TextCentered($"Editing {(presets.ModIdentifier.Length > 0 ? "Mod" : "Generic")} Preset {preset.Name}");
        LunaStyle.DrawSeparator();

        var size = UiHelpers.InputTextWidth;
        preset.DrawIdentifier(size);
        if (preset.DrawName(size, out var newName))
            presets.PresetManager.ChangeName(presets.ModIdentifier, preset, newName);
        if (preset.DrawEditTime(size, out var newTime))
            presets.PresetManager.ChangeLastEdit(presets.ModIdentifier, preset, newTime);
        if (preset.DrawApplicationTime(size, out newTime))
            presets.PresetManager.ChangeLastApply(presets.ModIdentifier, preset, newTime);
        if (presets.ModIdentifier.Length > 0)
            if (ImEx.Button("Turn Mod Preset Generic"u8, size,
                    "Remove all GUIDs from this preset and keep only references by name to make this preset generically applicable on any mod, remove it from your mod preset list and add it to your generic preset list."u8))
                presets.PresetManager.MakeGeneric(preset, selection.Mod);

        if (ImEx.Button("Delete"u8, size, !LunaStyle.Modifier.Destructive))
        {
            if (presets.ModIdentifier.Length > 0)
                presets.PresetManager.DeletePreset(mod, preset);
            else
                presets.PresetManager.DeleteGeneric(preset);
        }

        LunaStyle.Modifier.Destructive.TooltipLineBreak("delete"u8);

        var halfSize = new Vector2((size.X - Im.Style.ItemSpacing.X) / 2, 0);
        if (ImEx.Button("Update From Clipboard"u8, halfSize, "Try to set this preset to the preset data contained in your clipboard."u8,
                !LunaStyle.Modifier.Misclick)
         && SettingPresetData.FromClipboard(out var sharedData))
            presets.PresetManager.Update(presets.ModIdentifier, preset, sharedData);
        LunaStyle.Modifier.Misclick.TooltipLineBreak("update"u8);

        Im.Line.Same();
        if (ImEx.Button("Update From Mod"u8, halfSize, "Try to set this preset to the current settings for this mod."u8,
                !LunaStyle.Modifier.Misclick))
            presets.PresetManager.Update(presets.ModIdentifier, preset, mod, selection.Settings);
        LunaStyle.Modifier.Misclick.TooltipLineBreak("update"u8);

        LunaStyle.DrawSeparator();
        if (preset.Data.DrawState(size, out var newState))
            presets.PresetManager.ChangeState(presets.ModIdentifier, preset, newState);
        if (preset.Data.DrawPriority(size, out var newPriority))
            presets.PresetManager.ChangePriority(presets.ModIdentifier, preset, newPriority);

        if (DrawGroups(size, preset, mod, out var changedGroupIdentifier, out var newGroupIdentifier, out var newDisableUnknown))
        {
            if (newDisableUnknown.HasValue)
                presets.PresetManager.ChangeDisableUnknownOptions(presets.ModIdentifier, preset, changedGroupIdentifier,
                    newDisableUnknown.Value);
            else if (newGroupIdentifier is null)
                presets.PresetManager.DeleteGroupReference(presets.ModIdentifier, preset, changedGroupIdentifier);
            else
                presets.PresetManager.ChangeGroupReference(presets.ModIdentifier, preset, changedGroupIdentifier, newGroupIdentifier.Value);
        }

        LunaStyle.DrawSeparator();
        var id = new ModObjectIdentifier(_groupIdentifierInput, _groupNameInput);
        if (preset.Data.DrawAddGroup(size, ref _groupIdentifierInput, ref _groupNameInput, out var newGroup,
                id.FindGroup(mod) is { } g ? ModObjectIdentifier.From(g) : null))
            presets.PresetManager.AddGroupReference(presets.ModIdentifier, preset, newGroup);
    }

    private bool DrawGroups(Vector2 size, SettingPreset preset, Mod mod, out ModObjectIdentifier changedGroupIdentifier,
        out ModObjectIdentifier? newGroupIdentifier, out bool? newDisableUnknown)
    {
        var ret = false;
        changedGroupIdentifier = default;
        newGroupIdentifier     = null;
        newDisableUnknown      = null;
        foreach (var (index, (groupIdentifier, groupData)) in preset.Data.Settings.Index())
        {
            using var groupId = Im.Id.Push(index);
            LunaStyle.DrawSeparator();
            var actualGroup = groupIdentifier.FindGroup(mod);
            if (SettingPresetData.DrawGroup(size, index, groupIdentifier,
                    actualGroup is not null ? ModObjectIdentifier.From(actualGroup) : null, groupData.DisableAllUnknown, out var newGroup,
                    out var disable))
            {
                ret                    = true;
                changedGroupIdentifier = groupIdentifier;
                newGroupIdentifier     = newGroup;
                newDisableUnknown      = disable;
            }

            using var indent = Im.Indent();
            if (DrawOptions(size.AddX(-Im.Style.IndentSpacing), groupData, actualGroup, out var changedOption, out var newOption,
                    out var newOptionState))
            {
                if (newOptionState.HasValue)
                    presets.PresetManager.ChangeOption(presets.ModIdentifier, preset, groupIdentifier, changedOption,
                        newOptionState.Value);
                else if (newOption is null)
                    presets.PresetManager.DeleteOptionReference(presets.ModIdentifier, preset, groupIdentifier, changedOption);
                else
                    presets.PresetManager.ChangeOptionReference(presets.ModIdentifier, preset, groupIdentifier, changedOption,
                        newOption.Value);
            }

            Im.Cursor.Y += Im.Style.ItemSpacing.Y;
            var id = new ModObjectIdentifier(_optionIdentifierInput, _optionNameInput);
            if (SettingPresetData.DrawAddOption(size.AddX(-Im.Style.IndentSpacing), groupData, ref _optionIdentifierInput, ref _optionNameInput,
                    out var option,
                    id.FindOption(actualGroup) is { } o ? ModObjectIdentifier.From(o) : null))
                presets.PresetManager.ChangeOption(presets.ModIdentifier, preset, groupIdentifier, option, OptionState.Ignored);
        }

        return ret;
    }

    private static bool DrawOptions(Vector2 size, in GroupSettingData data, IModGroup? actualGroup,
        out ModObjectIdentifier changedOptionIdentifier, out ModObjectIdentifier? newOptionIdentifier, out OptionState? newOptionState)
    {
        var ret = false;
        changedOptionIdentifier = default;
        newOptionIdentifier     = null;
        newOptionState          = null;
        foreach (var (optionIndex, (optionIdentifier, optionState)) in data.Options.Index())
        {
            using var id             = Im.Id.Push(optionIndex);
            var       resolvedOption = optionIdentifier.FindOption(actualGroup);
            Im.Cursor.Y += Im.Style.ItemSpacing.Y;
            if (SettingPresetData.DrawOption(size, optionIndex, optionIdentifier,
                    resolvedOption is not null ? ModObjectIdentifier.From(resolvedOption) : null,
                    (OptionState)optionState, out var newOption, out var state))
            {
                ret                     = true;
                changedOptionIdentifier = optionIdentifier;
                newOptionIdentifier     = newOption;
                newOptionState          = state;
            }
        }

        return ret;
    }
}
