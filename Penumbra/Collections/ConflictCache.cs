using System;
using System.Collections.Generic;
using OtterGui.Classes;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections;

public struct ConflictCache
{
    // A conflict stores all data about a mod conflict.
    public readonly struct Conflict : IComparable< Conflict >
    {
        public readonly object Data;
        public readonly int    Mod1;
        public readonly int    Mod2;
        public readonly bool   Mod1Priority;
        public readonly bool   Solved;

        public Conflict( int modIdx1, int modIdx2, bool priority, bool solved, object data )
        {
            Mod1         = modIdx1;
            Mod2         = modIdx2;
            Data         = data;
            Mod1Priority = priority;
            Solved       = solved;
        }

        // Order: Mod1 -> Mod1 overwritten -> Mod2 -> File > MetaManipulation
        public int CompareTo( Conflict other )
        {
            var idxComp = Mod1.CompareTo( other.Mod1 );
            if( idxComp != 0 )
            {
                return idxComp;
            }

            if( Mod1Priority != other.Mod1Priority )
            {
                return Mod1Priority ? 1 : -1;
            }

            idxComp = Mod2.CompareTo( other.Mod2 );
            if( idxComp != 0 )
            {
                return idxComp;
            }

            return Data switch
            {
                Utf8GamePath p when other.Data is Utf8GamePath q         => p.CompareTo( q ),
                Utf8GamePath                                             => -1,
                MetaManipulation m when other.Data is MetaManipulation n => m.CompareTo( n ),
                MetaManipulation                                         => 1,
                _                                                        => 0,
            };
        }

        public override string ToString()
            => ( Mod1Priority, Solved ) switch
            {
                (true, true)   => $"{Penumbra.ModManager[ Mod1 ].Name} >  {Penumbra.ModManager[ Mod2 ].Name} ({Data})",
                (true, false)  => $"{Penumbra.ModManager[ Mod1 ].Name} >= {Penumbra.ModManager[ Mod2 ].Name} ({Data})",
                (false, true)  => $"{Penumbra.ModManager[ Mod1 ].Name} <  {Penumbra.ModManager[ Mod2 ].Name} ({Data})",
                (false, false) => $"{Penumbra.ModManager[ Mod1 ].Name} <= {Penumbra.ModManager[ Mod2 ].Name} ({Data})",
            };
    }

    private readonly List< Conflict > _conflicts = new();
    private          bool             _isSorted  = true;

    public ConflictCache()
    { }

    public IReadOnlyList< Conflict > Conflicts
    {
        get
        {
            Sort();
            return _conflicts;
        }
    }

    // Find all mod conflicts concerning the specified mod (in both directions).
    public SubList< Conflict > ModConflicts( int modIdx )
    {
        Sort();
        var start = _conflicts.FindIndex( c => c.Mod1 == modIdx );
        if( start < 0 )
        {
            return SubList< Conflict >.Empty;
        }

        var end = _conflicts.FindIndex( start, c => c.Mod1 != modIdx );
        return new SubList< Conflict >( _conflicts, start, end - start );
    }

    private void Sort()
    {
        if( !_isSorted )
        {
            _conflicts?.Sort();
            _isSorted = true;
        }
    }

    // Add both directions for the mod.
    // On same priority, it is assumed that mod1 is the earlier one.
    // Also update older conflicts to refer to the highest-prioritized conflict.
    private void AddConflict( int modIdx1, int modIdx2, int priority1, int priority2, object data )
    {
        var solved         = priority1 != priority2;
        var priority       = priority1 >= priority2;
        var prioritizedMod = priority ? modIdx1 : modIdx2;
        _conflicts.Add( new Conflict( modIdx1, modIdx2, priority, solved, data ) );
        _conflicts.Add( new Conflict( modIdx2, modIdx1, !priority, solved, data ) );
        for( var i = 0; i < _conflicts.Count; ++i )
        {
            var c = _conflicts[ i ];
            if( data.Equals( c.Data ) )
            {
                _conflicts[ i ] = c.Mod1Priority
                    ? new Conflict( prioritizedMod, c.Mod2, true, c.Solved  || solved, data )
                    : new Conflict( c.Mod1, prioritizedMod, false, c.Solved || solved, data );
            }
        }

        _isSorted = false;
    }

    public void AddConflict( int modIdx1, int modIdx2, int priority1, int priority2, Utf8GamePath gamePath )
        => AddConflict( modIdx1, modIdx2, priority1, priority2, ( object )gamePath );

    public void AddConflict( int modIdx1, int modIdx2, int priority1, int priority2, MetaManipulation manipulation )
        => AddConflict( modIdx1, modIdx2, priority1, priority2, ( object )manipulation );

    public void ClearConflicts()
        => _conflicts?.Clear();

    public void ClearFileConflicts()
        => _conflicts?.RemoveAll( m => m.Data is Utf8GamePath );

    public void ClearMetaConflicts()
        => _conflicts?.RemoveAll( m => m.Data is MetaManipulation );

    public void ClearConflictsWithMod( int modIdx )
        => _conflicts?.RemoveAll( m => m.Mod1 == modIdx || m.Mod2 == modIdx );
}