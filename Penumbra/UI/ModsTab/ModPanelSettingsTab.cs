using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using OtterGui.Widgets;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Mods;
using Penumbra.UI.Classes;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ModsTab;

public class ModPanelSettingsTab : ITab
{
    private readonly Configuration         _config;
    private readonly CollectionManager _collectionManager;
    private readonly ModFileSystemSelector _selector;
    private readonly TutorialService       _tutorial;
    private readonly PenumbraApi           _api;
    private readonly ModManager           _modManager;

    private bool          _inherited;
    private ModSettings   _settings   = null!;
    private ModCollection _collection = null!;
    private bool          _empty;
    private int?          _currentPriority = null;

    public ModPanelSettingsTab(CollectionManager collectionManager, ModManager modManager, ModFileSystemSelector selector,
        TutorialService tutorial, PenumbraApi api, Configuration config)
    {
        _collectionManager = collectionManager;
        _modManager        = modManager;
        _selector          = selector;
        _tutorial          = tutorial;
        _api               = api;
        _config            = config;
    }

    public ReadOnlySpan<byte> Label
        => "Settings"u8;

    public void DrawHeader()
        => _tutorial.OpenTutorial(BasicTutorialSteps.ModOptions);

    public void Reset()
        => _currentPriority = null;

    public void DrawContent()
    {
        using var child = ImRaii.Child("##settings");
        if (!child)
            return;

        _settings   = _selector.SelectedSettings;
        _collection = _selector.SelectedSettingCollection;
        _inherited  = _collection != _collectionManager.Current;
        _empty      = _settings == ModSettings.Empty;

        DrawInheritedWarning();
        UiHelpers.DefaultLineSpace();
        _api.InvokePreSettingsPanel(_selector.Selected!.ModPath.Name);
        DrawEnabledInput();
        _tutorial.OpenTutorial(BasicTutorialSteps.EnablingMods);
        ImGui.SameLine();
        DrawPriorityInput();
        _tutorial.OpenTutorial(BasicTutorialSteps.Priority);
        DrawRemoveSettings();

        if (_selector.Selected!.Groups.Count > 0)
        {
            var useDummy = true;
            foreach (var (group, idx) in _selector.Selected!.Groups.WithIndex()
                         .Where(g => g.Value.Type == GroupType.Single && g.Value.Count > _config.SingleGroupRadioMax))
            {
                ImGuiUtil.Dummy(UiHelpers.DefaultSpace, useDummy);
                useDummy = false;
                DrawSingleGroupCombo(group, idx);
            }

            useDummy = true;
            foreach (var (group, idx) in _selector.Selected!.Groups.WithIndex().Where(g => g.Value.IsOption))
            {
                ImGuiUtil.Dummy(UiHelpers.DefaultSpace, useDummy);
                useDummy = false;
                switch (group.Type)
                {
                    case GroupType.Multi:
                        DrawMultiGroup(group, idx);
                        break;
                    case GroupType.Single when group.Count <= _config.SingleGroupRadioMax:
                        DrawSingleGroupRadio(group, idx);
                        break;
                }
            }
        }

        UiHelpers.DefaultLineSpace();
        _api.InvokePostSettingsPanel(_selector.Selected!.ModPath.Name);
    }

    /// <summary> Draw a big red bar if the current setting is inherited. </summary>
    private void DrawInheritedWarning()
    {
        if (!_inherited)
            return;

        using var color = ImRaii.PushColor(ImGuiCol.Button, Colors.PressEnterWarningBg);
        var       width = new Vector2(ImGui.GetContentRegionAvail().X, 0);
        if (ImGui.Button($"These settings are inherited from {_collection.Name}.", width))
            _collectionManager.Current.SetModInheritance(_selector.Selected!.Index, false);

        ImGuiUtil.HoverTooltip("You can click this button to copy the current settings to the current selection.\n"
          + "You can also just change any setting, which will copy the settings with the single setting changed to the current selection.");
    }

    /// <summary> Draw a checkbox for the enabled status of the mod. </summary>
    private void DrawEnabledInput()
    {
        var enabled = _settings.Enabled;
        if (!ImGui.Checkbox("Enabled", ref enabled))
            return;

        _modManager.SetKnown(_selector.Selected!);
        _collectionManager.Current.SetModState(_selector.Selected!.Index, enabled);
    }

