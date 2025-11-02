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
        var size = 550 * Im.Style.GlobalScale;
        DrawMergeInto(size);
        Im.Line.Same();
        DrawMergeIntoDesc();

        ImGui.Dummy(Vector2.One);
        Im.Separator();
        ImGui.Dummy(Vector2.One);

        DrawSplitOff(size);
        Im.Line.Same();
        DrawSplitOffDesc();


        DrawError();
        DrawWarnings();
    }

    private void DrawMergeInto(float size)
    {
        using var bigGroup     = Im.Group();
        var       minComboSize = 300 * Im.Style.GlobalScale;
        var       textSize     = ImUtf8.CalcTextSize($"Merge {modMerger.MergeFromMod!.Name} into ").X;

        ImGui.AlignTextToFramePadding();

        using (Im.Group())
        {
            ImUtf8.Text("Merge "u8);
            Im.Line.Same(0, 0);
            if (size - textSize < minComboSize)
            {
                Im.Text("selected mod"u8, ColorId.FolderLine.Value());
                Im.Tooltip.OnHover(modMerger.MergeFromMod!.Name);
            }
            else
            {
                Im.Text(modMerger.MergeFromMod!.Name, ColorId.FolderLine.Value());
            }

            Im.Line.Same(0, 0);
            ImUtf8.Text(" into"u8);
        }

        Im.Line.Same();
        DrawCombo(size - ImGui.GetItemRectSize().X - Im.Style.ItemSpacing.X);

        using (Im.Group())
        {
            using var disabled    = ImRaii.Disabled(modMerger.MergeFromMod.HasOptions);
            var       buttonWidth = (size - Im.Style.ItemSpacing.X) / 2;
            var       group       = modMerger.MergeToMod?.Groups.FirstOrDefault(g => g.Name == modMerger.OptionGroupName);
            var color = group != null || modMerger.OptionGroupName.Length is 0 && modMerger.OptionName.Length is 0
                ? Colors.PressEnterWarningBg
                : Colors.DiscordColor;
            using var style = ImStyleBorder.Frame.Push(color);
            Im.Item.SetNextWidth(buttonWidth);
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
            style.Push(ImGuiColor.Border, color);
            Im.Item.SetNextWidth(buttonWidth);
            ImGui.InputTextWithHint("##optionInput", "Target Option Name", ref modMerger.OptionName, 64);
            ImGuiUtil.HoverTooltip(
                "The name of the new or existing option to merge this mod into. Leave both group and option name blank for the default option.\n"
              + "A red border indicates an existing option, a blue border indicates a new one.");
        }

        if (modMerger.MergeFromMod.HasOptions)
            Im.Tooltip.OnHover("You can only specify a target option if the source mod has no true options itself."u8,
                HoveredFlags.AllowWhenDisabled);

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
        using var group = Im.Group();
        Im.Item.SetNextWidth(size);
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
        var buttonSize = new Vector2((size - 2 * Im.Style.ItemSpacing.X) / 3, 0);
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
            ? Im.ContentRegion.Available.Y - 3 * Im.Style.FrameHeightWithSpacing
            : 8 * Im.Style.FrameHeightWithSpacing;
        height = Math.Min(height, (options.Count + 1) * Im.Style.FrameHeightWithSpacing);
        var tableSize = new Vector2(size, height);
        using var table = Im.Table.Begin("##options"u8, 6,
            TableFlags.RowBackground
          | TableFlags.SizingFixedFit
          | TableFlags.ScrollY
          | TableFlags.BordersOuterVertical
          | TableFlags.BordersOuterHorizontal,
            tableSize);
        if (!table)
            return;

        table.SetupColumn("##Selected"u8,   TableColumnFlags.WidthFixed, Im.Style.FrameHeight);
        table.SetupColumn("Option"u8,       TableColumnFlags.WidthStretch);
        table.SetupColumn("Option Group"u8, TableColumnFlags.WidthFixed, 120 * Im.Style.GlobalScale);
        table.SetupColumn("#Files"u8,       TableColumnFlags.WidthFixed, 50 * Im.Style.GlobalScale);
        table.SetupColumn("#Swaps"u8,       TableColumnFlags.WidthFixed, 50 * Im.Style.GlobalScale);
        table.SetupColumn("#Manips"u8,      TableColumnFlags.WidthFixed, 50 * Im.Style.GlobalScale);
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
            ImGuiUtil.RightAlign(option.Files.Count.ToString(), 3 * Im.Style.GlobalScale);
            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(option.FileSwaps.Count.ToString(), 3 * Im.Style.GlobalScale);
            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign(option.Manipulations.Count.ToString(), 3 * Im.Style.GlobalScale);
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

        Im.Separator();
        ImGui.Dummy(Vector2.One);
        using var color = ImGuiColor.Text.Push(Colors.TutorialBorder);
        foreach (var warning in modMerger.Warnings.SkipLast(1))
        {
            ImGuiUtil.TextWrapped(warning);
            Im.Separator();
        }

        ImGuiUtil.TextWrapped(modMerger.Warnings[^1]);
    }

    private void DrawError()
    {
        if (modMerger.Error == null)
            return;

        Im.Separator();
        ImGui.Dummy(Vector2.One);
        using var color = ImGuiColor.Text.Push(Colors.RegexWarningBorder);
        ImGuiUtil.TextWrapped(modMerger.Error.ToString());
    }
}
