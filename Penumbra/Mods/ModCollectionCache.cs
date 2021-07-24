using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.GameData.Util;
using Penumbra.Meta;
using Penumbra.Mod;
using Penumbra.Util;

namespace Penumbra.Mods
{
    // The ModCollectionCache contains all required temporary data to use a collection.
    // It will only be setup if a collection gets activated in any way.
    public class ModCollectionCache
    {
        public readonly List< Mod.Mod > AvailableMods = new();

        public readonly Dictionary< GamePath, FileInfo > ResolvedFiles = new();
        public readonly Dictionary< GamePath, GamePath > SwappedFiles  = new();
        public readonly MetaManager                      MetaManipulations;

        public ModCollectionCache( string collectionName, DirectoryInfo modDir )
            => MetaManipulations = new MetaManager( collectionName, ResolvedFiles, modDir );

        public void SortMods()
        {
            AvailableMods.Sort( ( m1, m2 )
                => string.Compare( m1.Data.SortOrder, m2.Data.SortOrder, StringComparison.InvariantCultureIgnoreCase ) );
        }

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

            foreach( var mod in AvailableMods.Where( m => m.Settings.Enabled && m.Data.Resources.MetaManipulations.Count > 0 )
               .OrderByDescending( m => m.Settings.Priority ) )
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
            foreach( var mod in AvailableMods.Where( m => m.Settings.Enabled ).OrderByDescending( m => m.Settings.Priority ) )
            {
                mod.Cache.ClearFileConflicts();
                AddFiles( registeredFiles, mod );
                AddSwaps( registeredFiles, mod );
            }
        }

        public void RemoveMod( DirectoryInfo basePath )
        {
            var hadMeta    = false;
            var wasEnabled = false;
            AvailableMods.RemoveAll( m =>
            {
                if( m.Settings.Enabled )
                {
                    wasEnabled =  true;
                    hadMeta    |= m.Data.Resources.MetaManipulations.Count > 0;
                }

                return m.Data.BasePath.Name == basePath.Name;
            } );

            if( wasEnabled )
            {
                CalculateEffectiveFileList();
                if( hadMeta )
                {
                    UpdateMetaManipulations();
                }
            }
        }

        public void AddMod( ModSettings settings, ModData data )
        {
            AvailableMods.Add( new Mod.Mod( settings, data ) );
            SortMods();
            if( settings.Enabled )
            {
                CalculateEffectiveFileList();
                if( data.Resources.MetaManipulations.Count > 0 )
                {
                    UpdateMetaManipulations();
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