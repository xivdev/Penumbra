using System.Collections.Generic;
using System.Linq;
using Penumbra.Meta;
using Penumbra.Util;

namespace Penumbra.Mod
{
    public class ModCache
    {
        public Dictionary< Mod, (List< GamePath > Files, List< MetaManipulation > Manipulations) > Conflicts { get; private set; } = new();

        public void AddConflict( Mod precedingMod, GamePath gamePath )
        {
            if( Conflicts.TryGetValue( precedingMod, out var conflicts ) && !conflicts.Files.Contains( gamePath ) )
            {
                conflicts.Files.Add( gamePath );
            }
            else
            {
                Conflicts[ precedingMod ] = ( new List< GamePath > { gamePath }, new List< MetaManipulation >() );
            }
        }

        public void AddConflict( Mod precedingMod, MetaManipulation manipulation )
        {
            if( Conflicts.TryGetValue( precedingMod, out var conflicts ) && !conflicts.Manipulations.Contains( manipulation ) )
            {
                conflicts.Manipulations.Add( manipulation );
            }
            else
            {
                Conflicts[ precedingMod ] = ( new List< GamePath >(), new List< MetaManipulation > { manipulation } );
            }
        }

        public void ClearConflicts()
            => Conflicts.Clear();

        public void ClearFileConflicts()
        {
            Conflicts = Conflicts.Where( kvp => kvp.Value.Manipulations.Count > 0 ).ToDictionary( kvp => kvp.Key, kvp =>
            {
                kvp.Value.Files.Clear();
                return kvp.Value;
            } );
        }

        public void ClearMetaConflicts()
        {
            Conflicts = Conflicts.Where( kvp => kvp.Value.Files.Count > 0 ).ToDictionary( kvp => kvp.Key, kvp =>
            {
                kvp.Value.Manipulations.Clear();
                return kvp.Value;
            } );
        }
    }
}