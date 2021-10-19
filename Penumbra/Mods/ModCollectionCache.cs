using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Logging;
using Penumbra.GameData.Util;
using Penumbra.Meta;
using Penumbra.Mod;
using Penumbra.Structs;
using Penumbra.Util;

namespace Penumbra.Mods
{
    // The ModCollectionCache contains all required temporary data to use a collection.
    // It will only be setup if a collection gets activated in any way.
    public class ModCollectionCache
    {
        // Shared caches to avoid allocations.
        private static readonly BitArray                        FileSeen        = new( 256 );
        private static readonly Dictionary< GamePath, Mod.Mod > RegisteredFiles = new( 256 );

        public readonly Dictionary< string, Mod.Mod > AvailableMods = new();

        public readonly Dictionary< GamePath, FileInfo > ResolvedFiles = new();
        public readonly Dictionary< GamePath, GamePath > SwappedFiles  = new();
        public readonly HashSet< FileInfo >              MissingFiles  = new();
        public readonly MetaManager                      MetaManipulations;

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

        private void AddFile( Mod.Mod mod, GamePath gamePath, FileInfo file )
        {
            if( !RegisteredFiles.TryGetValue( gamePath, out var oldMod ) )
            {
                RegisteredFiles.Add( gamePath, mod );
                ResolvedFiles[ gamePath ] = file;
            }
            else
            {
                mod.Cache.AddConflict( oldMod, gamePath );
                if( !ReferenceEquals( mod, oldMod ) && mod.Settings.Priority == oldMod.Settings.Priority)
                {
                    oldMod.Cache.AddConflict( mod, gamePath );
                }
            }
        }

        private void AddMissingFile( FileInfo file )
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
                var fullPath = Path.Combine( mod.Data.BasePath.FullName, file );
                var idx      = mod.Data.Resources.ModFiles.IndexOf( f => f.FullName == fullPath );
                if( idx < 0 )
                {
                    AddMissingFile( new FileInfo( fullPath ) );
                    continue;
                }

                var registeredFile = mod.Data.Resources.ModFiles[ idx ];
                registeredFile.Refresh();
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
                file.Refresh();
                if( file.Exists )
                {
                    AddFile( mod, new GamePath( file, mod.Data.BasePath ), file );
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
            foreach( var swap in mod.Data.Meta.FileSwaps )
            {
                if( !RegisteredFiles.TryGetValue( swap.Key, out var oldMod ) )
                {
                    RegisteredFiles.Add( swap.Key, mod );
                    SwappedFiles.Add( swap.Key, swap.Value );
                }
                else
                {
                    mod.Cache.AddConflict( oldMod, swap.Key );
                    if( !ReferenceEquals( mod, oldMod ) && mod.Settings.Priority == oldMod.Settings.Priority )
                    {
                        oldMod.Cache.AddConflict( mod, swap.Key );
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

        public FileInfo? GetCandidateForGameFile( GamePath gameResourcePath )
        {
            if( !ResolvedFiles.TryGetValue( gameResourcePath, out var candidate ) )
            {
                return null;
            }

            candidate.Refresh();
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
}