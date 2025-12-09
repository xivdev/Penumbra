using ImSharp;
using Luna;
using Penumbra.Mods.Editor;
using Penumbra.Mods.SubMods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class ModMergeTab(ModMerger modMerger, ModComboWithoutCurrent combo) : IUiService
{
    private string _newModName = string.Empty;

    public void Draw()
    {
        if (modMerger.MergeFromMod == null)
            return;

        using var tab = Im.TabBar.BeginItem("Merge Mods"u8);
        if (!tab)
            return;

        Im.Dummy(Vector2.Zero);
        var size = 550 * Im.Style.GlobalScale;
        DrawMergeInto(size);
        Im.Line.Same();
        DrawMergeIntoDesc();

        Im.Dummy(Vector2.Zero);
        Im.Separator();
        Im.Dummy(Vector2.Zero);

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
        var       textSize     = Im.Font.CalculateSize($"Merge {modMerger.MergeFromMod!.Name} into ").X;

        Im.Cursor.FrameAlign();

        using (Im.Group())
        {
            Im.Text("Merge "u8);
            Im.Line.NoSpacing();
            if (size - textSize < minComboSize)
            {
                Im.Text("selected mod"u8, ColorId.FolderLine.Value());
                Im.Tooltip.OnHover(modMerger.MergeFromMod!.Name);
            }
            else
            {
                Im.Text(modMerger.MergeFromMod!.Name, ColorId.FolderLine.Value());
            }

            Im.Line.NoSpacing();
            Im.Text(" into"u8);
        }

        Im.Line.Same();
        DrawCombo(size - Im.Item.Size.X - Im.Style.ItemSpacing.X);

        using (Im.Group())
        {
            using var disabled    = Im.Disabled(modMerger.MergeFromMod.HasOptions);
            var       buttonWidth = (size - Im.Style.ItemSpacing.X) / 2;
            var       group       = modMerger.MergeToMod?.Groups.FirstOrDefault(g => g.Name == modMerger.OptionGroupName);
            var color = group != null || modMerger.OptionGroupName.Length is 0 && modMerger.OptionName.Length is 0
                ? Colors.PressEnterWarningBg
                : LunaStyle.DiscordColor;
            using var style = ImStyleBorder.Frame.Push(color);
            Im.Item.SetNextWidth(buttonWidth);
            Im.Input.Text("##optionGroupInput"u8, ref modMerger.OptionGroupName, "Target Option Group"u8);
            Im.Tooltip.OnHover(
                "The name of the new or existing option group to find or create the option in. Leave both group and option name blank for the default option.\n"u8
              + "A red border indicates an existing option group, a blue border indicates a new one."u8);
            Im.Line.Same();


            color = color == LunaStyle.DiscordColor
                ? LunaStyle.DiscordColor
                : group == null || group.Options.Any(o => o.Name == modMerger.OptionName)
                    ? Colors.PressEnterWarningBg
                    : LunaStyle.DiscordColor;
            style.Push(ImGuiColor.Border, color);
            Im.Item.SetNextWidth(buttonWidth);
            Im.Input.Text("##optionInput"u8, ref modMerger.OptionName, "Target Option Name"u8);
            Im.Tooltip.OnHover(
                "The name of the new or existing option to merge this mod into. Leave both group and option name blank for the default option.\n"u8
              + "A red border indicates an existing option, a blue border indicates a new one."u8);
        }

        if (modMerger.MergeFromMod.HasOptions)
            Im.Tooltip.OnHover("You can only specify a target option if the source mod has no true options itself."u8,
                HoveredFlags.AllowWhenDisabled);

        if (ImEx.Button("Merge"u8, new Vector2(size, 0),
                modMerger.CanMerge ? StringU8.Empty : "Please select a target mod different from the current mod."u8, !modMerger.CanMerge))
            modMerger.Merge();
    }

    private void DrawMergeIntoDesc()
    {
        Im.TextWrapped(modMerger.MergeFromMod!.HasOptions
            ? "The currently selected mod has options.\n\nThis means, that all of those options will be merged into the target. If merging an option is not possible due to the redirections already existing in an existing option, it will revert all changes and break."u8
            : "The currently selected mod has no true options.\n\nThis means that you can select an existing or new option to merge all its changes into in the target mod. On failure to merge into an existing option, all changes will be reverted."u8);
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
        Im.Input.Text("##newModInput"u8, ref _newModName, "New Mod Name..."u8);
        Im.Tooltip.OnHover("Choose a name for the newly created mod. This does not need to be unique."u8);
        var tt = _newModName.Length == 0
            ? "Please enter a name for the newly created mod first."u8
            : modMerger.SelectedOptions.Count == 0
                ? "Please select at least one option to split off."u8
                : StringU8.Empty;
        if (ImEx.Button(
                $"Split Off {modMerger.SelectedOptions.Count} Option{(modMerger.SelectedOptions.Count > 1 ? "s" : string.Empty)}###SplitOff",
                new Vector2(size, 0), tt, tt.Length > 0))
            modMerger.SplitIntoMod(_newModName);

        Im.Dummy(Vector2.One);
        var buttonSize = new Vector2((size - 2 * Im.Style.ItemSpacing.X) / 3, 0);
        if (Im.Button("Select All"u8, buttonSize))
            modMerger.SelectedOptions.UnionWith(modMerger.MergeFromMod!.AllDataContainers);
        Im.Line.Same();
        if (Im.Button("Unselect All"u8, buttonSize))
            modMerger.SelectedOptions.Clear();
        Im.Line.Same();
        if (Im.Button("Invert Selection"u8, buttonSize))
            modMerger.SelectedOptions.SymmetricExceptWith(modMerger.MergeFromMod!.AllDataContainers);
        DrawOptionTable(size);
    }

    private static void DrawSplitOffDesc()
    {
        Im.TextWrapped("Here you can create a copy or a partial copy of the currently selected mod.\n\n"u8
          + "Select as many of the options you want to copy over, enter a new mod name and click Split Off.\n\n"u8
          + "You can right-click option groups to select or unselect all options from that specific group, and use the three buttons above the table for quick manipulation of your selection.\n\n"u8
          + "Only required files will be copied over to the new mod. The names of options and groups will be retained. If the Default option is not selected, the new mods default option will be empty."u8);
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
        table.HeaderRow();
        foreach (var (idx, option) in options.Index())
        {
            using var id       = Im.Id.Push(idx);
            var       selected = modMerger.SelectedOptions.Contains(option);

            table.NextColumn();
            if (Im.Checkbox("##check"u8, ref selected))
                Handle(option, selected);

            if (option.Group is not { } group)
            {
                table.DrawColumn(option.GetFullName());
                table.NextColumn();
            }
            else
            {
                table.DrawColumn(option.GetName());
                table.NextColumn();
                Im.Selectable(group.Name);
                using var popup = Im.Popup.BeginContextItem("##groupContext"u8);
                if (popup)
                {
                    if (Im.Menu.Item("Select All"u8))
                        // ReSharper disable once PossibleMultipleEnumeration
                        foreach (var opt in group.DataContainers)
                            Handle(opt, true);

                    if (Im.Menu.Item("Unselect All"u8))
                        // ReSharper disable once PossibleMultipleEnumeration
                        foreach (var opt in group.DataContainers)
                            Handle(opt, false);
                }
            }

            table.NextColumn();
            ImEx.TextRightAligned($"{option.Files.Count}", 3 * Im.Style.GlobalScale);
            table.NextColumn();
            ImEx.TextRightAligned($"{option.FileSwaps.Count}", 3 * Im.Style.GlobalScale);
            table.NextColumn();
            ImEx.TextRightAligned($"{option.Manipulations.Count}", 3 * Im.Style.GlobalScale);
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
        Im.Dummy(Vector2.One);
        using var color = ImGuiColor.Text.Push(Colors.TutorialBorder);
        foreach (var warning in modMerger.Warnings.SkipLast(1))
        {
            Im.TextWrapped(warning);
            Im.Separator();
        }

        Im.TextWrapped(modMerger.Warnings[^1]);
    }

    private void DrawError()
    {
        if (modMerger.Error == null)
            return;

        Im.Separator();
        Im.Dummy(Vector2.One);
        using var color = ImGuiColor.Text.Push(Colors.RegexWarningBorder);
        Im.TextWrapped(modMerger.Error.ToString());
    }
}
