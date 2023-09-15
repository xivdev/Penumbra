namespace Penumbra.Util;

public static class DictionaryExtensions
{
    /// <summary> Returns whether two dictionaries contain equal keys and values. </summary>
    public static bool SetEquals< TKey, TValue >( this IReadOnlyDictionary< TKey, TValue > lhs, IReadOnlyDictionary< TKey, TValue > rhs )
    {
        if( ReferenceEquals( lhs, rhs ) )
        {
            return true;
        }


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

    /// <summary> Set one dictionary to the other, deleting previous entries and ensuring capacity beforehand. </summary>
    public static void SetTo< TKey, TValue >( this Dictionary< TKey, TValue > lhs, IReadOnlyDictionary< TKey, TValue > rhs )
        where TKey : notnull
    {
        if( ReferenceEquals( lhs, rhs ) )
            return;

        lhs.Clear();
        lhs.EnsureCapacity( rhs.Count );
        foreach( var (key, value) in rhs )
            lhs.Add( key, value );
    }

    /// <summary> Set one set to the other, deleting previous entries and ensuring capacity beforehand. </summary>
    public static void SetTo<T>(this HashSet<T> lhs, IReadOnlySet<T> rhs)
    {
        if (ReferenceEquals(lhs, rhs))
            return;

        lhs.Clear();
        lhs.EnsureCapacity(rhs.Count);
        foreach (var value in rhs)
            lhs.Add(value);
    }

    /// <summary> Add all entries from the other dictionary that would not overwrite current keys. </summary>
    public static void AddFrom< TKey, TValue >( this Dictionary< TKey, TValue > lhs, IReadOnlyDictionary< TKey, TValue > rhs )
        where TKey : notnull
    {
        if( ReferenceEquals( lhs, rhs ) )
        {
            return;
        }

        lhs.EnsureCapacity( lhs.Count + rhs.Count );
        foreach( var (key, value) in rhs )
        {
            lhs.Add( key, value );
        }
    }

    public static int ReplaceValue< TKey, TValue >( this Dictionary< TKey, TValue > dict, TValue from, TValue to )
        where TKey : notnull
        where TValue : IEquatable< TValue >
    {
        var count = 0;
        foreach( var (key, _) in dict.ToArray().Where( kvp => kvp.Value.Equals( from ) ) )
        {
            dict[ key ] = to;
            ++count;
        }

        return count;
    }
}