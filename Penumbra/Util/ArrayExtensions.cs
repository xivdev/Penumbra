using System;
using System.Collections.Generic;

namespace Penumbra
{
    public static class ArrayExtensions
    {
        public static void Swap<T>( this T[] array, int idx1, int idx2 )
        {
            var tmp = array[idx1];
            array[idx1] = array[idx2];
            array[idx2] = tmp;
        }

        public static void Swap<T>( this List<T> array, int idx1, int idx2 )
        {
            var tmp = array[idx1];
            array[idx1] = array[idx2];
            array[idx2] = tmp;
        }

        public static void Swap<T>( this T[] array, T lhs, T rhs )
        {
            var idx1 = Array.IndexOf( array, lhs );
            if( idx1 < 0 )
            {
                return;
            }

            var idx2 = Array.IndexOf( array, rhs );
            if( idx2 < 0 )
            {
                return;
            }

            array.Swap( idx1, idx2 );
        }

        public static void Swap<T>( this List<T> array, T lhs, T rhs )
        {
            var idx1 = array.IndexOf( lhs );
            if( idx1 < 0 )
            {
                return;
            }

            var idx2 = array.IndexOf( rhs );
            if( idx2 < 0 )
            {
                return;
            }

            array.Swap( idx1, idx2 );
        }
    }
}
