using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Dalamud.Logging;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Util;
using Penumbra.Meta;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Mods;

// The ModCollectionCache contains all required temporary data to use a collection.
// It will only be setup if a collection gets activated in any way.
public class ModCollectionCache
{
    // Shared caches to avoid allocations.
    private static readonly BitArray                            FileSeen        = new(256);
    private static readonly Dictionary< Utf8GamePath, Mod.Mod > RegisteredFiles = new(256);

    public readonly Dictionary< string, Mod.Mod > AvailableMods = new();

    private readonly SortedList< string, object? >        _changedItems = new();
    public readonly  Dictionary< Utf8GamePath, FullPath > ResolvedFiles = new();
    public readonly  HashSet< FullPath >                  MissingFiles  = new();
    public readonly  MetaManager2                         MetaManipulations;

    public IReadOnlyDictionary< string, object? > ChangedItems
    {
        get
        {
            SetChangedItems();
            return _changedItems;
        }
    }

    public ModCollectionCache( ModCollection collection )
        => MetaManipulations = new MetaManager2( collection );

    private static void ResetFileSeen( int size )
    {
        if( size < FileSeen.Length )
        {
            FileSeen.Length = size;
            FileSeen.SetAll( false );
        }
        else
        {
            FileSeen.SetAll( false );
            FileSeen.Length = size;
        }
    }

    public void CalculateEffectiveFileList()
    {
        ResolvedFiles.Clear();
        MissingFiles.Clear();
        RegisteredFiles.Clear();
        _changedItems.Clear();

        foreach( var mod in AvailableMods.Values
                   .Where( m => m.Settings.Enabled )
                   .OrderByDescending( m => m.Settings.Priority ) )
        {
            mod.Cache.ClearFileConflicts();
            AddFiles( mod );
            AddSwaps( mod );
        }

        AddMetaFiles();
    }

    private void SetChangedItems()
    {
        if( _changedItems.Count > 0 || ResolvedFiles.Count + MetaManipulations.Count == 0 )
        {
            return;
        }

        try
        {
            // Skip IMCs because they would result in far too many false-positive items,
            // since they are per set instead of per item-slot/item/variant.
            var identifier = GameData.GameData.GetIdentifier();
            foreach( var resolved in ResolvedFiles.Keys.Where( file => !file.Path.EndsWith( 'i', 'm', 'c' ) ) )
            {
                identifier.Identify( _changedItems, resolved.ToGamePath() );
            }
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Unknown Error:\n{e}" );
        }
    }


    private void AddFiles( Mod.Mod mod )
    {
        ResetFileSeen( mod.Data.Resources.ModFiles.Count );
        // Iterate in reverse so that later groups take precedence before earlier ones.
        foreach( var group in mod.Data.Meta.Groups.Values.Reverse() )
        {
            switch( group.SelectionType )
            {
                case SelectType.Single:
                    AddFilesForSingle( group, mod );
                    break;
                case SelectType.Multi:
                    AddFilesForMulti( group, mod );
                    break;
                default: throw new InvalidEnumArgumentException();
            }
        }

        AddRemainingFiles( mod );
    }

    private static bool FilterFile( Utf8GamePath gamePath )
    {
        // If audio streaming is not disabled, replacing .scd files crashes the game,
        // so only add those files if it is disabled.
        if( !Penumbra.Config.DisableSoundStreaming
        && gamePath.Path.EndsWith( '.', 's', 'c', 'd' ) )
        {
            return true;
        }

        return false;
    }


    private void AddFile( Mod.Mod mod, Utf8GamePath gamePath, FullPath file )
    {
        if( FilterFile( gamePath ) )
        {
            return;
        }

        if( !RegisteredFiles.TryGetValue( gamePath, out var oldMod ) )
        {
            RegisteredFiles.Add( gamePath, mod );
            ResolvedFiles[ gamePath ] = file;
        }
        else
        {
            mod.Cache.AddConflict( oldMod, gamePath );
            if( !ReferenceEquals( mod, oldMod ) && mod.Settings.Priority == oldMod.Settings.Priority )
            {
                oldMod.Cache.AddConflict( mod, gamePath );
            }
        }
    }

    private void AddMissingFile( FullPath file )
    {
        switch( file.Extension.ToLowerInvariant() )
        {
            case ".meta":
            case ".rgsp":
                return;
            default:
                MissingFiles.Add( file );
                return;
        }
    }

    private void AddPathsForOption( Option option, Mod.Mod mod, bool enabled )
    {
        foreach( var (file, paths) in option.OptionFiles )
        {
            var fullPath = new FullPath( mod.Data.BasePath, file );
            var idx      = mod.Data.Resources.ModFiles.IndexOf( f => f.Equals( fullPath ) );
            if( idx < 0 )
            {
                AddMissingFile( fullPath );
                continue;
            }

            var registeredFile = mod.Data.Resources.ModFiles[ idx ];
            if( !registeredFile.Exists )
            {
                AddMissingFile( registeredFile );
                continue;
            }

            FileSeen.Set( idx, true );
            if( enabled )
            {
                foreach( var path in paths )
                {
                    AddFile( mod, path, registeredFile );
                }
            }
        }
    }

