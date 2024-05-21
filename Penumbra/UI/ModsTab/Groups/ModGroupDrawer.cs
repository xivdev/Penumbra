using Dalamud.Interface.Components;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;

namespace Penumbra.UI.ModsTab.Groups;

public sealed class ModGroupDrawer(Configuration config, CollectionManager collectionManager) : IUiService
{
    private readonly List<(IModGroup, int)> _blockGroupCache = [];

    public void Draw(Mod mod, ModSettings settings)
    {
        if (mod.Groups.Count <= 0)
            return;

        _blockGroupCache.Clear();
        var useDummy = true;
        foreach (var (group, idx) in mod.Groups.WithIndex())
        {
            if (!group.IsOption)
                continue;

            switch (group.Behaviour)
            {
                case GroupDrawBehaviour.SingleSelection when group.Options.Count <= config.SingleGroupRadioMax:
                case GroupDrawBehaviour.MultiSelection:
                    _blockGroupCache.Add((group, idx));
                    break;

                case GroupDrawBehaviour.SingleSelection:
                    ImGuiUtil.Dummy(UiHelpers.DefaultSpace, useDummy);
                    useDummy = false;
                    DrawSingleGroupCombo(group, idx, settings == ModSettings.Empty ? group.DefaultSettings : settings.Settings[idx]);
                    break;
            }
        }

        useDummy = true;
        foreach (var (group, idx) in _blockGroupCache)
        {
            ImGuiUtil.Dummy(UiHelpers.DefaultSpace, useDummy);
            useDummy = false;
            var option = settings == ModSettings.Empty ? group.DefaultSettings : settings.Settings[idx];
            if (group.Behaviour is GroupDrawBehaviour.MultiSelection)
                DrawMultiGroup(group, idx, option);
            else
                DrawSingleGroupRadio(group, idx, option);
        }
    }

    /// <summary>
    /// Draw a single group selector as a combo box.
    /// If a description is provided, add a help marker besides it.
    /// </summary>
    private void DrawSingleGroupCombo(IModGroup group, int groupIdx, Setting setting)
    {
        using var id = ImRaii.PushId(groupIdx);
        var selectedOption = setting.AsIndex;
        ImGui.SetNextItemWidth(UiHelpers.InputTextWidth.X * 3 / 4);
        var options = group.Options;
        using (var combo = ImRaii.Combo(string.Empty, options[selectedOption].Name))
        {
            if (combo)
                for (var idx2 = 0; idx2 < options.Count; ++idx2)
                {
                    id.Push(idx2);
                    var option = options[idx2];
                    if (ImGui.Selectable(option.Name, idx2 == selectedOption))
                        SetModSetting(group, groupIdx, Setting.Single(idx2));

                    if (option.Description.Length > 0)
                        ImGuiUtil.SelectableHelpMarker(option.Description);

                    id.Pop();
                }
        }

        ImGui.SameLine();
        if (group.Description.Length > 0)
            ImGuiUtil.LabeledHelpMarker(group.Name, group.Description);
        else
            ImGui.TextUnformatted(group.Name);
    }

    /// <summary>
    /// Draw a single group selector as a set of radio buttons.
    /// If a description is provided, add a help marker besides it.
    /// </summary>
    private void DrawSingleGroupRadio(IModGroup group, int groupIdx, Setting setting)
    {
        using var id = ImRaii.PushId(groupIdx);
        var selectedOption = setting.AsIndex;
        var minWidth = Widget.BeginFramedGroup(group.Name, group.Description);
        var options = group.Options;
        DrawCollapseHandling(options, minWidth, DrawOptions);

        Widget.EndFramedGroup();
        return;

        void DrawOptions()
        {
            for (var idx = 0; idx < group.Options.Count; ++idx)
            {
                using var i = ImRaii.PushId(idx);
                var option = options[idx];
                if (ImGui.RadioButton(option.Name, selectedOption == idx))
                    SetModSetting(group, groupIdx, Setting.Single(idx));

                if (option.Description.Length <= 0)
                    continue;

                ImGui.SameLine();
                ImGuiComponents.HelpMarker(option.Description);
            }
        }
    }

    /// <summary>
    /// Draw a multi group selector as a bordered set of checkboxes.
    /// If a description is provided, add a help marker in the title.
    /// </summary>
    private void DrawMultiGroup(IModGroup group, int groupIdx, Setting setting)
    {
        using var id = ImRaii.PushId(groupIdx);
        var minWidth = Widget.BeginFramedGroup(group.Name, group.Description);
        var options = group.Options;
        DrawCollapseHandling(options, minWidth, DrawOptions);

        Widget.EndFramedGroup();
        var label = $"##multi{groupIdx}";
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup($"##multi{groupIdx}");

        DrawMultiPopup(group, groupIdx, label);
        return;

        void DrawOptions()
        {
            for (var idx = 0; idx < options.Count; ++idx)
            {
                using var i = ImRaii.PushId(idx);
                var option = options[idx];
                var enabled = setting.HasFlag(idx);

                if (ImGui.Checkbox(option.Name, ref enabled))
                    SetModSetting(group, groupIdx, setting.SetBit(idx, enabled));

                if (option.Description.Length > 0)
                {
                    ImGui.SameLine();
                    ImGuiComponents.HelpMarker(option.Description);
                }
            }
        }
    }

    private void DrawMultiPopup(IModGroup group, int groupIdx, string label)
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1);
        using var popup = ImRaii.Popup(label);
        if (!popup)
            return;

        ImGui.TextUnformatted(group.Name);
        ImGui.Separator();
        if (ImGui.Selectable("Enable All"))
            SetModSetting(group, groupIdx, Setting.AllBits(group.Options.Count));

        if (ImGui.Selectable("Disable All"))
            SetModSetting(group, groupIdx, Setting.Zero);
    }

    private void DrawCollapseHandling(IReadOnlyList<IModOption> options, float minWidth, Action draw)
    {
        if (options.Count <= config.OptionGroupCollapsibleMin)
        {
            draw();
        }
        else
        {
            var collapseId = ImGui.GetID("Collapse");
            var shown = ImGui.GetStateStorage().GetBool(collapseId, true);
            var buttonTextShow = $"Show {options.Count} Options";
            var buttonTextHide = $"Hide {options.Count} Options";
            var buttonWidth = Math.Max(ImGui.CalcTextSize(buttonTextShow).X, ImGui.CalcTextSize(buttonTextHide).X)
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


                var width = Math.Max(ImGui.GetItemRectSize().X, minWidth);
                var endPos = ImGui.GetCursorPos();
                ImGui.SetCursorPos(pos);
                if (ImGui.Button(buttonTextHide, new Vector2(width, 0)))
                    ImGui.GetStateStorage().SetBool(collapseId, !shown);

                ImGui.SetCursorPos(endPos);
            }
            else
            {
                var optionWidth = options.Max(o => ImGui.CalcTextSize(o.Name).X)
                  + ImGui.GetStyle().ItemInnerSpacing.X
                  + ImGui.GetFrameHeight()
                  + ImGui.GetStyle().FramePadding.X;
                var width = Math.Max(optionWidth, minWidth);
                if (ImGui.Button(buttonTextShow, new Vector2(width, 0)))
                    ImGui.GetStateStorage().SetBool(collapseId, !shown);
            }
        }
    }

    private ModCollection Current
        => collectionManager.Active.Current;

    private void SetModSetting(IModGroup group, int groupIdx, Setting setting)
        => collectionManager.Editor.SetModSetting(Current, group.Mod, groupIdx, setting);
}
