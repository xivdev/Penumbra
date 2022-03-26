using System;
using System.Collections.Generic;
using System.Linq;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Collections;

public partial class ModCollection
{
    private readonly List< ModCollection > _inheritance = new();

    public event Action< bool > InheritanceChanged;

    public IReadOnlyList< ModCollection > Inheritance
        => _inheritance;

    public IEnumerable< ModCollection > GetFlattenedInheritance()
    {
        yield return this;

        foreach( var collection in _inheritance.SelectMany( c => c._inheritance )
                   .Where( c => !ReferenceEquals( this, c ) )
                   .Distinct() )
        {
            yield return collection;
        }
    }

    public bool AddInheritance( ModCollection collection )
    {
        if( ReferenceEquals( collection, this ) || _inheritance.Contains( collection ) )
        {
            return false;
        }

        _inheritance.Add( collection );
        collection.ModSettingChanged  += OnInheritedModSettingChange;
        collection.InheritanceChanged += OnInheritedInheritanceChange;
        InheritanceChanged.Invoke( false );
        return true;
    }

    public void RemoveInheritance( int idx )
    {
        var inheritance = _inheritance[ idx ];
        inheritance.ModSettingChanged  -= OnInheritedModSettingChange;
        inheritance.InheritanceChanged -= OnInheritedInheritanceChange;
        _inheritance.RemoveAt( idx );
        InheritanceChanged.Invoke( false );
    }

    public void MoveInheritance( int from, int to )
    {
        if( _inheritance.Move( from, to ) )
        {
            InheritanceChanged.Invoke( false );
        }
    }

    private void OnInheritedModSettingChange( ModSettingChange type, int modIdx, int oldValue, string? optionName, bool _ )
    {
        if( _settings[ modIdx ] == null )
        {
            ModSettingChanged.Invoke( type, modIdx, oldValue, optionName, true );
        }
    }

    private void OnInheritedInheritanceChange( bool _ )
        => InheritanceChanged.Invoke( true );

    public (ModSettings? Settings, ModCollection Collection) this[ Index idx ]
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