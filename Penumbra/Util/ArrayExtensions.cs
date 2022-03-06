using System;
using System.Collections.Generic;

namespace Penumbra.Util;

public static class ArrayExtensions
{
    public static int IndexOf< T >( this T[] array, Predicate< T > match )
    {
        for( var i = 0; i < array.Length; ++i )
        {
            if( match( array[ i ] ) )
            {
                return i;
            }
        }

        return -1;
    }

    public static int IndexOf< T >( this IList< T > array, Func< T, bool > predicate )
    {
        for( var i = 0; i < array.Count; ++i )
        {
            if( predicate.Invoke( array[ i ] ) )
            {
                return i;
            }
        }

        return -1;
    }
}