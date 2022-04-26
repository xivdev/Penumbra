using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Penumbra.Util;

public static class ArrayExtensions
{
    public static IEnumerable< (T, int) > WithIndex< T >( this IEnumerable< T > list )
        => list.Select( ( x, i ) => ( x, i ) );

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
}