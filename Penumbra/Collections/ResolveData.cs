using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.String;

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

    public ResolveData( ModCollection collection, IntPtr gameObject )
    {
        _modCollection       = collection;
        AssociatedGameObject = gameObject;
    }

    public ResolveData( ModCollection collection )
        : this( collection, IntPtr.Zero )
    { }

    public override string ToString()
        => ModCollection.Name;

    public unsafe string AssociatedName()
    {
        if( AssociatedGameObject == IntPtr.Zero )
        {
            return "no associated object.";
        }

        try
        {
            var id = Penumbra.Actors.FromObject( ( GameObject* )AssociatedGameObject, out _, false, true, true );
            if( id.IsValid )
            {
                var name  = id.ToString();
                var parts = name.Split( ' ', 3 );
                return string.Join( " ", parts.Length != 3 ? parts.Select( n => $"{n[ 0 ]}." ) : parts[ ..2 ].Select( n => $"{n[ 0 ]}." ).Append( parts[ 2 ] ) );
            }
        }
        catch
        {
            // ignored
        }

        return $"0x{AssociatedGameObject:X}";
    }
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