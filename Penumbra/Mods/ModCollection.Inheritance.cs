using System;
using System.Collections.Generic;
using System.Linq;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Mods;

public partial class ModCollection2
{
    private readonly List< ModCollection2 > _inheritance = new();

    public event Action InheritanceChanged;

    public IReadOnlyList< ModCollection2 > Inheritance
        => _inheritance;

    public IEnumerable< ModCollection2 > GetFlattenedInheritance()
    {
        yield return this;

        foreach( var collection in _inheritance.SelectMany( c => c._inheritance )
                   .Where( c => !ReferenceEquals( this, c ) )
                   .Distinct() )
        {
            yield return collection;
        }
    }

    public bool AddInheritance( ModCollection2 collection )
    {
        if( ReferenceEquals( collection, this ) || _inheritance.Contains( collection ) )
        {
            return false;
        }

        _inheritance.Add( collection );
        InheritanceChanged.Invoke();
        return true;
    }

    public void RemoveInheritance( int idx )
    {
        _inheritance.RemoveAt( idx );
        InheritanceChanged.Invoke();
    }

    public void MoveInheritance( int from, int to )
    {
        if( _inheritance.Move( from, to ) )
        {
            InheritanceChanged.Invoke();
        }
    }

    public (ModSettings? Settings, ModCollection2 Collection) this[ int idx ]
    {
        get
        {
            foreach( var collection in GetFlattenedInheritance() )
            {
                var settings = _settings[ idx ];
                if( settings != null )
                {
                    return ( settings, collection );
                }
            }

            return ( null, this );
        }
    }
}