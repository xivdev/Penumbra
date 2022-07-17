using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using OtterGui;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;

namespace Penumbra.Api;

public enum RedirectResult
{
    Success                 = 0,
    IdenticalFileRegistered = 1,
    NotRegistered           = 2,
    FilteredGamePath        = 3,
}

public class TempModManager
{
    private readonly Dictionary< ModCollection, List< Mod.TemporaryMod > > _mods                  = new();
    private readonly List< Mod.TemporaryMod >                              _modsForAllCollections = new();
    private readonly Dictionary< string, ModCollection >                   _collections           = new();

    public IReadOnlyDictionary< ModCollection, List< Mod.TemporaryMod > > Mods
        => _mods;

    public IReadOnlyList< Mod.TemporaryMod > ModsForAllCollections
        => _modsForAllCollections;

    public IReadOnlyDictionary< string, ModCollection > Collections
        => _collections;

    public bool CollectionByName( string name, [NotNullWhen( true )] out ModCollection? collection )
        => Collections.Values.FindFirst( c => string.Equals( c.Name, name, StringComparison.OrdinalIgnoreCase ), out collection );

    // These functions to check specific redirections or meta manipulations for existence are currently unused.
    //public bool IsRegistered( string tag, ModCollection? collection, Utf8GamePath gamePath, out FullPath? fullPath, out int priority )
    //{
    //    var mod = GetExistingMod( tag, collection, null );
    //    if( mod == null )
    //    {
    //        priority = 0;
    //        fullPath = null;
    //        return false;
    //    }
    //
    //    priority = mod.Priority;
    //    if( mod.Default.Files.TryGetValue( gamePath, out var f ) )
    //    {
    //        fullPath = f;
    //        return true;
    //    }
    //
    //    fullPath = null;
    //    return false;
    //}
    //
    //public bool IsRegistered( string tag, ModCollection? collection, MetaManipulation meta, out MetaManipulation? manipulation,
    //    out int priority )
    //{
    //    var mod = GetExistingMod( tag, collection, null );
    //    if( mod == null )
    //    {
    //        priority     = 0;
    //        manipulation = null;
    //        return false;
    //    }
    //
    //    priority = mod.Priority;
    //    // IReadOnlySet has no TryGetValue for some reason.
    //    if( ( ( HashSet< MetaManipulation > )mod.Default.Manipulations ).TryGetValue( meta, out var manip ) )
    //    {
    //        manipulation = manip;
    //        return true;
    //    }
    //
    //    manipulation = null;
    //    return false;
    //}

    // These functions for setting single redirections or manips are currently unused.
    //public RedirectResult Register( string tag, ModCollection? collection, Utf8GamePath path, FullPath file, int priority )
    //{
    //    if( Mod.FilterFile( path ) )
    //    {
    //        return RedirectResult.FilteredGamePath;
    //    }
    //
    //    var mod = GetOrCreateMod( tag, collection, priority, out var created );
    //
    //    var changes = !mod.Default.Files.TryGetValue( path, out var oldFile ) || !oldFile.Equals( file );
    //    mod.SetFile( path, file );
    //    ApplyModChange( mod, collection, created, false );
    //    return changes ? RedirectResult.IdenticalFileRegistered : RedirectResult.Success;
    //}
    //
    //public RedirectResult Register( string tag, ModCollection? collection, MetaManipulation meta, int priority )
    //{
    //    var mod = GetOrCreateMod( tag, collection, priority, out var created );
    //    var changes = !( ( HashSet< MetaManipulation > )mod.Default.Manipulations ).TryGetValue( meta, out var oldMeta )
    //     || !oldMeta.Equals( meta );
    //    mod.SetManipulation( meta );
    //    ApplyModChange( mod, collection, created, false );
    //    return changes ? RedirectResult.IdenticalFileRegistered : RedirectResult.Success;
    //}

