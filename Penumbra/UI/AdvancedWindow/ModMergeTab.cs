using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using ImSharp;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Mods.SubMods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class ModMergeTab(ModMerger modMerger, ModComboWithoutCurrent combo) : Luna.IUiService
{
    private string _newModName = string.Empty;

    public void Draw()
    {
        if (modMerger.MergeFromMod == null)
            return;

        using var tab = ImRaii.TabItem("Merge Mods");
        if (!tab)
            return;

        ImGui.Dummy(Vector2.One);
        var size = 550 * ImGuiHelpers.GlobalScale;
        DrawMergeInto(size);
        Im.Line.Same();
        DrawMergeIntoDesc();

        ImGui.Dummy(Vector2.One);
        ImGui.Separator();
        ImGui.Dummy(Vector2.One);

        DrawSplitOff(size);
        Im.Line.Same();
        DrawSplitOffDesc();


        DrawError();
        DrawWarnings();
    }

    private void DrawMergeInto(float size)
    {
        using var bigGroup     = ImRaii.Group();
        var       minComboSize = 300 * ImGuiHelpers.GlobalScale;
        var       textSize     = ImUtf8.CalcTextSize($"Merge {modMerger.MergeFromMod!.Name} into ").X;

        ImGui.AlignTextToFramePadding();

        using (ImRaii.Group())
        {
            ImUtf8.Text("Merge "u8);
            ImGui.SameLine(0, 0);
            if (size - textSize < minComboSize)
            {
                Im.Text("selected mod"u8, ColorId.FolderLine.Value());
                ImUtf8.HoverTooltip(modMerger.MergeFromMod!.Name);
            }
            else
            {
                Im.Text(modMerger.MergeFromMod!.Name, ColorId.FolderLine.Value());
            }

            ImGui.SameLine(0, 0);
            ImUtf8.Text(" into"u8);
        }

        Im.Line.Same();
        DrawCombo(size - ImGui.GetItemRectSize().X - ImGui.GetStyle().ItemSpacing.X);

        using (ImRaii.Group())
        {
            using var disabled    = ImRaii.Disabled(modMerger.MergeFromMod.HasOptions);
            var       buttonWidth = (size - ImGui.GetStyle().ItemSpacing.X) / 2;
            using var style       = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 1);
            var       group       = modMerger.MergeToMod?.Groups.FirstOrDefault(g => g.Name == modMerger.OptionGroupName);
            var color = group != null || modMerger.OptionGroupName.Length == 0 && modMerger.OptionName.Length == 0
                ? Colors.PressEnterWarningBg
                : Colors.DiscordColor;
            using var c = ImGuiColor.Border.Push(color);
            ImGui.SetNextItemWidth(buttonWidth);
            ImGui.InputTextWithHint("##optionGroupInput", "Target Option Group", ref modMerger.OptionGroupName, 64);
            ImGuiUtil.HoverTooltip(
                "The name of the new or existing option group to find or create the option in. Leave both group and option name blank for the default option.\n"
              + "A red border indicates an existing option group, a blue border indicates a new one.");
            Im.Line.Same();


            color = color == Colors.DiscordColor
                ? Colors.DiscordColor
                : group == null || group.Options.Any(o => o.Name == modMerger.OptionName)
                    ? Colors.PressEnterWarningBg
                    : Colors.DiscordColor;
            c.Push(ImGuiColor.Border, color);
            ImGui.SetNextItemWidth(buttonWidth);
            ImGui.InputTextWithHint("##optionInput", "Target Option Name", ref modMerger.OptionName, 64);
            ImGuiUtil.HoverTooltip(
                "The name of the new or existing option to merge this mod into. Leave both group and option name blank for the default option.\n"
              + "A red border indicates an existing option, a blue border indicates a new one.");
        }

        if (modMerger.MergeFromMod.HasOptions)
            ImGuiUtil.HoverTooltip("You can only specify a target option if the source mod has no true options itself.",
                ImGuiHoveredFlags.AllowWhenDisabled);

        if (ImGuiUtil.DrawDisabledButton("Merge", new Vector2(size, 0),
                modMerger.CanMerge ? string.Empty : "Please select a target mod different from the current mod.", !modMerger.CanMerge))
            modMerger.Merge();
    }

    private void DrawMergeIntoDesc()
    {
        ImGuiUtil.TextWrapped(modMerger.MergeFromMod!.HasOptions
            ? "The currently selected mod has options.\n\nThis means, that all of those options will be merged into the target. If merging an option is not possible due to the redirections already existing in an existing option, it will revert all changes and break."
            : "The currently selected mod has no true options.\n\nThis means that you can select an existing or new option to merge all its changes into in the target mod. On failure to merge into an existing option, all changes will be reverted.");
    }

    private void DrawCombo(float width)
    {
        if (combo.Draw("##ModSelection"u8, modMerger.MergeToMod?.Name ?? "Select the target Mod...", StringU8.Empty, width,
                out var cacheMod))
            modMerger.MergeToMod = cacheMod.Item;
    }

    private void DrawSplitOff(float size)
    {
        using var group = ImRaii.Group();
        ImGui.SetNextItemWidth(size);
        ImGui.InputTextWithHint("##newModInput", "New Mod Name...", ref _newModName, 64);
        ImGuiUtil.HoverTooltip("Choose a name for the newly created mod. This does not need to be unique.");
        var tt = _newModName.Length == 0
            ? "Please enter a name for the newly created mod first."
            : modMerger.SelectedOptions.Count == 0
                ? "Please select at least one option to split off."
                : string.Empty;
        var buttonText =
            $"Split Off {modMerger.SelectedOptions.Count} Option{(modMerger.SelectedOptions.Count > 1 ? "s" : string.Empty)}###SplitOff";
        if (ImGuiUtil.DrawDisabledButton(buttonText, new Vector2(size, 0), tt, tt.Length > 0))
            modMerger.SplitIntoMod(_newModName);

        ImGui.Dummy(Vector2.One);
        var buttonSize = new Vector2((size - 2 * ImGui.GetStyle().ItemSpacing.X) / 3, 0);
        if (ImGui.Button("Select All", buttonSize))
            modMerger.SelectedOptions.UnionWith(modMerger.MergeFromMod!.AllDataContainers);
        Im.Line.Same();
        if (ImGui.Button("Unselect All", buttonSize))
            modMerger.SelectedOptions.Clear();
        Im.Line.Same();
        if (ImGui.Button("Invert Selection", buttonSize))
            modMerger.SelectedOptions.SymmetricExceptWith(modMerger.MergeFromMod!.AllDataContainers);
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
        var options = modMerger.MergeFromMod!.AllDataContainers.ToList();
        var height = modMerger.Warnings.Count == 0 && modMerger.Error == null
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
        foreach (var (idx, option) in options.Index())
        {
            using var id       = ImRaii.PushId(idx);
            var       selected = modMerger.SelectedOptions.Contains(option);

            ImGui.TableNextColumn();
            if (ImGui.Checkbox("##check", ref selected))
                Handle(option, selected);

            if (option.Group is not { } group)
            {
                ImGuiUtil.DrawTableColumn(option.GetFullName());
                ImGui.TableNextColumn();
            }
            else
            {
                ImGuiUtil.DrawTableColumn(option.GetName());

                ImGui.TableNextColumn();
                ImGui.Selectable(group.Name, false);
                if (ImGui.BeginPopupContextItem("##groupContext"))
                {
                    if (ImGui.MenuItem("Select All"))
                        // ReSharper disable once PossibleMultipleEnumeration
                        foreach (var opt in group.DataContainers)
                            Handle(opt, true);

                    if (ImGui.MenuItem("Unselect All"))
                        // ReSharper disable once PossibleMultipleEnumeration
                        foreach (var opt in group.DataContainers)
                            Handle(opt, false);
                    ImGui.EndPopup();
                }
            }

            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(option.Files.Count.ToString(), 3 * ImGuiHelpers.GlobalScale);
            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(option.FileSwaps.Count.ToString(), 3 * ImGuiHelpers.GlobalScale);
            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(option.Manipulations.Count.ToString(), 3 * ImGuiHelpers.GlobalScale);
            continue;

            void Handle(IModDataContainer option2, bool selected2)
            {
                if (selected2)
                    modMerger.SelectedOptions.Add(option2);
                else
                    modMerger.SelectedOptions.Remove(option2);
            }
        }
    }

    private void DrawWarnings()
    {
        if (modMerger.Warnings.Count == 0)
            return;

        ImGui.Separator();
        ImGui.Dummy(Vector2.One);
        using var color = ImGuiColor.Text.Push(Colors.TutorialBorder);
        foreach (var warning in modMerger.Warnings.SkipLast(1))
        {
            ImGuiUtil.TextWrapped(warning);
            ImGui.Separator();
        }

        ImGuiUtil.TextWrapped(modMerger.Warnings[^1]);
    }

    private void DrawError()
    {
        if (modMerger.Error == null)
            return;

        ImGui.Separator();
        ImGui.Dummy(Vector2.One);
        using var color = ImGuiColor.Text.Push(Colors.RegexWarningBorder);
        ImGuiUtil.TextWrapped(modMerger.Error.ToString());
    }
}
