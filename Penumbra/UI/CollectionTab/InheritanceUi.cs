using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.UI.CollectionTab;

public class InheritanceUi
{
    private const int    InheritedCollectionHeight = 9;
    private const string InheritanceDragDropLabel  = "##InheritanceMove";

    private readonly CollectionStorage  _collections;
    private readonly ActiveCollections  _active;
    private readonly InheritanceManager _inheritance;
    private readonly CollectionSelector _selector;

    public InheritanceUi(CollectionManager collectionManager, CollectionSelector selector)
    {
        _selector    = selector;
        _collections = collectionManager.Storage;
        _active      = collectionManager.Active;
        _inheritance = collectionManager.Inheritances;
    }


    /// <summary> Draw the whole inheritance block. </summary>
    public void Draw()
    {
        using var id    = ImRaii.PushId("##Inheritance");
        ImGuiUtil.DrawColoredText(($"The {TutorialService.SelectedCollection} ", 0), (Name(_active.Current), ColorId.SelectedCollection.Value() | 0xFF000000), (" inherits from:", 0));
        ImGui.Dummy(Vector2.One);

        DrawCurrentCollectionInheritance();
        ImGui.SameLine();
        DrawInheritanceTrashButton();
        ImGui.SameLine();
        DrawRightText();

        DrawNewInheritanceSelection();
        ImGui.SameLine();
        if (ImGui.Button("More Information about Inheritance", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            ImGui.OpenPopup("InheritanceHelp");

        DrawHelpPopup();
        DelayedActions();
    }

    // Keep for reuse.
    private readonly HashSet<ModCollection> _seenInheritedCollections = new(32);

    // Execute changes only outside of loops.
    private ModCollection? _newInheritance;
    private ModCollection? _movedInheritance;
    private (int, int)?    _inheritanceAction;
    private ModCollection? _newCurrentCollection;

    private void DrawRightText()
    {
        using var group = ImRaii.Group();
        ImGuiUtil.TextWrapped(
            "Inheritance is a way to use a baseline of mods across multiple collections, without needing to change all those collections if you want to add a single mod.");
        ImGuiUtil.TextWrapped(
            "You can select inheritances from the combo below to add them.\nSince the order of inheritances is important, you can reorder them here via drag and drop.\nYou can also delete inheritances by dragging them onto the trash can.");
    }

    private void DrawHelpPopup()
    => ImGuiUtil.HelpPopup("InheritanceHelp", new Vector2(1000 * UiHelpers.Scale, 20 * ImGui.GetTextLineHeightWithSpacing()), () =>
    {
        ImGui.NewLine();
        ImGui.TextUnformatted("Every mod in a collection can have three basic states: 'Enabled', 'Disabled' and 'Unconfigured'.");
        ImGui.BulletText("If the mod is 'Enabled' or 'Disabled', it does not matter if the collection inherits from other collections.");
        ImGui.BulletText(
            "If the mod is unconfigured, those inherited-from collections are checked in the order displayed here, including sub-inheritances.");
        ImGui.BulletText(
            "If a collection is found in which the mod is either 'Enabled' or 'Disabled', the settings from this collection will be used.");
        ImGui.BulletText("If no such collection is found, the mod will be treated as disabled.");
        ImGui.BulletText(
            "Highlighted collections in the left box are never reached because they are already checked in a sub-inheritance before.");
        ImGui.NewLine();
        ImGui.TextUnformatted("Example");
        ImGui.BulletText("Collection A has the Bibo+ body and a Hempen Camise mod enabled.");
        ImGui.BulletText(
            "Collection B inherits from A, leaves Bibo+ unconfigured, but has the Hempen Camise enabled with different settings than A.");
        ImGui.BulletText("Collection C also inherits from A, has Bibo+ explicitly disabled and the Hempen Camise unconfigured.");
        ImGui.BulletText("Collection D inherits from C and then B and leaves everything unconfigured.");
        using var indent = ImRaii.PushIndent();
        ImGui.BulletText("B uses Bibo+ settings from A and its own Hempen Camise settings.");
        ImGui.BulletText("C has Bibo+ disabled and uses A's Hempen Camise settings.");
        ImGui.BulletText(
            "D has Bibo+ disabled and uses A's Hempen Camise settings, not B's. It traversed the collections in Order D -> (C -> A) -> (B -> A).");
    });


    /// <summary>
    /// If an inherited collection is expanded,
    /// draw all its flattened, distinct children in order with a tree-line.
    /// </summary>
    private void DrawInheritedChildren(ModCollection collection)
    {
        using var id     = ImRaii.PushId(collection.Index);
        using var indent = ImRaii.PushIndent();

        // Get start point for the lines (top of the selector).
        // Tree line stuff.
        var lineStart = ImGui.GetCursorScreenPos();
        var offsetX   = -ImGui.GetStyle().IndentSpacing + ImGui.GetTreeNodeToLabelSpacing() / 2;
        var drawList  = ImGui.GetWindowDrawList();
        var lineSize  = Math.Max(0, ImGui.GetStyle().IndentSpacing - 9 * UiHelpers.Scale);
        lineStart.X += offsetX;
        lineStart.Y -= 2 * UiHelpers.Scale;
        var lineEnd = lineStart;

        // Skip the collection itself.
        foreach (var inheritance in collection.GetFlattenedInheritance().Skip(1))
        {
            // Draw the child, already seen collections are colored as conflicts.
            using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.HandledConflictMod.Value(),
                _seenInheritedCollections.Contains(inheritance));
            _seenInheritedCollections.Add(inheritance);

            ImRaii.TreeNode($"{Name(inheritance)}###{inheritance.Name}", ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet);
            var (minRect, maxRect) = (ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
            DrawInheritanceTreeClicks(inheritance, false);

            // Tree line stuff.
            if (minRect.X == 0)
                continue;

            // Draw the notch and increase the line length.
            var midPoint = (minRect.Y + maxRect.Y) / 2f - 1f;
            drawList.AddLine(new Vector2(lineStart.X, midPoint), new Vector2(lineStart.X + lineSize, midPoint), Colors.MetaInfoText,
                UiHelpers.Scale);
            lineEnd.Y = midPoint;
        }

        // Finally, draw the folder line.
        drawList.AddLine(lineStart, lineEnd, Colors.MetaInfoText, UiHelpers.Scale);
    }

    /// <summary> Draw a single primary inherited collection. </summary>
    private void DrawInheritance(ModCollection collection)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.HandledConflictMod.Value(),
            _seenInheritedCollections.Contains(collection));
        _seenInheritedCollections.Add(collection);
        using var tree = ImRaii.TreeNode($"{Name(collection)}###{collection.Name}", ImGuiTreeNodeFlags.NoTreePushOnOpen);
        color.Pop();
        DrawInheritanceTreeClicks(collection, true);
        DrawInheritanceDropSource(collection);
        DrawInheritanceDropTarget(collection);

        if (tree)
            DrawInheritedChildren(collection);
        else
            // We still want to keep track of conflicts.
            _seenInheritedCollections.UnionWith(collection.GetFlattenedInheritance());
    }

