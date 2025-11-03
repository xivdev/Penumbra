using Dalamud.Interface;
using ImSharp;
using Luna;
using OtterGui.Widgets;
using Penumbra.Collections.Cache;
using Penumbra.Collections.Manager;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Settings;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public class ModPanelConflictsTab(CollectionManager collectionManager, ModFileSystemSelector selector) : ITab<ModPanelTab>
{
    public ReadOnlySpan<byte> Label
        => "Conflicts"u8;

    public ModPanelTab Identifier
        => ModPanelTab.Conflicts;

    public bool IsVisible
        => collectionManager.Active.Current.Conflicts(selector.Selected!).Any(c => !GetPriority(c).IsHidden);

    private readonly ConditionalWeakTable<IMod, object> _expandedMods = [];

    private ModPriority GetPriority(ModConflicts conflicts)
    {
        if (conflicts.Mod2.Index < 0)
            return conflicts.Mod2.Priority;

        return collectionManager.Active.Current.GetActualSettings(conflicts.Mod2.Index).Settings?.Priority ?? ModPriority.Default;
    }

    public void DrawContent()
    {
        using var table = Im.Table.Begin("conflicts"u8, 3, TableFlags.RowBackground | TableFlags.ScrollY, Im.ContentRegion.Available);
        if (!table)
            return;

        var       buttonSize       = new Vector2(Im.Style.FrameHeight);
        var       spacing          = Im.Style.ItemInnerSpacing with { Y = Im.Style.ItemSpacing.Y };
        var       priorityRowWidth = Im.Font.CalculateSize("Priority"u8).X + 20 * Im.Style.GlobalScale + 2 * buttonSize.X;
        var       priorityWidth    = priorityRowWidth - 2 * (buttonSize.X + spacing.X);
        using var style            = ImStyleDouble.ItemSpacing.Push(spacing);
        table.SetupColumn("Conflicting Mod"u8, TableColumnFlags.WidthStretch);
        table.SetupColumn("Priority"u8,        TableColumnFlags.WidthFixed, priorityRowWidth);
        table.SetupColumn("Files"u8,           TableColumnFlags.WidthFixed, Im.Font.CalculateSize("Files"u8).X + spacing.X);

        table.SetupScrollFreeze(2, 2);
        table.HeaderRow();
        DrawCurrentRow(table, priorityWidth);

        // Can not be null because otherwise the tab bar is never drawn.
        var mod = selector.Selected!;
        foreach (var (index, conflict) in collectionManager.Active.Current.Conflicts(mod).Where(c => !c.Mod2.Priority.IsHidden)
                     .OrderByDescending(GetPriority)
                     .ThenBy(c => c.Mod2.Name, StringComparer.OrdinalIgnoreCase).Index())
        {
            using var id = Im.Id.Push(index);
            DrawConflictRow(table, conflict, priorityWidth, buttonSize);
        }
    }

    private void DrawCurrentRow(in Im.TableDisposable table, float priorityWidth)
    {
        using var c = ImGuiColor.Text.Push(ColorId.FolderLine.Value());
        table.DrawFrameColumn(selector.Selected!.Name);
        table.NextColumn();
        var actualSettings = collectionManager.Active.Current.GetActualSettings(selector.Selected!.Index).Settings!;
        var priority       = actualSettings.Priority.Value;
        // TODO
        using (Im.Disabled(actualSettings is TemporaryModSettings))
        {
            if (ImEx.InputOnDeactivation.Scalar("##priority"u8, ref priority))
                if (priority != actualSettings.Priority.Value)
                    collectionManager.Editor.SetModPriority(collectionManager.Active.Current, selector.Selected!,
                        new ModPriority(priority));
        }

        table.NextColumn();
    }

    private void DrawConflictSelectable(ModConflicts conflict)
    {
        Im.Cursor.FrameAlign();
        if (Im.Selectable(conflict.Mod2.Name) && conflict.Mod2 is Mod otherMod)
            selector.SelectByValue(otherMod);
        var hovered      = Im.Item.Hovered();
        var rightClicked = Im.Item.RightClicked();
        if (conflict.Mod2 is Mod otherMod2)
        {
            if (hovered)
                Im.Tooltip.Set("Click to jump to mod, Control + Right-Click to disable mod."u8);
            if (rightClicked && Im.Io.KeyControl)
                collectionManager.Editor.SetModState(collectionManager.Active.Current, otherMod2, false);
        }
    }

    private bool DrawExpandedFiles(ModConflicts conflict)
    {
        if (!_expandedMods.TryGetValue(conflict.Mod2, out _))
            return false;

        using var indent = Im.Indent(30f);
        foreach (var data in conflict.Conflicts)
        {
            _ = data switch
            {
                Utf8GamePath p    => Im.Selectable(p.Path.Span),
                IMetaIdentifier m => Im.Selectable($"{m}"),
                _                 => false,
            };
        }

        return true;
    }

    private void DrawConflictRow(in Im.TableDisposable table, ModConflicts conflict, float priorityWidth, Vector2 buttonSize)
    {
        table.NextColumn();
        DrawConflictSelectable(conflict);
        var expanded = DrawExpandedFiles(conflict);
        table.NextColumn();
        var conflictPriority = DrawPriorityInput(conflict, priorityWidth);
        Im.Line.Same();
        var selectedPriority = collectionManager.Active.Current.GetActualSettings(selector.Selected!.Index).Settings!.Priority.Value;
        DrawPriorityButtons(conflict.Mod2 as Mod, conflictPriority, selectedPriority, buttonSize);
        table.NextColumn();
        DrawExpandButton(conflict.Mod2, expanded, buttonSize);
    }

    private void DrawExpandButton(IMod mod, bool expanded, Vector2 buttonSize)
    {
        var (icon, tt) = expanded
            ? RefTuple.Create(LunaStyle.CollapseUpIcon, "Hide the conflicting files for this mod."u8)
            : RefTuple.Create(LunaStyle.ExpandDownIcon, "Show the conflicting files for this mod."u8);
        if (ImEx.Icon.Button(icon, tt, buttonSize))
        {
            if (expanded)
                _expandedMods.Remove(mod);
            else
                _expandedMods.Add(mod, new object());
        }
    }

    private int DrawPriorityInput(ModConflicts conflict, float priorityWidth)
    {
        using var color = ImGuiColor.Text.Push(conflict.HasPriority ? ColorId.HandledConflictMod.Value() : ColorId.ConflictingMod.Value());
        using var disabled = Im.Disabled(conflict.Mod2.Index < 0);
        var       priority = GetPriority(conflict).Value;
        var       originalPriority = priority;

        Im.Item.SetNextWidth(priorityWidth);
        if (ImEx.InputOnDeactivation.Scalar("##priority"u8, ref priority) && priority != originalPriority)
            collectionManager.Editor.SetModPriority(collectionManager.Active.Current, (Mod)conflict.Mod2,
                new ModPriority(priority));
        return priority;
    }

    private void DrawPriorityButtons(Mod? conflict, int conflictPriority, int selectedPriority, Vector2 buttonSize)
    {
        if (ImEx.Icon.Button(FontAwesomeIcon.SortNumericUpAlt.Icon(),
                $"Set the priority of the currently selected mod to this mods priority plus one. ({selectedPriority} -> {conflictPriority + 1})",
                selectedPriority > conflictPriority, buttonSize))
            collectionManager.Editor.SetModPriority(collectionManager.Active.Current, selector.Selected!,
                new ModPriority(conflictPriority + 1));
        Im.Line.Same();
        if (ImEx.Icon.Button(FontAwesomeIcon.SortNumericDownAlt.Icon(), 
                $"Set the priority of this mod to the currently selected mods priority minus one. ({conflictPriority} -> {selectedPriority - 1})",
                selectedPriority > conflictPriority || conflict == null, buttonSize))
            collectionManager.Editor.SetModPriority(collectionManager.Active.Current, conflict!, new ModPriority(selectedPriority - 1));
    }
}
