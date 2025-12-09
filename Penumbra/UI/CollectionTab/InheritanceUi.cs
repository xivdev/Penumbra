using ImSharp;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.UI.Classes;

namespace Penumbra.UI.CollectionTab;

public class InheritanceUi(CollectionManager collectionManager, IncognitoService incognito) : IUiService
{
    private const int InheritedCollectionHeight = 9;

    private static ReadOnlySpan<byte> InheritanceDragDropLabel
        => "##InheritanceMove"u8;

    private readonly CollectionStorage  _collections = collectionManager.Storage;
    private readonly ActiveCollections  _active      = collectionManager.Active;
    private readonly InheritanceManager _inheritance = collectionManager.Inheritances;

    /// <summary> Draw the whole inheritance block. </summary>
    public void Draw()
    {
        using var id = Im.Id.Push("##Inheritance"u8);
        ImEx.TextMultiColored("The Selected Collection "u8)
            .Then(Name(_active.Current), ColorId.SelectedCollection.Value().FullAlpha().Color)
            .Then(" inherits from:"u8)
            .End();
        Im.Dummy(Vector2.One);

        DrawCurrentCollectionInheritance();
        Im.Line.Same();
        DrawInheritanceTrashButton();
        Im.Line.Same();
        DrawRightText();

        DrawNewInheritanceSelection();
        Im.Line.Same();
        if (Im.Button("More Information about Inheritance"u8, Im.ContentRegion.Available with { Y = 0 }))
            Im.Popup.Open("InheritanceHelp"u8);

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

    private static void DrawRightText()
    {
        using var group = Im.Group();
        Im.TextWrapped(
            "Inheritance is a way to use a baseline of mods across multiple collections, without needing to change all those collections if you want to add a single mod."u8);
        Im.TextWrapped(
            "You can select inheritances from the combo below to add them.\nSince the order of inheritances is important, you can reorder them here via drag and drop.\nYou can also delete inheritances by dragging them onto the trash can."u8);
    }

    private static void DrawHelpPopup()
        => ImEx.HelpPopup("InheritanceHelp"u8, new Vector2(1000 * Im.Style.GlobalScale, 20 * Im.Style.TextHeightWithSpacing), () =>
        {
            Im.Line.New();
            Im.Text("Every mod in a collection can have three basic states: 'Enabled', 'Disabled' and 'Unconfigured'."u8);
            Im.BulletText("If the mod is 'Enabled' or 'Disabled', it does not matter if the collection inherits from other collections."u8);
            Im.BulletText(
                "If the mod is unconfigured, those inherited-from collections are checked in the order displayed here, including sub-inheritances."u8);
            Im.BulletText(
                "If a collection is found in which the mod is either 'Enabled' or 'Disabled', the settings from this collection will be used."u8);
            Im.BulletText("If no such collection is found, the mod will be treated as disabled."u8);
            Im.BulletText(
                "Highlighted collections in the left box are never reached because they are already checked in a sub-inheritance before."u8);
            Im.Line.New();
            Im.Text("Example"u8);
            Im.BulletText("Collection A has the Bibo+ body and a Hempen Camise mod enabled."u8);
            Im.BulletText(
                "Collection B inherits from A, leaves Bibo+ unconfigured, but has the Hempen Camise enabled with different settings than A."u8);
            Im.BulletText("Collection C also inherits from A, has Bibo+ explicitly disabled and the Hempen Camise unconfigured."u8);
            Im.BulletText("Collection D inherits from C and then B and leaves everything unconfigured."u8);
            using var indent = Im.Indent();
            Im.BulletText("B uses Bibo+ settings from A and its own Hempen Camise settings."u8);
            Im.BulletText("C has Bibo+ disabled and uses A's Hempen Camise settings."u8);
            Im.BulletText(
                "D has Bibo+ disabled and uses A's Hempen Camise settings, not B's. It traversed the collections in Order D -> (C -> A) -> (B -> A)."u8);
        });


    /// <summary>
    /// If an inherited collection is expanded,
    /// draw all its flattened, distinct children in order with a tree-line.
    /// </summary>
    private void DrawInheritedChildren(ModCollection collection)
    {
        using var id     = Im.Id.Push(collection.Identity.Index);
        using var indent = Im.Indent();

        // Get start point for the lines (top of the selector).
        // Tree line stuff.
        var lineStart = Im.Cursor.ScreenPosition;
        var offsetX   = -Im.Style.IndentSpacing + Im.Style.TreeNodeToLabelSpacing / 2;
        var drawList  = Im.Window.DrawList.Shape;
        var lineSize  = Math.Max(0, Im.Style.IndentSpacing - 9 * Im.Style.GlobalScale);
        lineStart.X += offsetX;
        lineStart.Y -= 2 * Im.Style.GlobalScale;
        var lineEnd = lineStart;

        // Skip the collection itself.
        foreach (var inheritance in collection.Inheritance.FlatHierarchy.Skip(1))
        {
            // Draw the child, already seen collections are colored as conflicts.
            using var color = ImGuiColor.Text.Push(ColorId.HandledConflictMod.Value(),
                _seenInheritedCollections.Contains(inheritance));
            _seenInheritedCollections.Add(inheritance);

            Im.Tree.Leaf($"{Name(inheritance)}###{inheritance.Identity.Id}", TreeNodeFlags.NoTreePushOnOpen);
            var (minRect, maxRect) = (Im.Item.UpperLeftCorner, Im.Item.LowerRightCorner);
            DrawInheritanceTreeClicks(inheritance, false);

            // Tree line stuff.
            if (minRect.X == 0)
                continue;

            // Draw the notch and increase the line length.
            var midPoint = (minRect.Y + maxRect.Y) / 2f - 1f;
            drawList.Line(lineStart with { Y = midPoint }, new Vector2(lineStart.X + lineSize, midPoint), Colors.MetaInfoText,
                Im.Style.GlobalScale);
            lineEnd.Y = midPoint;
        }

        // Finally, draw the folder line.
        drawList.Line(lineStart, lineEnd, Colors.MetaInfoText, Im.Style.GlobalScale);
    }

    /// <summary> Draw a single primary inherited collection. </summary>
    private void DrawInheritance(ModCollection collection)
    {
        using var color = ImGuiColor.Text.Push(ColorId.HandledConflictMod.Value(),
            _seenInheritedCollections.Contains(collection));
        _seenInheritedCollections.Add(collection);
        using var tree = Im.Tree.Node($"{Name(collection)}###{collection.Identity.Name}", TreeNodeFlags.NoTreePushOnOpen);
        color.Pop();
        DrawInheritanceTreeClicks(collection, true);
        DrawInheritanceDropSource(collection);
        DrawInheritanceDropTarget(collection);

        if (tree)
            DrawInheritedChildren(collection);
        else
            // We still want to keep track of conflicts.
            _seenInheritedCollections.UnionWith(collection.Inheritance.FlatHierarchy);
    }

    /// <summary> Draw the list box containing the current inheritance information. </summary>
    private void DrawCurrentCollectionInheritance()
    {
        using var list = Im.ListBox.Begin("##inheritanceList"u8,
            new Vector2(UiHelpers.InputTextMinusButton, Im.Style.TextHeightWithSpacing * InheritedCollectionHeight));
        if (!list)
            return;

        _seenInheritedCollections.Clear();
        _seenInheritedCollections.Add(_active.Current);
        foreach (var collection in _active.Current.Inheritance.DirectlyInheritsFrom.ToList())
            DrawInheritance(collection);
    }

    /// <summary> Draw a drag and drop button to delete. </summary>
    private void DrawInheritanceTrashButton()
    {
        var size        = UiHelpers.IconButtonSize with { Y = Im.Style.TextHeightWithSpacing * InheritedCollectionHeight };
        var buttonColor = Im.Style[ImGuiColor.Button];
        // Prevent hovering from highlighting the button.
        using var color = ImGuiColor.ButtonActive.Push(buttonColor)
            .Push(ImGuiColor.ButtonHovered, buttonColor);
        ImEx.Icon.Button(LunaStyle.DeleteIcon, "Drag primary inheritance here to remove it from the list."u8, size);

        using var target = Im.DragDrop.Target();
        if (target.Success && target.IsDropping(InheritanceDragDropLabel))
            _inheritanceAction = (_active.Current.Inheritance.DirectlyInheritsFrom.IndexOf(_movedInheritance!), -1);
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
        Im.Line.Same();
        var inheritance = InheritanceManager.CheckValidInheritance(_active.Current, _newInheritance);
        var tt = inheritance switch
        {
            InheritanceManager.ValidInheritance.Empty     => "No valid collection to inherit from selected.",
            InheritanceManager.ValidInheritance.Valid     => $"Let the Selected Collection inherit from this collection.",
            InheritanceManager.ValidInheritance.Self      => "The collection can not inherit from itself.",
            InheritanceManager.ValidInheritance.Contained => "Already inheriting from this collection.",
            InheritanceManager.ValidInheritance.Circle    => "Inheriting from this collection would lead to cyclic inheritance.",
            _                                             => string.Empty,
        };
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, inheritance is not InheritanceManager.ValidInheritance.Valid)
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
        Im.Item.SetNextWidth(UiHelpers.InputTextMinusButton);
        _newInheritance ??= _collections.FirstOrDefault(c
                => c != _active.Current && !_active.Current.Inheritance.DirectlyInheritsFrom.Contains(c))
         ?? ModCollection.Empty;
        using var combo = Im.Combo.Begin("##newInheritance"u8, Name(_newInheritance));
        if (!combo)
            return;

        foreach (var collection in _collections
                     .Where(c => InheritanceManager.CheckValidInheritance(_active.Current, c) == InheritanceManager.ValidInheritance.Valid)
                     .OrderBy(c => c.Identity.Name))
        {
            if (Im.Selectable(Name(collection), _newInheritance == collection))
                _newInheritance = collection;
        }
    }

