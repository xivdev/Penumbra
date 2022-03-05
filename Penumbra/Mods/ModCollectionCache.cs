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
using Penumbra.Structs;
using Penumbra.Util;

namespace Penumbra.Mods;

// The ModCollectionCache contains all required temporary data to use a collection.
// It will only be setup if a collection gets activated in any way.
public class ModCollectionCache
{
    // Shared caches to avoid allocations.
    private static readonly BitArray                        FileSeen        = new(256);
    private static readonly Dictionary< GamePath, Mod.Mod > RegisteredFiles = new(256);

    public readonly Dictionary< string, Mod.Mod > AvailableMods = new();

    private readonly SortedList< string, object? >    _changedItems = new();
    public readonly  Dictionary< GamePath, FullPath > ResolvedFiles = new();
    public readonly  Dictionary< GamePath, GamePath > SwappedFiles  = new();
    public readonly  HashSet< FullPath >              MissingFiles  = new();
    public readonly  HashSet< ulong >                 Checksums     = new();
    public readonly  MetaManager                      MetaManipulations;

    public IReadOnlyDictionary< string, object? > ChangedItems
    {
        get
        {
            SetChangedItems();
            return _changedItems;
        }
    }

    public ModCollectionCache( string collectionName, DirectoryInfo tempDir )
        => MetaManipulations = new MetaManager( collectionName, ResolvedFiles, tempDir );

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
        SwappedFiles.Clear();
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
        Checksums.Clear();
        foreach( var file in ResolvedFiles )
        {
            Checksums.Add( file.Value.Crc64 );
        }
    }

    private void SetChangedItems()
    {
        if( _changedItems.Count > 0 || ResolvedFiles.Count + SwappedFiles.Count + MetaManipulations.Count == 0 )
        {
            return;
        }

        try
        {
            // Skip meta files because IMCs would result in far too many false-positive items,
            // since they are per set instead of per item-slot/item/variant.
            var metaFiles  = MetaManipulations.Files.Select( p => p.Item1 ).ToHashSet();
            var identifier = GameData.GameData.GetIdentifier();
            foreach( var resolved in ResolvedFiles.Keys.Where( file => !metaFiles.Contains( file ) ) )
            {
                identifier.Identify( _changedItems, resolved );
            }

            foreach( var swapped in SwappedFiles.Keys )
            {
                identifier.Identify( _changedItems, swapped );
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

    private bool FilterFile( GamePath gamePath )
    {
        // If audio streaming is not disabled, replacing .scd files crashes the game,
        // so only add those files if it is disabled.
        if( !Penumbra.Config.DisableSoundStreaming
        && gamePath.ToString().EndsWith( ".scd", StringComparison.InvariantCultureIgnoreCase ) )
        {
            return true;
        }

        return false;
    }


    private void AddFile( Mod.Mod mod, GamePath gamePath, FullPath file )
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
            var fullPath = new FullPath( mod.Data.BasePath,
                NewRelPath.FromString( file.ToString(), out var p ) ? p : NewRelPath.Empty ); // TODO
            var idx = mod.Data.Resources.ModFiles.IndexOf( f => f.Equals( fullPath ) );
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
                    AddFile( mod, new GamePath( gamePath.ToString() ), file ); // TODO
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
    {
        foreach( var (gamePath, file) in MetaManipulations.Files )
        {
            if( RegisteredFiles.TryGetValue( gamePath, out var mod ) )
            {
                PluginLog.Warning(
                    $"The meta manipulation file {gamePath} was already completely replaced by {mod.Data.Meta.Name}. This is probably a mistake. Using the custom file {file.FullName}." );
            }

            ResolvedFiles[ gamePath ] = file;
        }
    }

    private void AddSwaps( Mod.Mod mod )
    {
        foreach( var (key, value) in mod.Data.Meta.FileSwaps.Where( kvp => !FilterFile( kvp.Key ) ) )
        {
            if( !RegisteredFiles.TryGetValue( key, out var oldMod ) )
            {
                RegisteredFiles.Add( key, mod );
                SwappedFiles.Add( key, value );
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
                mod.Cache.AddConflict( oldMod, manip );
                if( !ReferenceEquals( mod, oldMod ) && mod.Settings.Priority == oldMod.Settings.Priority )
                {
                    oldMod.Cache.AddConflict( mod, manip );
                }
            }
        }
    }

    public void UpdateMetaManipulations()
    {
        MetaManipulations.Reset( false );

        foreach( var mod in AvailableMods.Values.Where( m => m.Settings.Enabled && m.Data.Resources.MetaManipulations.Count > 0 ) )
        {
            mod.Cache.ClearMetaConflicts();
            AddManipulations( mod );
        }

        MetaManipulations.WriteNewFiles();
    }

    public void RemoveMod( DirectoryInfo basePath )
    {
        if( AvailableMods.TryGetValue( basePath.Name, out var mod ) )
        {
            AvailableMods.Remove( basePath.Name );
            if( mod.Settings.Enabled )
            {
                CalculateEffectiveFileList();
                if( mod.Data.Resources.MetaManipulations.Count > 0 )
                {
                    UpdateMetaManipulations();
                }
            }
        }
    }

    private class PriorityComparer : IComparer< Mod.Mod >
    {
        public int Compare( Mod.Mod? x, Mod.Mod? y )
            => ( x?.Settings.Priority ?? 0 ).CompareTo( y?.Settings.Priority ?? 0 );
    }

    private static readonly PriorityComparer Comparer = new();

    public void AddMod( ModSettings settings, ModData data, bool updateFileList = true )
    {
        if( !AvailableMods.TryGetValue( data.BasePath.Name, out var existingMod ) )
        {
            var newMod = new Mod.Mod( settings, data );
            AvailableMods[ data.BasePath.Name ] = newMod;

            if( updateFileList && settings.Enabled )
            {
                CalculateEffectiveFileList();
                if( data.Resources.MetaManipulations.Count > 0 )
                {
                    UpdateMetaManipulations();
                }
            }
        }
    }

    public FullPath? GetCandidateForGameFile( GamePath gameResourcePath )
    {
        if( !ResolvedFiles.TryGetValue( gameResourcePath, out var candidate ) )
        {
            return null;
        }

        if( candidate.FullName.Length >= 260 || !candidate.Exists )
        {
            return null;
        }

        return candidate;
    }

    public GamePath? GetSwappedFilePath( GamePath gameResourcePath )
        => SwappedFiles.TryGetValue( gameResourcePath, out var swappedPath ) ? swappedPath : null;

    public string? ResolveSwappedOrReplacementPath( GamePath gameResourcePath )
        => GetCandidateForGameFile( gameResourcePath )?.FullName.Replace( '\\', '/' ) ?? GetSwappedFilePath( gameResourcePath ) ?? null;
}