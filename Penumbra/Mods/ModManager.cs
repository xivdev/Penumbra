using System;
using System.Collections.Generic;
using System.IO;
using Penumbra.Models;

namespace Penumbra.Mods
{
    public class ModManager : IDisposable
    {
        public readonly Dictionary< string, FileInfo > ResolvedFiles = new();
        public readonly Dictionary< string, string > SwappedFiles = new();

        public ModCollection Mods { get; set; }

        private DirectoryInfo _basePath;

        public void DiscoverMods()
        {
            if( _basePath == null )
            {
                return;
            }

            DiscoverMods( _basePath );
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

        public void DiscoverMods( string basePath )
        {
            DiscoverMods( new DirectoryInfo( basePath ) );
        }

        public void DiscoverMods( DirectoryInfo basePath )
        {
            if( basePath == null )
            {
                return;
            }

            if( !basePath.Exists )
            {
                Mods = null;
                return;
            }

            _basePath = basePath;

            // haha spaghet
            // _fileSystemWatcher?.Dispose();
            // _fileSystemWatcher = new FileSystemWatcher( _basePath.FullName )
            // {
            //     NotifyFilter = NotifyFilters.LastWrite |
            //                    NotifyFilters.FileName |
            //                    NotifyFilters.DirectoryName,
            //     IncludeSubdirectories = true,
            //     EnableRaisingEvents = true
            // };
            //
            // _fileSystemWatcher.Changed += FileSystemWatcherOnChanged;
            // _fileSystemWatcher.Created += FileSystemWatcherOnChanged;
            // _fileSystemWatcher.Deleted += FileSystemWatcherOnChanged;
            // _fileSystemWatcher.Renamed += FileSystemWatcherOnChanged;

            Mods = new ModCollection( basePath );
            Mods.Load();
            Mods.Save();

            CalculateEffectiveFileList();
        }

        public void CalculateEffectiveFileList()
        {
            ResolvedFiles.Clear();
            SwappedFiles.Clear();

            var registeredFiles = new Dictionary< string, string >();

            foreach( var mod in Mods.GetOrderedAndEnabledModList() )
            {
                mod.FileConflicts?.Clear();

                // fixup path
                var baseDir = mod.ModBasePath.FullName;

                foreach( var file in mod.ModFiles )
                {
                    var gamePath = file.FullName.Substring( baseDir.Length )
                        .TrimStart( '\\' ).Replace( '\\', '/' );

                    if( !ResolvedFiles.ContainsKey( gamePath ) )
                    {
                        ResolvedFiles[ gamePath.ToLowerInvariant() ] = file;
                        registeredFiles[ gamePath ] = mod.Meta.Name;
                    }
                    else if( registeredFiles.TryGetValue( gamePath, out var modName ) )
                    {
                        mod.AddConflict( modName, gamePath );
                    }
                }

                foreach( var swap in mod.Meta.FileSwaps )
                {
                    // just assume people put not fucked paths in here lol
                    if( !SwappedFiles.ContainsKey( swap.Value ) )
                    {
                        SwappedFiles[ swap.Key.ToLowerInvariant() ] = swap.Value;
                        registeredFiles[ swap.Key ] = mod.Meta.Name;
                    }
                    else if( registeredFiles.TryGetValue( swap.Key, out var modName ) )
                    {
                        mod.AddConflict( modName, swap.Key );
                    }
                }
            }
        }

        public void ChangeModPriority( ModInfo info, bool up = false )
        {
            Mods.ReorderMod( info, up );
            CalculateEffectiveFileList();
        }

        public void DeleteMod( ResourceMod mod )
        {
            Directory.Delete( mod.ModBasePath.FullName, true );
            DiscoverMods();
        }


        public FileInfo GetCandidateForGameFile( string gameResourcePath )
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

        public string GetSwappedFilePath( string gameResourcePath )
        {
            return SwappedFiles.TryGetValue( gameResourcePath, out var swappedPath ) ? swappedPath : null;
        }

        public string ResolveSwappedOrReplacementFilePath( string gameResourcePath )
        {
            gameResourcePath = gameResourcePath.ToLowerInvariant();

            return GetCandidateForGameFile( gameResourcePath )?.FullName ?? GetSwappedFilePath( gameResourcePath );
        }

        public void Dispose()
        {
            // _fileSystemWatcher?.Dispose();
        }
    }
}