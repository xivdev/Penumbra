using System;
using System.Runtime.CompilerServices;

namespace Penumbra.Util;

public static class Functions
{
    [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
    public static bool SetDifferent< T >( T oldValue, T newValue, Action< T > set ) where T : IEquatable< T >
    {
        if( oldValue.Equals( newValue ) )
        {
            return false;
        }

        set( newValue );
        return true;
    }
}