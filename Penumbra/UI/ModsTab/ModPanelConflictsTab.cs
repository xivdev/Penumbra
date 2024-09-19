using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
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

public class ModPanelConflictsTab(CollectionManager collectionManager, ModFileSystemSelector selector) : ITab, IUiService
{
    private int? _currentPriority;

    public ReadOnlySpan<byte> Label
        => "Conflicts"u8;

    public bool IsVisible
        => collectionManager.Active.Current.Conflicts(selector.Selected!).Any(c => !GetPriority(c).IsHidden);

    private readonly ConditionalWeakTable<IMod, object> _expandedMods = [];

    private ModPriority GetPriority(ModConflicts conflicts)
    {
        if (conflicts.Mod2.Index < 0)
            return conflicts.Mod2.Priority;

        return collectionManager.Active.Current[conflicts.Mod2.Index].Settings?.Priority ?? ModPriority.Default;
    }

    public void DrawContent()
    {
        using var table = ImRaii.Table("conflicts", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, ImGui.GetContentRegionAvail());
        if (!table)
            return;

        var       buttonSize       = new Vector2(ImGui.GetFrameHeight());
        var       spacing          = ImGui.GetStyle().ItemInnerSpacing with { Y = ImGui.GetStyle().ItemSpacing.Y };
        var       priorityRowWidth = ImGui.CalcTextSize("Priority").X + 20 * ImGuiHelpers.GlobalScale + 2 * buttonSize.X;
        var       priorityWidth    = priorityRowWidth - 2 * (buttonSize.X + spacing.X);
        using var style            = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing);
        ImGui.TableSetupColumn("Conflicting Mod", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Priority",        ImGuiTableColumnFlags.WidthFixed, priorityRowWidth);
        ImGui.TableSetupColumn("Files",           ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Files").X + spacing.X);

        ImGui.TableSetupScrollFreeze(2, 2);
        ImGui.TableHeadersRow();
        DrawCurrentRow(priorityWidth);

        // Can not be null because otherwise the tab bar is never drawn.
        var mod = selector.Selected!;
        foreach (var (conflict, index) in collectionManager.Active.Current.Conflicts(mod).Where(c => !c.Mod2.Priority.IsHidden)
                     .OrderByDescending(GetPriority)
                     .ThenBy(c => c.Mod2.Name.Lower).WithIndex())
        {
            using var id = ImRaii.PushId(index);
            DrawConflictRow(conflict, priorityWidth, buttonSize);
        }
    }

    private void DrawCurrentRow(float priorityWidth)
    {
        ImGui.TableNextColumn();
        using var c = ImRaii.PushColor(ImGuiCol.Text, ColorId.FolderLine.Value());
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(selector.Selected!.Name);
        ImGui.TableNextColumn();
        var priority = collectionManager.Active.Current[selector.Selected!.Index].Settings!.Priority.Value;
        ImGui.SetNextItemWidth(priorityWidth);
        if (ImGui.InputInt("##priority", ref priority, 0, 0, ImGuiInputTextFlags.EnterReturnsTrue))
            _currentPriority = priority;

        if (ImGui.IsItemDeactivatedAfterEdit() && _currentPriority.HasValue)
        {
            if (_currentPriority != collectionManager.Active.Current[selector.Selected!.Index].Settings!.Priority.Value)
                collectionManager.Editor.SetModPriority(collectionManager.Active.Current, selector.Selected!,
                    new ModPriority(_currentPriority.Value));

            _currentPriority = null;
        }
        else if (ImGui.IsItemDeactivated())
        {
            _currentPriority = null;
        }

        ImGui.TableNextColumn();
    }

    private void DrawConflictSelectable(ModConflicts conflict)
    {
        ImGui.AlignTextToFramePadding();
        if (ImGui.Selectable(conflict.Mod2.Name) && conflict.Mod2 is Mod otherMod)
            selector.SelectByValue(otherMod);
        var hovered      = ImGui.IsItemHovered();
        var rightClicked = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        if (conflict.Mod2 is Mod otherMod2)
        {
            if (hovered)
                ImGui.SetTooltip("Click to jump to mod, Control + Right-Click to disable mod.");
            if (rightClicked && ImGui.GetIO().KeyCtrl)
                collectionManager.Editor.SetModState(collectionManager.Active.Current, otherMod2, false);
        }
    }

    private bool DrawExpandedFiles(ModConflicts conflict)
    {
        if (!_expandedMods.TryGetValue(conflict.Mod2, out _))
            return false;

        using var indent = ImRaii.PushIndent(30f);
        foreach (var data in conflict.Conflicts)
        {
            _ = data switch
            {
                Utf8GamePath p    => ImUtf8.Selectable(p.Path.Span,  false),
                IMetaIdentifier m => ImUtf8.Selectable(m.ToString(), false),
                _                 => false,
            };
        }

        return true;
    }

    private void DrawConflictRow(ModConflicts conflict, float priorityWidth, Vector2 buttonSize)
    {
        ImGui.TableNextColumn();
        DrawConflictSelectable(conflict);
        var expanded = DrawExpandedFiles(conflict);
        ImGui.TableNextColumn();
        var conflictPriority = DrawPriorityInput(conflict, priorityWidth);
        ImGui.SameLine();
        var selectedPriority = collectionManager.Active.Current[selector.Selected!.Index].Settings!.Priority.Value;
        DrawPriorityButtons(conflict.Mod2 as Mod, conflictPriority, selectedPriority, buttonSize);
        ImGui.TableNextColumn();
        DrawExpandButton(conflict.Mod2, expanded, buttonSize);
    }

    private void DrawExpandButton(IMod mod, bool expanded, Vector2 buttonSize)
    {
        var (icon, tt) = expanded
            ? (FontAwesomeIcon.CaretUp.ToIconString(), "Hide the conflicting files for this mod.")
            : (FontAwesomeIcon.CaretDown.ToIconString(), "Show the conflicting files for this mod.");
        if (ImGuiUtil.DrawDisabledButton(icon, buttonSize, tt, false, true))
        {
            if (expanded)
                _expandedMods.Remove(mod);
            else
                _expandedMods.Add(mod, new object());
        }
    }

    private int DrawPriorityInput(ModConflicts conflict, float priorityWidth)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text,
            conflict.HasPriority ? ColorId.HandledConflictMod.Value() : ColorId.ConflictingMod.Value());
        using var disabled = ImRaii.Disabled(conflict.Mod2.Index < 0);
        var       priority = _currentPriority ?? GetPriority(conflict).Value;

