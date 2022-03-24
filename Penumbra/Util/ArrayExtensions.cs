using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Penumbra.Util;

public static class ArrayExtensions
{
    public static int IndexOf< T >( this IReadOnlyList< T > array, Predicate< T > predicate )
    {
        for( var i = 0; i < array.Count; ++i )
        {
            if( predicate( array[ i ] ) )
            {
                return i;
            }
        }

        return -1;
    }

    public static int IndexOf< T >( this IReadOnlyList< T > array, T needle )
    {
        for( var i = 0; i < array.Count; ++i )
        {
            if( needle!.Equals( array[ i ] ) )
            {
                return i;
            }
        }

        return -1;
    }

    public static bool FindFirst< T >( this IReadOnlyList< T > array, Predicate< T > predicate, [NotNullWhen( true )] out T? result )
    {
        foreach( var obj in array )
        {
            if( predicate( obj ) )
            {
                result = obj!;
                return true;
            }
        }

        result = default;
        return false;
    }

    public static bool FindFirst< T >( this IReadOnlyList< T > array, T needle, [NotNullWhen( true )] out T? result ) where T : IEquatable< T >
    {
        foreach( var obj in array )
        {
            if( obj.Equals( needle ) )
            {
                result = obj!;
                return true;
            }
        }

        result = default;
        return false;
    }

    public static bool Move< T >( this IList< T > list, int idx1, int idx2 )
    {
        idx1 = Math.Clamp( idx1, 0, list.Count - 1 );
        idx2 = Math.Clamp( idx2, 0, list.Count - 1 );
        if( idx1 == idx2 )
        {
            return false;
        }

        var tmp = list[ idx1 ];
        // move element down and shift other elements up
        if( idx1 < idx2 )
        {
            for( var i = idx1; i < idx2; i++ )
            {
                list[ i ] = list[ i + 1 ];
            }
        }
        // move element up and shift other elements down
        else
        {
            for( var i = idx1; i > idx2; i-- )
            {
                list[ i ] = list[ i - 1 ];
            }
        }

        list[ idx2 ] = tmp;
        return true;
    }
}