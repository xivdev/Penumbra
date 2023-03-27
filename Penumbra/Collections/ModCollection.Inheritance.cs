using OtterGui.Filesystem;
using Penumbra.Mods;
using System;
using System.Collections.Generic;
using System.Linq;
using Penumbra.Api.Enums;

namespace Penumbra.Collections;

// ModCollections can inherit from an arbitrary number of other collections.
// This is transitive, so a collection A inheriting from B also inherits from everything B inherits.
// Circular dependencies are resolved by distinctness.
public partial class ModCollection
{
    // A change in inheritance usually requires complete recomputation.
    // The bool signifies whether the change was in an already inherited collection.
    public event Action< bool > InheritanceChanged;

    internal readonly List< ModCollection > _inheritance = new();

    public IReadOnlyList< ModCollection > Inheritance
        => _inheritance;

    // Iterate over all collections inherited from in depth-first order.
    // Skip already visited collections to avoid circular dependencies.
    public IEnumerable< ModCollection > GetFlattenedInheritance()
        => InheritedCollections( this ).Distinct();

    // All inherited collections in application order without filtering for duplicates.
    private static IEnumerable< ModCollection > InheritedCollections( ModCollection collection )
        => collection.Inheritance.SelectMany( InheritedCollections ).Prepend( collection );

    // Reasons why a collection can not be inherited from.
    public enum ValidInheritance
    {
        Valid,
        Self,      // Can not inherit from self
        Empty,     // Can not inherit from the empty collection
        Contained, // Already inherited from
        Circle,    // Inheritance would lead to a circle.
    }

    // Check whether a collection can be inherited from.
    public ValidInheritance CheckValidInheritance( ModCollection? collection )
    {
        if( collection == null || ReferenceEquals( collection, Empty ) )
        {
            return ValidInheritance.Empty;
        }

        if( ReferenceEquals( collection, this ) )
        {
            return ValidInheritance.Self;
        }

        if( _inheritance.Contains( collection ) )
        {
            return ValidInheritance.Contained;
        }

        if( InheritedCollections( collection ).Any( c => c == this ) )
        {
            return ValidInheritance.Circle;
        }

        return ValidInheritance.Valid;
    }

    // Add a new collection to the inheritance list.
    // We do not check if this collection would be visited before,
    // only that it is unique in the list itself.
    public bool AddInheritance( ModCollection collection, bool invokeEvent )
    {
        if( CheckValidInheritance( collection ) != ValidInheritance.Valid )
        {
            return false;
        }

        _inheritance.Add( collection );
        // Changes in inherited collections may need to trigger further changes here.
        collection.ModSettingChanged  += OnInheritedModSettingChange;
        collection.InheritanceChanged += OnInheritedInheritanceChange;
        if( invokeEvent )
        {
            InheritanceChanged.Invoke( false );
        }

        Penumbra.Log.Debug( $"Added {collection.AnonymizedName} to {AnonymizedName} inheritances." );
        return true;
    }

    public void RemoveInheritance( int idx )
    {
        var inheritance = _inheritance[ idx ];
        ClearSubscriptions( inheritance );
        _inheritance.RemoveAt( idx );
        InheritanceChanged.Invoke( false );
        Penumbra.Log.Debug( $"Removed {inheritance.AnonymizedName} from {AnonymizedName} inheritances." );
    }

    internal void ClearSubscriptions( ModCollection other )
    {
        other.ModSettingChanged  -= OnInheritedModSettingChange;
        other.InheritanceChanged -= OnInheritedInheritanceChange;
    }

    // Order in the inheritance list is relevant.
    public void MoveInheritance( int from, int to )
    {
        if( _inheritance.Move( from, to ) )
        {
            InheritanceChanged.Invoke( false );
            Penumbra.Log.Debug( $"Moved {AnonymizedName}s inheritance {from} to {to}." );
        }
    }

    // Carry changes in collections inherited from forward if they are relevant for this collection.
    private void OnInheritedModSettingChange( ModSettingChange type, int modIdx, int oldValue, int groupIdx, bool _ )
    {
        switch( type )
        {
            case ModSettingChange.MultiInheritance:
            case ModSettingChange.MultiEnableState:
                ModSettingChanged.Invoke( type, modIdx, oldValue, groupIdx, true );
                return;
            default:
                if( modIdx < 0 || modIdx >= _settings.Count )
                {
                    Penumbra.Log.Warning(
                        $"Collection state broken, Mod {modIdx} in inheritance does not exist. ({_settings.Count} mods exist)." );
                    return;
                }

                if( _settings[ modIdx ] == null )
                {
                    ModSettingChanged.Invoke( type, modIdx, oldValue, groupIdx, true );
                }

                return;
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
            if( Index <= 0 )
            {
                return ( ModSettings.Empty, this );
            }

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