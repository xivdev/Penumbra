using System;
using System.Collections;
using System.Collections.Generic;

namespace Penumbra.Util;

public class IndexSet : IEnumerable<int>
{
    private readonly BitArray _set;
    private int _count;

    public int Capacity => _set.Count;

    public int Count => _count;

    public bool this[Index index]
    {
        get => _set[index];
        set
        {
            if( value )
            {
                Add( index );
            }
            else
            {
                Remove( index );
            }
        }
    }

    public IndexSet( int capacity, bool initiallyFull )
    {
        _set = new BitArray( capacity, initiallyFull );
        _count = initiallyFull ? capacity : 0;
    }

    public bool Add( Index index )
    {
        var ret = !_set[index];
        if( ret )
        {
            ++_count;
            _set[index] = true;
        }
        return ret;
    }

    public bool Remove( Index index )
    {
        var ret = _set[index];
        if( ret )
        {
            --_count;
            _set[index] = false;
        }
        return ret;
    }

    public int AddRange( int offset, int length )
    {
        var ret = 0;
        for( var idx = 0; idx < length; ++idx )
        {
            if( Add( offset + idx ) )
            {
                ++ret;
            }
        }
        return ret;
    }

    public int RemoveRange( int offset, int length )
    {
        var ret = 0;
        for( var idx = 0; idx < length; ++idx )
        {
            if( Remove( offset + idx ) )
            {
                ++ret;
            }
        }
        return ret;
    }

    public IEnumerator<int> GetEnumerator()
    {
        if( _count > 0 )
        {
            var capacity = _set.Count;
            var remaining = _count;
            for( var i = 0; i < capacity; ++i )
            {
                if( _set[i] )
                {
                    yield return i;
                    if( --remaining == 0 )
                    {
                        yield break;
                    }
                }
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
