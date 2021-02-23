using System;
using System.Collections.Generic;
using System.IO;
using Dalamud.Plugin;
using Penumbra.Models;
using Penumbra.Util;

namespace Penumbra.Mods
{
    public class ModManager : IDisposable
    {
        private readonly Plugin                           _plugin;
        public readonly  Dictionary< GamePath, FileInfo > ResolvedFiles = new();
        public readonly  Dictionary< GamePath, GamePath > SwappedFiles  = new();

        public ModCollection? Mods { get; set; }
        private DirectoryInfo? _basePath;

        public ModManager( Plugin plugin )
            => _plugin = plugin;

        public void DiscoverMods()
            => DiscoverMods( _basePath );

        public void DiscoverMods( string? basePath )
            => DiscoverMods( basePath == null ? null : new DirectoryInfo( basePath ) );

        public void DiscoverMods( DirectoryInfo? basePath )
        {
            _basePath = basePath;
            if( basePath == null || !basePath.Exists )
            {
                Mods = null;
                return;
            }

            // FileSystemPasta();

            Mods = new ModCollection( basePath );
            Mods.Load();

            CalculateEffectiveFileList();
        }

        public void CalculateEffectiveFileList()
        {
            ResolvedFiles.Clear();
            SwappedFiles.Clear();

            if( Mods == null )
            {
                return;
            }

            var changedSettings = false;
            var registeredFiles = new Dictionary< GamePath, string >();
            foreach( var (mod, settings) in Mods.GetOrderedAndEnabledModListWithSettings( _plugin!.Configuration!.InvertModListOrder ) )
            {
                mod.FileConflicts.Clear();

                changedSettings |= ProcessModFiles( registeredFiles, mod, settings );
                ProcessSwappedFiles( registeredFiles, mod, settings );
            }

            if (changedSettings)
                Mods.Save();

            _plugin!.GameUtils!.ReloadPlayerResources();
        }

        private void ProcessSwappedFiles( Dictionary< GamePath, string > registeredFiles, ResourceMod mod, ModInfo settings )
        {
            foreach( var swap in mod.Meta.FileSwaps )
            {
                // just assume people put not fucked paths in here lol
                if( !SwappedFiles.ContainsKey( swap.Value ) )
                {
                    SwappedFiles[ swap.Key ]    = swap.Value;
                    registeredFiles[ swap.Key ] = mod.Meta.Name;
                }
                else if( registeredFiles.TryGetValue( swap.Key, out var modName ) )
                {
                    mod.AddConflict( modName, swap.Key );
                }
            }
        }

        private bool ProcessModFiles( Dictionary< GamePath, string > registeredFiles, ResourceMod mod, ModInfo settings )
        {
            var changedConfig = settings.FixInvalidSettings();
            foreach( var file in mod.ModFiles )
            {
                RelPath relativeFilePath = new( file, mod.ModBasePath );
                var (configChanged, gamePaths) =  mod.Meta.GetFilesForConfig( relativeFilePath, settings );
                changedConfig                  |= configChanged;
                AddFiles( gamePaths, file, registeredFiles, mod );
            }

            return changedConfig;
        }

        private void AddFiles( IEnumerable< GamePath > gamePaths, FileInfo file, Dictionary< GamePath, string > registeredFiles,
            ResourceMod mod )
        {
            foreach( var gamePath in gamePaths )
            {
                if( !ResolvedFiles.ContainsKey( gamePath ) )
                {
                    ResolvedFiles[ gamePath ]   = file;
                    registeredFiles[ gamePath ] = mod.Meta.Name;
                }
                else if( registeredFiles.TryGetValue( gamePath, out var modName ) )
                {
                    mod.AddConflict( modName, gamePath );
                }
            }
        }

        public void ChangeModPriority( ModInfo info, bool up = false )
        {
            Mods!.ReorderMod( info, up );
            CalculateEffectiveFileList();
        }

        public void DeleteMod( ResourceMod? mod )
        {
            if( mod?.ModBasePath.Exists ?? false )
            {
                try
                {
                    Directory.Delete( mod.ModBasePath.FullName, true );
                }
                catch( Exception e )
                {
                    PluginLog.Error( $"Could not delete the mod {mod.ModBasePath.Name}:\n{e}" );
                }
            }

            DiscoverMods();
        }

        public FileInfo? GetCandidateForGameFile( GamePath gameResourcePath )
        {
            var val = ResolvedFiles.TryGetValue( gameResourcePath, out var candidate );
            if( !val )
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

        public string? ResolveSwappedOrReplacementFilePath( GamePath gameResourcePath )
            => GetCandidateForGameFile( gameResourcePath )?.FullName ?? GetSwappedFilePath( gameResourcePath ) ?? null;


        public void Dispose()
        {
            // _fileSystemWatcher?.Dispose();
        }

//         private void FileSystemWatcherOnChanged( object sender, FileSystemEventArgs e )
//         {
// #if DEBUG
//             PluginLog.Verbose( "file changed: {FullPath}", e.FullPath );
// #endif
//
//             if( _plugin.ImportInProgress )
//             {
//                 return;
//             }
//
//             if( _plugin.Configuration.DisableFileSystemNotifications )
//             {
//                 return;
//             }
//
//             var file = e.FullPath;
//
//             if( !ResolvedFiles.Any( x => x.Value.FullName == file ) )
//             {
//                 return;
//             }
//
//             PluginLog.Log( "a loaded file has been modified - file: {FullPath}", file );
//             _plugin.GameUtils.ReloadPlayerResources();
//         }
// 
//         private void FileSystemPasta()
//         {
//              haha spaghet
//              _fileSystemWatcher?.Dispose();
//              _fileSystemWatcher = new FileSystemWatcher( _basePath.FullName )
//              {
//                  NotifyFilter = NotifyFilters.LastWrite |
//                                 NotifyFilters.FileName |
//                                 NotifyFilters.DirectoryName,
//                  IncludeSubdirectories = true,
//                  EnableRaisingEvents = true
//              };
//             
//              _fileSystemWatcher.Changed += FileSystemWatcherOnChanged;
//              _fileSystemWatcher.Created += FileSystemWatcherOnChanged;
//              _fileSystemWatcher.Deleted += FileSystemWatcherOnChanged;
//              _fileSystemWatcher.Renamed += FileSystemWatcherOnChanged;
//         }
    }
}