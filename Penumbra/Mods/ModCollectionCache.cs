using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumina.Data.Parsing;
using Penumbra.GameData.Util;
using Penumbra.Meta;
using Penumbra.Mod;

namespace Penumbra.Mods
{
    // The ModCollectionCache contains all required temporary data to use a collection.
    // It will only be setup if a collection gets activated in any way.
    public class ModCollectionCache
    {
        public readonly Dictionary< string, Mod.Mod > AvailableMods = new();

        public readonly Dictionary< GamePath, FileInfo > ResolvedFiles = new();
        public readonly Dictionary< GamePath, GamePath > SwappedFiles  = new();
        public readonly MetaManager                      MetaManipulations;

        public ModCollectionCache( string collectionName, DirectoryInfo tempDir )
            => MetaManipulations = new MetaManager( collectionName, ResolvedFiles, tempDir );

        private void AddFiles( Dictionary< GamePath, Mod.Mod > registeredFiles, Mod.Mod mod )
        {
            foreach( var file in mod.Data.Resources.ModFiles )
            {
                var gamePaths = mod.GetFiles( file );
                foreach( var gamePath in gamePaths )
                {
                    if( !registeredFiles.TryGetValue( gamePath, out var oldMod ) )
                    {
                        registeredFiles.Add( gamePath, mod );
                        ResolvedFiles[ gamePath ] = file;
                    }
                    else
                    {
                        mod.Cache.AddConflict( oldMod, gamePath );
                    }
                }
            }
        }

        private void AddSwaps( Dictionary< GamePath, Mod.Mod > registeredFiles, Mod.Mod mod )
        {
            foreach( var swap in mod.Data.Meta.FileSwaps )
            {
                if( !registeredFiles.TryGetValue( swap.Key, out var oldMod ) )
                {
                    registeredFiles.Add( swap.Key, mod );
                    SwappedFiles.Add( swap.Key, swap.Value );
                }
                else
                {
                    mod.Cache.AddConflict( oldMod, swap.Key );
                }
            }
        }

        private void AddManipulations( Mod.Mod mod )
        {
            foreach( var manip in mod.Data.Resources.MetaManipulations.GetManipulationsForConfig( mod.Settings, mod.Data.Meta ) )
            {
                if( MetaManipulations.TryGetValue( manip, out var precedingMod ) )
                {
                    mod.Cache.AddConflict( precedingMod, manip );
                }
                else
                {
                    MetaManipulations.ApplyMod( manip, mod );
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

        public void CalculateEffectiveFileList()
        {
            ResolvedFiles.Clear();
            SwappedFiles.Clear();

            var registeredFiles = new Dictionary< GamePath, Mod.Mod >();
            foreach( var mod in AvailableMods.Values
               .Where( m => m.Settings.Enabled )
               .OrderByDescending( m => m.Settings.Priority ) )
            {
                mod.Cache.ClearFileConflicts();
                AddFiles( registeredFiles, mod );
                AddSwaps( registeredFiles, mod );
            }
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
            public int Compare( Mod.Mod x, Mod.Mod y )
                => x.Settings.Priority.CompareTo( y.Settings.Priority );
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