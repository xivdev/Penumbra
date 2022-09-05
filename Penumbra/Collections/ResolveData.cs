using System;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Penumbra.Collections;

public readonly struct ResolveData
{
    public static readonly ResolveData Invalid = new(ModCollection.Empty);

    public readonly ModCollection ModCollection;
    public readonly IntPtr        AssociatedGameObject;

    public bool Valid
        => ModCollection != ModCollection.Empty;

    public ResolveData()
    {
        ModCollection        = ModCollection.Empty;
        AssociatedGameObject = IntPtr.Zero;
    }

    public ResolveData( ModCollection collection, IntPtr gameObject )
    {
        ModCollection        = collection;
        AssociatedGameObject = gameObject;
    }

    public ResolveData( ModCollection collection )
        : this( collection, IntPtr.Zero )
    { }

    public override string ToString()
        => ModCollection.Name;
}

public static class ResolveDataExtensions
{
    public static ResolveData ToResolveData( this ModCollection collection )
        => new(collection);

    public static ResolveData ToResolveData( this ModCollection collection, IntPtr ptr )
        => new(collection, ptr);

    public static unsafe ResolveData ToResolveData( this ModCollection collection, void* ptr )
        => new(collection, ( IntPtr )ptr);
}