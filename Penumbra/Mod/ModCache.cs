using System.Collections.Generic;
using System.Linq;
using Penumbra.GameData.ByteString;
using Penumbra.Meta;

namespace Penumbra.Mod;

// The ModCache contains volatile information dependent on all current settings in a collection.
public class ModCache
{
    public Dictionary< Mod, (List< Utf8GamePath > Files, List< MetaManipulation > Manipulations) > Conflicts { get; private set; } = new();

    public void AddConflict( Mod precedingMod, Utf8GamePath gamePath )
    {
        if( Conflicts.TryGetValue( precedingMod, out var conflicts ) && !conflicts.Files.Contains( gamePath ) )
        {
            conflicts.Files.Add( gamePath );
        }
        else
        {
            Conflicts[ precedingMod ] = ( new List< Utf8GamePath > { gamePath }, new List< MetaManipulation >() );
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
            Conflicts[ precedingMod ] = ( new List< Utf8GamePath >(), new List< MetaManipulation > { manipulation } );
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