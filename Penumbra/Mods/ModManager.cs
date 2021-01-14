using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms.VisualStyles;
using Penumbra.Models;
using Swan.Logging;

namespace Penumbra.Mods
{
    public class ModManager : IDisposable
    {
        private readonly Plugin _plugin;
        public readonly Dictionary< string, FileInfo > ResolvedFiles = new();

        public ModCollection Mods { get; set; }

        private DirectoryInfo _basePath;

        public ModManager( Plugin plugin )
        {
            _plugin = plugin;
        }

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
            
            // Needed to reload body textures with mods
            _plugin.GameUtils.ReloadPlayerResources();
        }

        public void CalculateEffectiveFileList()
        {
            ResolvedFiles.Clear();

            var registeredFiles = new Dictionary< string, string >();

            foreach( var (mod, settings) in Mods.GetOrderedAndEnabledModListWithSettings() )
            {
                mod.FileConflicts?.Clear();

                // fixup path
                var baseDir = mod.ModBasePath.FullName;

                foreach( var file in mod.ModFiles )
                {
                    var relativeFilePath = file.FullName.Substring( baseDir.Length ).TrimStart( '\\' );

                    string gamePath;
                    bool   addFile = true;
                    (string, uint, uint, ulong) tuple;
                    if (mod.Meta.Groups.FileToGameAndGroup.TryGetValue(relativeFilePath, out tuple))
                    {
                        gamePath = tuple.Item1;
                        var (_, tops, bottoms, excludes) = tuple;
                        var validTop    = ((1u  << settings.CurrentTop)    & tops    ) != 0;
                        var validBottom = ((1u  << settings.CurrentBottom) & bottoms ) != 0;
                        var validGroup  = ((1ul << settings.CurrentGroup)  & excludes) != 0;
                        addFile = validTop && validBottom && validGroup;
                    }
                    else
                        gamePath = relativeFilePath.Replace( '\\', '/' );
                    
                    if ( addFile )
                    {
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

        public string ResolveReplacementFilePath( string gameResourcePath )
        {
            gameResourcePath = gameResourcePath.ToLowerInvariant();

            return GetCandidateForGameFile( gameResourcePath )?.FullName;
        }

        public void Dispose()
        {
            // _fileSystemWatcher?.Dispose();
        }
    }
}