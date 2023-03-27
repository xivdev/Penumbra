using System;
using System.Collections.Generic;
using ImGuiNET;
using OtterGui.Widgets;
using Penumbra.Collections;
using Penumbra.GameData.Actors;

namespace Penumbra.UI.CollectionTab;

public sealed class CollectionSelector : FilterComboCache<ModCollection>
{
    private readonly CollectionManager _collectionManager;

    public CollectionSelector(CollectionManager manager, Func<IReadOnlyList<ModCollection>> items)
        : base(items)
        => _collectionManager = manager;

    public void Draw(string label, float width, int individualIdx)
    {
        var (_, collection) = _collectionManager.Individuals[individualIdx];
        if (Draw(label, collection.Name, string.Empty, width, ImGui.GetTextLineHeightWithSpacing()) && CurrentSelection != null)
            _collectionManager.SetCollection(CurrentSelection, CollectionType.Individual, individualIdx);
    }

    public void Draw(string label, float width, CollectionType type)
    {
        var current = _collectionManager.ByType(type, ActorIdentifier.Invalid);
        if (Draw(label, current?.Name ?? string.Empty, string.Empty, width, ImGui.GetTextLineHeightWithSpacing()) && CurrentSelection != null)
            _collectionManager.SetCollection(CurrentSelection, type);
    }

    protected override string ToString(ModCollection obj)
        => obj.Name;
}