    private void AddFilesForSingle( OptionGroup singleGroup, Mod.Mod mod )
    {
        Debug.Assert( singleGroup.SelectionType == SelectType.Single );

        if( !mod.Settings.Settings.TryGetValue( singleGroup.GroupName, out var setting ) )
        {
            setting = 0;
        }

        for( var i = 0; i < singleGroup.Options.Count; ++i )
        {
            AddPathsForOption( singleGroup.Options[ i ], mod, setting == i );
        }
    }

    private void AddFilesForMulti( OptionGroup multiGroup, Mod.Mod mod )
    {
        Debug.Assert( multiGroup.SelectionType == SelectType.Multi );

        if( !mod.Settings.Settings.TryGetValue( multiGroup.GroupName, out var setting ) )
        {
            return;
        }

        // Also iterate options in reverse so that later options take precedence before earlier ones.
        for( var i = multiGroup.Options.Count - 1; i >= 0; --i )
        {
            AddPathsForOption( multiGroup.Options[ i ], mod, ( setting & ( 1 << i ) ) != 0 );
        }
    }

    private void AddRemainingFiles( Mod.Mod mod )
    {
        for( var i = 0; i < mod.Data.Resources.ModFiles.Count; ++i )
        {
            if( FileSeen.Get( i ) )
            {
                continue;
            }

            var file = mod.Data.Resources.ModFiles[ i ];
            if( file.Exists )
            {
                if( file.ToGamePath( mod.Data.BasePath, out var gamePath ) )
                {
                    AddFile( mod, gamePath, file );
                }
                else
                {
                    PluginLog.Warning( $"Could not convert {file} in {mod.Data.BasePath.FullName} to GamePath." );
                }
            }
            else
            {
                MissingFiles.Add( file );
            }
        }
    }

    private void AddMetaFiles()
        => MetaManipulations.ApplyImcFiles( ResolvedFiles );

    private void AddSwaps( Mod.Mod mod )
    {
        foreach( var (key, value) in mod.Data.Meta.FileSwaps.Where( kvp => !FilterFile( kvp.Key ) ) )
        {
            if( !RegisteredFiles.TryGetValue( key, out var oldMod ) )
            {
                RegisteredFiles.Add( key, mod );
                ResolvedFiles.Add( key, value );
            }
            else
            {
                mod.Cache.AddConflict( oldMod, key );
                if( !ReferenceEquals( mod, oldMod ) && mod.Settings.Priority == oldMod.Settings.Priority )
                {
                    oldMod.Cache.AddConflict( mod, key );
                }
            }
        }
    }

    private void AddManipulations( Mod.Mod mod )
    {
        foreach( var manip in mod.Data.Resources.MetaManipulations.GetManipulationsForConfig( mod.Settings, mod.Data.Meta ) )
        {
            if( !MetaManipulations.TryGetValue( manip, out var oldMod ) )
            {
                MetaManipulations.ApplyMod( manip, mod );
            }
            else
            {
                mod.Cache.AddConflict( oldMod!, manip );
                if( !ReferenceEquals( mod, oldMod ) && mod.Settings.Priority == oldMod!.Settings.Priority )
                {
                    oldMod.Cache.AddConflict( mod, manip );
                }
            }
        }
    }

    public void UpdateMetaManipulations()
    {
        MetaManipulations.Reset();

        foreach( var mod in AvailableMods.Values.Where( m => m.Settings.Enabled && m.Data.Resources.MetaManipulations.Count > 0 ) )
        {
            mod.Cache.ClearMetaConflicts();
            AddManipulations( mod );
        }
    }

    public void RemoveMod( DirectoryInfo basePath )
    {
        if( !AvailableMods.TryGetValue( basePath.Name, out var mod ) )
        {
            return;
        }

        AvailableMods.Remove( basePath.Name );
        if( !mod.Settings.Enabled )
        {
            return;
        }

        CalculateEffectiveFileList();
        if( mod.Data.Resources.MetaManipulations.Count > 0 )
        {
            UpdateMetaManipulations();
        }
    }

    public void AddMod( ModSettings settings, ModData data, bool updateFileList = true )
    {
        if( AvailableMods.ContainsKey( data.BasePath.Name ) )
        {
            return;
        }

        AvailableMods[ data.BasePath.Name ] = new Mod.Mod( settings, data );

        if( !updateFileList || !settings.Enabled )
        {
            return;
        }

        CalculateEffectiveFileList();
        if( data.Resources.MetaManipulations.Count > 0 )
        {
            UpdateMetaManipulations();
        }
    }

    public FullPath? GetCandidateForGameFile( Utf8GamePath gameResourcePath )
    {
        if( !ResolvedFiles.TryGetValue( gameResourcePath, out var candidate ) )
        {
            return null;
        }

        if( candidate.InternalName.Length > Utf8GamePath.MaxGamePathLength
        || candidate.IsRooted && !candidate.Exists )
        {
            return null;
        }

        return candidate;
    }

    public FullPath? ResolveSwappedOrReplacementPath( Utf8GamePath gameResourcePath )
        => GetCandidateForGameFile( gameResourcePath );
}