    /// <summary>
    /// Draw a priority input.
    /// Priority is changed on deactivation of the input box.
    /// </summary>
    private void DrawPriorityInput()
    {
        using var group    = ImRaii.Group();
        var       priority = _currentPriority ?? _settings.Priority;
        ImGui.SetNextItemWidth(50 * UiHelpers.Scale);
        if (ImGui.InputInt("##Priority", ref priority, 0, 0))
            _currentPriority = priority;

        if (ImGui.IsItemDeactivatedAfterEdit() && _currentPriority.HasValue)
        {
            if (_currentPriority != _settings.Priority)
                _collectionManager.Current.SetModPriority(_selector.Selected!.Index, _currentPriority.Value);

            _currentPriority = null;
        }

        ImGuiUtil.LabeledHelpMarker("Priority", "Mods with a higher number here take precedence before Mods with a lower number.\n"
          + "That means, if Mod A should overwrite changes from Mod B, Mod A should have a higher priority number than Mod B.");
    }

    /// <summary>
    /// Draw a button to remove the current settings and inherit them instead
    /// on the top-right corner of the window/tab.
    /// </summary>
    private void DrawRemoveSettings()
    {
        const string text = "Inherit Settings";
        if (_inherited || _empty)
            return;

        var scroll = ImGui.GetScrollMaxY() > 0 ? ImGui.GetStyle().ScrollbarSize : 0;
        ImGui.SameLine(ImGui.GetWindowWidth() - ImGui.CalcTextSize(text).X - ImGui.GetStyle().FramePadding.X * 2 - scroll);
        if (ImGui.Button(text))
            _collectionManager.Current.SetModInheritance(_selector.Selected!.Index, true);

        ImGuiUtil.HoverTooltip("Remove current settings from this collection so that it can inherit them.\n"
          + "If no inherited collection has settings for this mod, it will be disabled.");
    }

    /// <summary>
    /// Draw a single group selector as a combo box.
    /// If a description is provided, add a help marker besides it.
    /// </summary>
    private void DrawSingleGroupCombo(IModGroup group, int groupIdx)
    {
        using var id             = ImRaii.PushId(groupIdx);
        var       selectedOption = _empty ? (int)group.DefaultSettings : (int)_settings.Settings[groupIdx];
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X * 3 / 4);
        using (var combo = ImRaii.Combo(string.Empty, group[selectedOption].Name))
        {
            if (combo)
                for (var idx2 = 0; idx2 < group.Count; ++idx2)
                {
                    id.Push(idx2);
                    var option = group[idx2];
                    if (ImGui.Selectable(option.Name, idx2 == selectedOption))
                        _collectionManager.Current.SetModSetting(_selector.Selected!.Index, groupIdx, (uint)idx2);

                    if (option.Description.Length > 0)
                    {
                        var hovered = ImGui.IsItemHovered();
                        ImGui.SameLine();
                        using (var _ = ImRaii.PushFont(UiBuilder.IconFont))
                        {
                            using var color = ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
                            ImGuiUtil.RightAlign(FontAwesomeIcon.InfoCircle.ToIconString(), ImGui.GetStyle().ItemSpacing.X);
                        }

                        if (hovered)
                        {
                            using var tt = ImRaii.Tooltip();
                            ImGui.TextUnformatted(option.Description);
                        }
                    }

                    id.Pop();
                }
        }

