using System;
using System.Collections.Generic;
using System.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections;

public struct ConflictCache
{
    public readonly struct ModCacheStruct : IComparable< ModCacheStruct >
    {
        public readonly object Conflict;
        public readonly int    Mod1;
        public readonly int    Mod2;
        public readonly bool   Mod1Priority;
        public readonly bool   Solved;

        public ModCacheStruct( int modIdx1, int modIdx2, int priority1, int priority2, object conflict )
        {
            Mod1         = modIdx1;
            Mod2         = modIdx2;
            Conflict     = conflict;
            Mod1Priority = priority1 >= priority2;
            Solved       = priority1 != priority2;
        }

        public int CompareTo( ModCacheStruct other )
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

            return Conflict switch
            {
                Utf8GamePath p when other.Conflict is Utf8GamePath q         => p.CompareTo( q ),
                Utf8GamePath                                                 => -1,
                MetaManipulation m when other.Conflict is MetaManipulation n => m.CompareTo( n ),
                MetaManipulation                                             => 1,
                _                                                            => 0,
            };
        }
    }

    private List< ModCacheStruct >? _conflicts;

    public IReadOnlyList< ModCacheStruct > Conflicts
        => _conflicts ?? ( IReadOnlyList< ModCacheStruct > )Array.Empty< ModCacheStruct >();

    public IEnumerable< ModCacheStruct > ModConflicts( int modIdx )
    {
        return _conflicts?.SkipWhile( c => c.Mod1 < modIdx ).TakeWhile( c => c.Mod1 == modIdx )
         ?? Array.Empty< ModCacheStruct >();
    }

    public void Sort()
        => _conflicts?.Sort();

    public void AddConflict( int modIdx1, int modIdx2, int priority1, int priority2, Utf8GamePath gamePath )
    {
        _conflicts ??= new List< ModCacheStruct >( 2 );

        _conflicts.Add( new ModCacheStruct( modIdx1, modIdx2, priority1, priority2, gamePath ) );
        _conflicts.Add( new ModCacheStruct( modIdx2, modIdx1, priority2, priority1, gamePath ) );
    }

    public void AddConflict( int modIdx1, int modIdx2, int priority1, int priority2, MetaManipulation manipulation )
    {
        _conflicts ??= new List< ModCacheStruct >( 2 );
        _conflicts.Add( new ModCacheStruct( modIdx1, modIdx2, priority1, priority2, manipulation ) );
        _conflicts.Add( new ModCacheStruct( modIdx2, modIdx1, priority2, priority1, manipulation ) );
    }

    public void ClearConflicts()
        => _conflicts?.Clear();

    public void ClearFileConflicts()
        => _conflicts?.RemoveAll( m => m.Conflict is Utf8GamePath );

    public void ClearMetaConflicts()
        => _conflicts?.RemoveAll( m => m.Conflict is MetaManipulation );

    public void ClearConflictsWithMod( int modIdx )
        => _conflicts?.RemoveAll( m => m.Mod1 == modIdx || m.Mod2 == ~modIdx );
}