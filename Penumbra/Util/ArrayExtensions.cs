using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Penumbra.Util;

public static class ArrayExtensions
{
    // Iterate over enumerables with additional index.
    public static IEnumerable< (T, int) > WithIndex< T >( this IEnumerable< T > list )
        => list.Select( ( x, i ) => ( x, i ) );


    // Find the index of the first object fulfilling predicate's criteria in the given list.
    // Returns -1 if no such object is found.
    public static int IndexOf< T >( this IEnumerable< T > array, Predicate< T > predicate )
    {
        var i = 0;
        foreach( var obj in array )
        {
            if( predicate( obj ) )
            {
                return i;
            }

            ++i;
        }

        return -1;
    }

    // Find the index of the first occurrence of needle in the given list.
    // Returns -1 if needle is not contained in the list.
    public static int IndexOf< T >( this IEnumerable< T > array, T needle ) where T : notnull
    {
        var i = 0;
        foreach( var obj in array )
        {
            if( needle.Equals( obj ) )
            {
                return i;
            }

            ++i;
        }

        return -1;
    }

    // Find the first object fulfilling predicate's criteria in the given list, if one exists.
    // Returns true if an object is found, false otherwise.
    public static bool FindFirst< T >( this IEnumerable< T > array, Predicate< T > predicate, [NotNullWhen( true )] out T? result )
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

    // Find the first occurrence of needle in the given list and return the value contained in the list in result.
    // Returns true if an object is found, false otherwise.
    public static bool FindFirst< T >( this IEnumerable< T > array, T needle, [NotNullWhen( true )] out T? result ) where T : notnull
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