    /// <summary> Draw the list box containing the current inheritance information. </summary>
    private void DrawCurrentCollectionInheritance()
    {
        using var list = ImRaii.ListBox("##inheritanceList",
            new Vector2(UiHelpers.InputTextMinusButton, ImGui.GetTextLineHeightWithSpacing() * InheritedCollectionHeight));
        if (!list)
            return;

        _seenInheritedCollections.Clear();
        _seenInheritedCollections.Add(_active.Current);
        foreach (var collection in _active.Current.DirectlyInheritsFrom.ToList())
            DrawInheritance(collection);
    }

    /// <summary> Draw a drag and drop button to delete. </summary>
    private void DrawInheritanceTrashButton()
    {
        var size        = UiHelpers.IconButtonSize with { Y = ImGui.GetTextLineHeightWithSpacing() * InheritedCollectionHeight };
        var buttonColor = ImGui.GetColorU32(ImGuiCol.Button);
        // Prevent hovering from highlighting the button.
        using var color = ImRaii.PushColor(ImGuiCol.ButtonActive, buttonColor)
            .Push(ImGuiCol.ButtonHovered, buttonColor);
        ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), size,
            "Drag primary inheritance here to remove it from the list.", false, true);

        using var target = ImRaii.DragDropTarget();
        if (target.Success && ImGuiUtil.IsDropping(InheritanceDragDropLabel))
            _inheritanceAction = (_active.Current.DirectlyInheritsFrom.IndexOf(_movedInheritance!), -1);
    }

    /// <summary>
    /// Set the current collection, or delete or move an inheritance if the action was triggered during iteration.
    /// Can not be done during iteration to keep collections unchanged.
    /// </summary>
    private void DelayedActions()
    {
        if (_newCurrentCollection != null)
        {
            _active.SetCollection(_newCurrentCollection, CollectionType.Current);
            _newCurrentCollection = null;
        }

        if (_inheritanceAction == null)
            return;

        if (_inheritanceAction.Value.Item1 >= 0)
        {
            if (_inheritanceAction.Value.Item2 == -1)
                _inheritance.RemoveInheritance(_active.Current, _inheritanceAction.Value.Item1);
            else
                _inheritance.MoveInheritance(_active.Current, _inheritanceAction.Value.Item1, _inheritanceAction.Value.Item2);
        }

        _inheritanceAction = null;
    }

    /// <summary>
    /// Draw the selector to add new inheritances.
    /// The add button is only available if the selected collection can actually be added.
    /// </summary>
    private void DrawNewInheritanceSelection()
    {
        DrawNewInheritanceCombo();
        ImGui.SameLine();
        var inheritance = InheritanceManager.CheckValidInheritance(_active.Current, _newInheritance);
        var tt = inheritance switch
        {
            InheritanceManager.ValidInheritance.Empty     => "No valid collection to inherit from selected.",
            InheritanceManager.ValidInheritance.Valid     => $"Let the {TutorialService.SelectedCollection} inherit from this collection.",
            InheritanceManager.ValidInheritance.Self      => "The collection can not inherit from itself.",
            InheritanceManager.ValidInheritance.Contained => "Already inheriting from this collection.",
            InheritanceManager.ValidInheritance.Circle    => "Inheriting from this collection would lead to cyclic inheritance.",
            _                                             => string.Empty,
        };
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), UiHelpers.IconButtonSize, tt,
                inheritance != InheritanceManager.ValidInheritance.Valid, true)
         && _inheritance.AddInheritance(_active.Current, _newInheritance!))
            _newInheritance = null;

        if (inheritance != InheritanceManager.ValidInheritance.Valid)
            _newInheritance = null;
    }

    /// <summary>
    /// Draw the combo to select new potential inheritances.
    /// Only valid inheritances are drawn in the preview, or nothing if no inheritance is available.
    /// </summary>
    private void DrawNewInheritanceCombo()
    {
        ImGui.SetNextItemWidth(UiHelpers.InputTextMinusButton);
        _newInheritance ??= _collections.FirstOrDefault(c
                => c != _active.Current && !_active.Current.DirectlyInheritsFrom.Contains(c))
         ?? ModCollection.Empty;
        using var combo = ImRaii.Combo("##newInheritance", Name(_newInheritance));
        if (!combo)
            return;

        foreach (var collection in _collections
                     .Where(c => InheritanceManager.CheckValidInheritance(_active.Current, c) == InheritanceManager.ValidInheritance.Valid)
                     .OrderBy(c => c.Name))
        {
            if (ImGui.Selectable(Name(collection), _newInheritance == collection))
                _newInheritance = collection;
        }
    }

    /// <summary>
    /// Move an inherited collection when dropped onto another.
    /// Move is delayed due to collection changes.
    /// </summary>
    private void DrawInheritanceDropTarget(ModCollection collection)
    {
        using var target = ImRaii.DragDropTarget();
        if (!target.Success || !ImGuiUtil.IsDropping(InheritanceDragDropLabel))
            return;

        if (_movedInheritance != null)
        {
            var idx1 = _active.Current.DirectlyInheritsFrom.IndexOf(_movedInheritance);
            var idx2 = _active.Current.DirectlyInheritsFrom.IndexOf(collection);
            if (idx1 >= 0 && idx2 >= 0)
                _inheritanceAction = (idx1, idx2);
        }

        _movedInheritance = null;
    }

    /// <summary> Move an inherited collection. </summary>
    private void DrawInheritanceDropSource(ModCollection collection)
    {
        using var source = ImRaii.DragDropSource();
        if (!source)
            return;

        ImGui.SetDragDropPayload(InheritanceDragDropLabel, nint.Zero, 0);
        _movedInheritance = collection;
        ImGui.TextUnformatted($"Moving {(_movedInheritance != null ? Name(_movedInheritance) : "Unknown")}...");
    }

    /// <summary>
    /// Ctrl + Right-Click -> Switch current collection to this (for all).
    /// Ctrl + Shift + Right-Click -> Delete this inheritance (only if withDelete).
    /// Deletion is delayed due to collection changes.
    /// </summary>
    private void DrawInheritanceTreeClicks(ModCollection collection, bool withDelete)
    {
        if (ImGui.GetIO().KeyCtrl && ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            if (withDelete && ImGui.GetIO().KeyShift)
                _inheritanceAction = (_active.Current.DirectlyInheritsFrom.IndexOf(collection), -1);
            else
                _newCurrentCollection = collection;
        }

        ImGuiUtil.HoverTooltip($"Control + Right-Click to switch the {TutorialService.SelectedCollection} to this one."
          + (withDelete ? "\nControl + Shift + Right-Click to remove this inheritance." : string.Empty));
    }

    private string Name(ModCollection collection)
        => _selector.IncognitoMode ? collection.AnonymizedName : collection.Name;
}
