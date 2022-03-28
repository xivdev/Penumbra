using System;
using System.Collections.Generic;
using System.Linq;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.Collections;

// ModCollections can inherit from an arbitrary number of other collections.
// This is transitive, so a collection A inheriting from B also inherits from everything B inherits.
// Circular dependencies are resolved by distinctness.
public partial class ModCollection
{
    // A change in inheritance usually requires complete recomputation.
    public event Action< bool > InheritanceChanged;

    private readonly List< ModCollection > _inheritance = new();

    public IReadOnlyList< ModCollection > Inheritance
        => _inheritance;

    // Iterate over all collections inherited from in depth-first order.
    // Skip already visited collections to avoid circular dependencies.
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

    // Add a new collection to the inheritance list.
    // We do not check if this collection would be visited before,
    // only that it is unique in the list itself.
    public bool AddInheritance( ModCollection collection )
    {
        if( ReferenceEquals( collection, this ) || _inheritance.Contains( collection ) )
        {
            return false;
        }

        _inheritance.Add( collection );
        // Changes in inherited collections may need to trigger further changes here.
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

    // Order in the inheritance list is relevant.
    public void MoveInheritance( int from, int to )
    {
        if( _inheritance.Move( from, to ) )
        {
            InheritanceChanged.Invoke( false );
        }
    }

    // Carry changes in collections inherited from forward if they are relevant for this collection.
    private void OnInheritedModSettingChange( ModSettingChange type, int modIdx, int oldValue, string? optionName, bool _ )
    {
        if( _settings[ modIdx ] == null )
        {
            ModSettingChanged.Invoke( type, modIdx, oldValue, optionName, true );
        }
    }

    private void OnInheritedInheritanceChange( bool _ )
        => InheritanceChanged.Invoke( true );

    // Obtain the actual settings for a given mod via index.
    // Also returns the collection the settings are taken from.
    // If no collection provides settings for this mod, this collection is returned together with null.
    public (ModSettings? Settings, ModCollection Collection) this[ Index idx ]
    {
        get
        {
            foreach( var collection in GetFlattenedInheritance() )
            {
                var settings = collection._settings[ idx ];
                if( settings != null )
                {
                    return ( settings, collection );
                }
            }

            return ( null, this );
        }
    }
}