        ImGui.SetNextItemWidth(priorityWidth);
        if (ImGui.InputInt("##priority", ref priority, 0, 0, ImGuiInputTextFlags.EnterReturnsTrue))
            _currentPriority = priority;

        if (ImGui.IsItemDeactivatedAfterEdit() && _currentPriority.HasValue)
        {
            if (_currentPriority != GetPriority(conflict).Value)
                collectionManager.Editor.SetModPriority(collectionManager.Active.Current, (Mod)conflict.Mod2,
                    new ModPriority(_currentPriority.Value));

            _currentPriority = null;
        }
        else if (ImGui.IsItemDeactivated())
        {
            _currentPriority = null;
        }

        return priority;
    }

    private void DrawPriorityButtons(Mod? conflict, int conflictPriority, int selectedPriority, Vector2 buttonSize)
    {
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.SortNumericUpAlt.ToIconString(), buttonSize,
                $"Set the priority of the currently selected mod to this mods priority plus one. ({selectedPriority} -> {conflictPriority + 1})",
                selectedPriority > conflictPriority, true))
            collectionManager.Editor.SetModPriority(collectionManager.Active.Current, selector.Selected!,
                new ModPriority(conflictPriority + 1));
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.SortNumericDownAlt.ToIconString(), buttonSize,
                $"Set the priority of this mod to the currently selected mods priority minus one. ({conflictPriority} -> {selectedPriority - 1})",
                selectedPriority > conflictPriority || conflict == null, true))
            collectionManager.Editor.SetModPriority(collectionManager.Active.Current, conflict!, new ModPriority(selectedPriority - 1));
    }
}