    public RedirectResult Register( string tag, ModCollection? collection, Dictionary< Utf8GamePath, FullPath > dict,
        HashSet< MetaManipulation > manips, int priority )
    {
        var mod = GetOrCreateMod( tag, collection, priority, out var created );
        mod.SetAll( dict, manips );
        ApplyModChange( mod, collection, created, false );
        return RedirectResult.Success;
    }

    public RedirectResult Unregister( string tag, ModCollection? collection, int? priority )
    {
        var list = collection == null ? _modsForAllCollections : _mods.TryGetValue( collection, out var l ) ? l : null;
        if( list == null )
        {
            return RedirectResult.NotRegistered;
        }

        var removed = list.RemoveAll( m =>
        {
            if( m.Name != tag || priority != null && m.Priority != priority.Value )
            {
                return false;
            }

            ApplyModChange( m, collection, false, true );
            return true;
        } );

        if( removed == 0 )
        {
            return RedirectResult.NotRegistered;
        }

        if( list.Count == 0 && collection != null )
        {
            _mods.Remove( collection );
        }

        return RedirectResult.Success;
    }

    public string SetTemporaryCollection( string tag, string characterName )
    {
        var collection = ModCollection.CreateNewTemporary( tag, characterName );
        _collections[ characterName ] = collection;
        return collection.Name;
    }

    public bool RemoveTemporaryCollection( string characterName )
    {
        if( _collections.Remove( characterName, out var c ) )
        {
            _mods.Remove( c );
            c.ClearCache();
            return true;
        }

        return false;
    }


    // Apply any new changes to the temporary mod.
    private static void ApplyModChange( Mod.TemporaryMod mod, ModCollection? collection, bool created, bool removed )
    {
        if( collection == null )
        {
            if( removed )
            {
                foreach( var c in Penumbra.CollectionManager )
                {
                    c.Remove( mod );
                }
            }
            else
            {
                foreach( var c in Penumbra.CollectionManager )
                {
                    c.Apply( mod, created );
                }
            }
        }
        else
        {
            if( removed )
            {
                collection.Remove( mod );
            }
            else
            {
                collection.Apply( mod, created );
            }
        }
    }

    // Only find already existing mods, currently unused.
    //private Mod.TemporaryMod? GetExistingMod( string tag, ModCollection? collection, int? priority )
    //{
    //    var list = collection == null ? _modsForAllCollections : _mods.TryGetValue( collection, out var l ) ? l : null;
    //    if( list == null )
    //    {
    //        return null;
    //    }
    //
    //    if( priority != null )
    //    {
    //        return list.Find( m => m.Priority == priority.Value && m.Name == tag );
    //    }
    //
    //    Mod.TemporaryMod? highestMod      = null;
    //    var               highestPriority = int.MinValue;
    //    foreach( var m in list )
    //    {
    //        if( highestPriority < m.Priority && m.Name == tag )
    //        {
    //            highestPriority = m.Priority;
    //            highestMod      = m;
    //        }
    //    }
    //
    //    return highestMod;
    //}

    // Find or create a mod with the given tag as name and the given priority, for the given collection (or all collections).
    // Returns the found or created mod and whether it was newly created.
    private Mod.TemporaryMod GetOrCreateMod( string tag, ModCollection? collection, int priority, out bool created )
    {
        List< Mod.TemporaryMod > list;
        if( collection == null )
        {
            list = _modsForAllCollections;
        }
        else if( _mods.TryGetValue( collection, out var l ) )
        {
            list = l;
        }
        else
        {
            list = new List< Mod.TemporaryMod >();
            _mods.Add( collection, list );
        }

        var mod = list.Find( m => m.Priority == priority && m.Name == tag );
        if( mod == null )
        {
            mod = new Mod.TemporaryMod()
            {
                Name     = tag,
                Priority = priority,
            };
            list.Add( mod );
            created = true;
        }
        else
        {
            created = false;
        }

        return mod;
    }
}