        ImGui.SameLine();
        if (group.Description.Length > 0)
            ImGuiUtil.LabeledHelpMarker(group.Name, group.Description);
        else
            ImGui.TextUnformatted(group.Name);
    }

    // Draw a single group selector as a set of radio buttons.
    // If a description is provided, add a help marker besides it.
    private void DrawSingleGroupRadio(IModGroup group, int groupIdx)
    {
        using var id             = ImRaii.PushId(groupIdx);
        var       selectedOption = _empty ? (int)group.DefaultSettings : (int)_settings.Settings[groupIdx];
        var minWidth = Widget.BeginFramedGroup(group.Name, group.Description);

        void DrawOptions()
        {
            for (var idx = 0; idx < group.Count; ++idx)
            {
                using var i      = ImRaii.PushId(idx);
                var       option = group[idx];
                if (ImGui.RadioButton(option.Name, selectedOption == idx))
                    _collectionManager.Current.SetModSetting(_selector.Selected!.Index, groupIdx, (uint)idx);

                if (option.Description.Length <= 0)
                    continue;

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(option.Description);
            }
        }

        DrawCollapseHandling(group, minWidth, DrawOptions);

        Widget.EndFramedGroup();
    }


    private void DrawCollapseHandling(IModGroup group, float minWidth, Action draw)
    {
        if (group.Count <= _config.OptionGroupCollapsibleMin)
        {
            draw();
        }
        else
        {
            var collapseId     = ImGui.GetID("Collapse");
            var shown          = ImGui.GetStateStorage().GetBool(collapseId, true);
            var buttonTextShow = $"Show {group.Count} Options";
            var buttonTextHide = $"Hide {group.Count} Options";
            var buttonWidth    = Math.Max(ImGui.CalcTextSize(buttonTextShow).X, ImGui.CalcTextSize(buttonTextHide).X) 
              + 2 * ImGui.GetStyle().FramePadding.X;
            minWidth = Math.Max(buttonWidth, minWidth);
            if (shown)
            {
                var pos = ImGui.GetCursorPos();
                ImGui.Dummy(UiHelpers.IconButtonSize);
                using (var _ = ImRaii.Group())
                {
                    draw();
                }

                
                
                var width       = Math.Max(ImGui.GetItemRectSize().X, minWidth);
                var endPos      = ImGui.GetCursorPos();
                ImGui.SetCursorPos(pos);
                if (ImGui.Button(buttonTextHide, new Vector2(width, 0)))
                    ImGui.GetStateStorage().SetBool(collapseId, !shown);

                ImGui.SetCursorPos(endPos);
            }
            else
            {
                var optionWidth = group.Max(o => ImGui.CalcTextSize(o.Name).X)
                  + ImGui.GetStyle().ItemInnerSpacing.X
                  + ImGui.GetFrameHeight()
                  + ImGui.GetStyle().FramePadding.X;
                var width        = Math.Max(optionWidth, minWidth);
                if (ImGui.Button(buttonTextShow, new Vector2(width, 0)))
                    ImGui.GetStateStorage().SetBool(collapseId, !shown);
            }
        }
    }

    /// <summary>
    /// Draw a multi group selector as a bordered set of checkboxes.
    /// If a description is provided, add a help marker in the title.
    /// </summary>
    private void DrawMultiGroup(IModGroup group, int groupIdx)
    {
        using var id    = ImRaii.PushId(groupIdx);
        var       flags = _empty ? group.DefaultSettings : _settings.Settings[groupIdx];
        var minWidth = Widget.BeginFramedGroup(group.Name, group.Description);

        void DrawOptions()
        {
            for (var idx = 0; idx < group.Count; ++idx)
            {
                using var i       = ImRaii.PushId(idx);
                var       option  = group[idx];
                var       flag    = 1u << idx;
                var       setting = (flags & flag) != 0;

                if (ImGui.Checkbox(option.Name, ref setting))
                {
                    flags = setting ? flags | flag : flags & ~flag;
                    _collectionManager.Current.SetModSetting(_selector.Selected!.Index, groupIdx, flags);
                }

                if (option.Description.Length > 0)
                {
                    ImGui.SameLine();
                    ImGuiComponents.HelpMarker(option.Description);
                }
            }
        }

        DrawCollapseHandling(group, minWidth, DrawOptions);

        Widget.EndFramedGroup();
        var label = $"##multi{groupIdx}";
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup($"##multi{groupIdx}");

        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1);
        using var popup = ImRaii.Popup(label);
        if (!popup)
            return;

        ImGui.TextUnformatted(group.Name);
        ImGui.Separator();
        if (ImGui.Selectable("Enable All"))
        {
            flags = group.Count == 32 ? uint.MaxValue : (1u << group.Count) - 1u;
            _collectionManager.Current.SetModSetting(_selector.Selected!.Index, groupIdx, flags);
        }

        if (ImGui.Selectable("Disable All"))
            _collectionManager.Current.SetModSetting(_selector.Selected!.Index, groupIdx, 0);
    }
}
