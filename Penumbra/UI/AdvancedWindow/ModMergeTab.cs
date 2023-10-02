using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Subclasses;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class ModMergeTab
{
    private readonly ModMerger _modMerger;
    private readonly ModCombo  _modCombo;

    private string _newModName = string.Empty;

    public ModMergeTab(ModMerger modMerger)
    {
        _modMerger = modMerger;
        _modCombo  = new ModCombo(() => _modMerger.ModsWithoutCurrent.ToList());
    }

    public void Draw()
    {
        if (_modMerger.MergeFromMod == null)
            return;

        using var tab = ImRaii.TabItem("Merge Mods");
        if (!tab)
            return;

        ImGui.Dummy(Vector2.One);
        var size = 550 * ImGuiHelpers.GlobalScale;
        DrawMergeInto(size);
        ImGui.SameLine();
        DrawMergeIntoDesc();

        ImGui.Dummy(Vector2.One);
        ImGui.Separator();
        ImGui.Dummy(Vector2.One);

        DrawSplitOff(size);
        ImGui.SameLine();
        DrawSplitOffDesc();


        DrawError();
        DrawWarnings();
    }

    private void DrawMergeInto(float size)
    {
        using var bigGroup = ImRaii.Group();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"Merge {_modMerger.MergeFromMod!.Name} into ");
        ImGui.SameLine();
        DrawCombo(size - ImGui.GetItemRectSize().X - ImGui.GetStyle().ItemSpacing.X);

        var width = ImGui.GetItemRectSize();
        using (var g = ImRaii.Group())
        {
            using var disabled    = ImRaii.Disabled(_modMerger.MergeFromMod.HasOptions);
            var       buttonWidth = (size - ImGui.GetStyle().ItemSpacing.X) / 2;
            using var style       = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1);
            var       group       = _modMerger.MergeToMod?.Groups.FirstOrDefault(g => g.Name == _modMerger.OptionGroupName);
            var color = group != null || _modMerger.OptionGroupName.Length == 0 && _modMerger.OptionName.Length == 0
                ? Colors.PressEnterWarningBg
                : Colors.DiscordColor;
            using var c = ImRaii.PushColor(ImGuiCol.Border, color);
            ImGui.SetNextItemWidth(buttonWidth);
            ImGui.InputTextWithHint("##optionGroupInput", "Target Option Group", ref _modMerger.OptionGroupName, 64);
            ImGuiUtil.HoverTooltip(
                "The name of the new or existing option group to find or create the option in. Leave both group and option name blank for the default option.\n"
              + "A red border indicates an existing option group, a blue border indicates a new one.");
            ImGui.SameLine();


            color = color == Colors.DiscordColor
                ? Colors.DiscordColor
                : group == null || group.Any(o => o.Name == _modMerger.OptionName)
                    ? Colors.PressEnterWarningBg
                    : Colors.DiscordColor;
            c.Push(ImGuiCol.Border, color);
            ImGui.SetNextItemWidth(buttonWidth);
            ImGui.InputTextWithHint("##optionInput", "Target Option Name", ref _modMerger.OptionName, 64);
            ImGuiUtil.HoverTooltip(
                "The name of the new or existing option to merge this mod into. Leave both group and option name blank for the default option.\n"
              + "A red border indicates an existing option, a blue border indicates a new one.");
        }

        if (_modMerger.MergeFromMod.HasOptions)
            ImGuiUtil.HoverTooltip("You can only specify a target option if the source mod has no true options itself.",
                ImGuiHoveredFlags.AllowWhenDisabled);

        if (ImGuiUtil.DrawDisabledButton("Merge", new Vector2(size, 0),
                _modMerger.CanMerge ? string.Empty : "Please select a target mod different from the current mod.", !_modMerger.CanMerge))
            _modMerger.Merge();
    }

    private void DrawMergeIntoDesc()
    {
        ImGuiUtil.TextWrapped(_modMerger.MergeFromMod!.HasOptions
            ? "The currently selected mod has options.\n\nThis means, that all of those options will be merged into the target. If merging an option is not possible due to the redirections already existing in an existing option, it will revert all changes and break."
            : "The currently selected mod has no true options.\n\nThis means that you can select an existing or new option to merge all its changes into in the target mod. On failure to merge into an existing option, all changes will be reverted.");
    }

    private void DrawCombo(float width)
    {
        _modCombo.Draw("##ModSelection", _modCombo.CurrentSelection?.Name.Text ?? "Select the target Mod...", string.Empty, width,
            ImGui.GetTextLineHeight());
        _modMerger.MergeToMod = _modCombo.CurrentSelection;
    }

    private void DrawSplitOff(float size)
    {
        using var group = ImRaii.Group();
        ImGui.SetNextItemWidth(size);
        ImGui.InputTextWithHint("##newModInput", "New Mod Name...", ref _newModName, 64);
        ImGuiUtil.HoverTooltip("Choose a name for the newly created mod. This does not need to be unique.");
        var tt = _newModName.Length == 0
            ? "Please enter a name for the newly created mod first."
            : _modMerger.SelectedOptions.Count == 0
                ? "Please select at least one option to split off."
                : string.Empty;
        var buttonText =
            $"Split Off {_modMerger.SelectedOptions.Count} Option{(_modMerger.SelectedOptions.Count > 1 ? "s" : string.Empty)}###SplitOff";
        if (ImGuiUtil.DrawDisabledButton(buttonText, new Vector2(size, 0), tt, tt.Length > 0))
            _modMerger.SplitIntoMod(_newModName);

        ImGui.Dummy(Vector2.One);
        var buttonSize = new Vector2((size - 2 * ImGui.GetStyle().ItemSpacing.X) / 3, 0);
        if (ImGui.Button("Select All", buttonSize))
            _modMerger.SelectedOptions.UnionWith(_modMerger.MergeFromMod!.AllSubMods);
        ImGui.SameLine();
        if (ImGui.Button("Unselect All", buttonSize))
            _modMerger.SelectedOptions.Clear();
        ImGui.SameLine();
        if (ImGui.Button("Invert Selection", buttonSize))
            _modMerger.SelectedOptions.SymmetricExceptWith(_modMerger.MergeFromMod!.AllSubMods);
        DrawOptionTable(size);
    }

    private void DrawSplitOffDesc()
    {
        ImGuiUtil.TextWrapped("Here you can create a copy or a partial copy of the currently selected mod.\n\n"
          + "Select as many of the options you want to copy over, enter a new mod name and click Split Off.\n\n"
          + "You can right-click option groups to select or unselect all options from that specific group, and use the three buttons above the table for quick manipulation of your selection.\n\n"
          + "Only required files will be copied over to the new mod. The names of options and groups will be retained. If the Default option is not selected, the new mods default option will be empty.");
    }

    private void DrawOptionTable(float size)
    {
        var options = _modMerger.MergeFromMod!.AllSubMods.ToList();
        var height = _modMerger.Warnings.Count == 0 && _modMerger.Error == null
            ? ImGui.GetContentRegionAvail().Y - 3 * ImGui.GetFrameHeightWithSpacing()
            : 8 * ImGui.GetFrameHeightWithSpacing();
        height = Math.Min(height, (options.Count + 1) * ImGui.GetFrameHeightWithSpacing());
        var tableSize = new Vector2(size, height);
        using var table = ImRaii.Table("##options", 6,
            ImGuiTableFlags.RowBg
          | ImGuiTableFlags.SizingFixedFit
          | ImGuiTableFlags.ScrollY
          | ImGuiTableFlags.BordersOuterV
          | ImGuiTableFlags.BordersOuterH,
            tableSize);
        if (!table)
            return;

        ImGui.TableSetupColumn("##Selected",   ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("Option",       ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Option Group", ImGuiTableColumnFlags.WidthFixed, 120 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("#Files",       ImGuiTableColumnFlags.WidthFixed, 50 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("#Swaps",       ImGuiTableColumnFlags.WidthFixed, 50 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("#Manips",      ImGuiTableColumnFlags.WidthFixed, 50 * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();
        foreach (var (option, idx) in options.WithIndex())
        {
            using var id       = ImRaii.PushId(idx);
            var       selected = _modMerger.SelectedOptions.Contains(option);

            void Handle(SubMod option2, bool selected2)
            {
                if (selected2)
                    _modMerger.SelectedOptions.Add(option2);
                else
                    _modMerger.SelectedOptions.Remove(option2);
            }

            ImGui.TableNextColumn();
            if (ImGui.Checkbox("##check", ref selected))
                Handle(option, selected);

            if (option.IsDefault)
            {
                ImGuiUtil.DrawTableColumn(option.FullName);
                ImGui.TableNextColumn();
            }
            else
            {
                ImGuiUtil.DrawTableColumn(option.Name);
                var group = option.ParentMod.Groups[option.GroupIdx];
                ImGui.TableNextColumn();
                ImGui.Selectable(group.Name, false);
                if (ImGui.BeginPopupContextItem("##groupContext"))
                {
                    if (ImGui.MenuItem("Select All"))
                        foreach (var opt in group)
                            Handle((SubMod)opt, true);

                    if (ImGui.MenuItem("Unselect All"))
                        foreach (var opt in group)
                            Handle((SubMod)opt, false);
                    ImGui.EndPopup();
                }
            }

            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(option.FileData.Count.ToString(), 3 * ImGuiHelpers.GlobalScale);
            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(option.FileSwapData.Count.ToString(), 3 * ImGuiHelpers.GlobalScale);
            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(option.Manipulations.Count.ToString(), 3 * ImGuiHelpers.GlobalScale);
        }
    }

    private void DrawWarnings()
    {
        if (_modMerger.Warnings.Count == 0)
            return;

        ImGui.Separator();
        ImGui.Dummy(Vector2.One);
        using var color = ImRaii.PushColor(ImGuiCol.Text, Colors.TutorialBorder);
        foreach (var warning in _modMerger.Warnings.SkipLast(1))
        {
            ImGuiUtil.TextWrapped(warning);
            ImGui.Separator();
        }

        ImGuiUtil.TextWrapped(_modMerger.Warnings[^1]);
    }

    private void DrawError()
    {
        if (_modMerger.Error == null)
            return;

        ImGui.Separator();
        ImGui.Dummy(Vector2.One);
        using var color = ImRaii.PushColor(ImGuiCol.Text, Colors.RegexWarningBorder);
        ImGuiUtil.TextWrapped(_modMerger.Error.ToString());
    }
}
