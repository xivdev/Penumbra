using OtterGui.Filesystem;

namespace Penumbra.Collections;

public struct ModCollectionInheritance
{
    public           IReadOnlyList<string>? InheritanceByName { get; private set; }
    private readonly List<ModCollection>    _directlyInheritsFrom = [];
    private readonly List<ModCollection>    _directlyInheritedBy  = [];
    private readonly List<ModCollection>    _flatHierarchy        = [];

    public ModCollectionInheritance()
    { }

    private ModCollectionInheritance(List<ModCollection> inheritsFrom)
        => _directlyInheritsFrom = [.. inheritsFrom];

    public ModCollectionInheritance(IReadOnlyList<string> byName)
        => InheritanceByName = byName;

    public ModCollectionInheritance Clone()
        => new(_directlyInheritsFrom);

    public IEnumerable<string> Identifiers
        => InheritanceByName ?? _directlyInheritsFrom.Select(c => c.Identity.Identifier);

    public IReadOnlyList<string>? ConsumeNames()
    {
        var ret = InheritanceByName;
        InheritanceByName = null;
        return ret;
    }

    public static void UpdateChildren(ModCollection parent)
    {
        foreach (var inheritance in parent.Inheritance.DirectlyInheritsFrom)
            inheritance.Inheritance._directlyInheritedBy.Add(parent);
    }

    public void AddInheritance(ModCollection inheritor, ModCollection newParent)
    {
        _directlyInheritsFrom.Add(newParent);
        newParent.Inheritance._directlyInheritedBy.Add(inheritor);
        UpdateFlattenedInheritance(inheritor);
    }

    public ModCollection RemoveInheritanceAt(ModCollection inheritor, int idx)
    {
        var parent = DirectlyInheritsFrom[idx];
        _directlyInheritsFrom.RemoveAt(idx);
        parent.Inheritance._directlyInheritedBy.Remove(parent);
        UpdateFlattenedInheritance(inheritor);
        return parent;
    }

    public bool MoveInheritance(ModCollection inheritor, int from, int to)
    {
        if (!_directlyInheritsFrom.Move(from, to))
            return false;

        UpdateFlattenedInheritance(inheritor);
        return true;
    }

    public void RemoveChild(ModCollection child)
        => _directlyInheritedBy.Remove(child);

    /// <summary> Contains all direct parent collections this collection inherits settings from. </summary>
    public readonly IReadOnlyList<ModCollection> DirectlyInheritsFrom
        => _directlyInheritsFrom;

    /// <summary> Contains all direct child collections that inherit from this collection. </summary>
    public readonly IReadOnlyList<ModCollection> DirectlyInheritedBy
        => _directlyInheritedBy;

    /// <summary>
    /// Iterate over all collections inherited from in depth-first order.
    /// Skip already visited collections to avoid circular dependencies.
    /// </summary>
    public readonly IReadOnlyList<ModCollection> FlatHierarchy
        => _flatHierarchy;

    public static void UpdateFlattenedInheritance(ModCollection parent)
    {
        parent.Inheritance._flatHierarchy.Clear();
        parent.Inheritance._flatHierarchy.AddRange(InheritedCollections(parent).Distinct());
    }

    /// <summary> All inherited collections in application order without filtering for duplicates. </summary>
    private static IEnumerable<ModCollection> InheritedCollections(ModCollection parent)
        => parent.Inheritance.DirectlyInheritsFrom.SelectMany(InheritedCollections).Prepend(parent);
}
