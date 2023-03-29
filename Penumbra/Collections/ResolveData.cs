using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.GameData.Actors;

namespace Penumbra.Collections;

public readonly struct ResolveData
{
    public static readonly ResolveData Invalid = new();

    private readonly ModCollection? _modCollection;

    public ModCollection ModCollection
        => _modCollection ?? ModCollection.Empty;

    public readonly IntPtr AssociatedGameObject;

    public bool Valid
        => _modCollection != null;

    public ResolveData()
    {
        _modCollection       = null!;
        AssociatedGameObject = IntPtr.Zero;
    }

    public ResolveData(ModCollection collection, IntPtr gameObject)
    {
        _modCollection       = collection;
        AssociatedGameObject = gameObject;
    }

    public ResolveData(ModCollection collection)
        : this(collection, IntPtr.Zero)
    { }

    public override string ToString()
        => ModCollection.Name;
}

public static class ResolveDataExtensions
{
    public static ResolveData ToResolveData(this ModCollection collection)
        => new(collection);

    public static ResolveData ToResolveData(this ModCollection collection, IntPtr ptr)
        => new(collection, ptr);

    public static unsafe ResolveData ToResolveData(this ModCollection collection, void* ptr)
        => new(collection, (IntPtr)ptr);
}