    /// <summary>
    /// Move an inherited collection when dropped onto another.
    /// Move is delayed due to collection changes.
    /// </summary>
    private void DrawInheritanceDropTarget(ModCollection collection)
    {
        using var target = Im.DragDrop.Target();
        if (!target.Success || !target.IsDropping(InheritanceDragDropLabel))
            return;

        if (_movedInheritance != null)
        {
            var idx1 = _active.Current.Inheritance.DirectlyInheritsFrom.IndexOf(_movedInheritance);
            var idx2 = _active.Current.Inheritance.DirectlyInheritsFrom.IndexOf(collection);
            if (idx1 >= 0 && idx2 >= 0)
                _inheritanceAction = (idx1, idx2);
        }

        _movedInheritance = null;
    }

    /// <summary> Move an inherited collection. </summary>
    private void DrawInheritanceDropSource(ModCollection collection)
    {
        using var source = Im.DragDrop.Source();
        if (!source)
            return;

        source.SetPayload(InheritanceDragDropLabel);
        _movedInheritance = collection;
        Im.Text($"Moving {(_movedInheritance != null ? Name(_movedInheritance) : "Unknown")}...");
    }

    /// <summary>
    /// Ctrl + Right-Click -> Switch current collection to this (for all).
    /// Ctrl + Shift + Right-Click -> Delete this inheritance (only if withDelete).
    /// Deletion is delayed due to collection changes.
    /// </summary>
    private void DrawInheritanceTreeClicks(ModCollection collection, bool withDelete)
    {
        if (Im.Io.KeyControl && Im.Item.RightClicked())
        {
            if (withDelete && Im.Io.KeyShift)
                _inheritanceAction = (_active.Current.Inheritance.DirectlyInheritsFrom.IndexOf(collection), -1);
            else
                _newCurrentCollection = collection;
        }

        Im.Tooltip.OnHover(
            $"Control + Right-Click to switch the Selected Collection to this one.{(withDelete ? "\nControl + Shift + Right-Click to remove this inheritance."u8 : StringU8.Empty)}");
    }

    private string Name(ModCollection collection)
        => incognito.IncognitoMode ? collection.Identity.AnonymizedName : collection.Identity.Name;
}
