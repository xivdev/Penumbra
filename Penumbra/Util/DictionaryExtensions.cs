using System.Collections.Generic;

namespace Penumbra.Util;

public static class DictionaryExtensions
{
    // Returns whether two dictionaries contain equal keys and values.
    public static bool SetEquals< TKey, TValue >( this IReadOnlyDictionary< TKey, TValue > lhs, IReadOnlyDictionary< TKey, TValue > rhs )
    {
        if( lhs.Count != rhs.Count )
        {
            return false;
        }

        foreach( var (key, value) in lhs )
        {
            if( !rhs.TryGetValue( key, out var rhsValue ) )
            {
                return false;
            }

            if( value == null )
            {
                if( rhsValue != null )
                {
                    return false;
                }

                continue;
            }

            if( !value.Equals( rhsValue ) )
            {
                return false;
            }
        }

        return true;
    }

    // Set one dictionary to the other, deleting previous entries and ensuring capacity beforehand.
    public static void SetTo< TKey, TValue >( this Dictionary< TKey, TValue > lhs, IReadOnlyDictionary< TKey, TValue > rhs )
        where TKey : notnull
    {
        lhs.Clear();
        lhs.EnsureCapacity( rhs.Count );
        foreach( var (key, value) in rhs )
        {
            lhs.Add( key, value );
        }
    }
}