using System;
using System.Collections.Generic;
using System.IO;
using Penumbra.Models;

namespace Penumbra.Mods
{
    public class ModManager : IDisposable
    {
        private readonly Plugin _plugin;
        public readonly Dictionary< string, FileInfo > ResolvedFiles = new();
        public readonly Dictionary< string, string > SwappedFiles = new();

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
            //_plugin.GameUtils.ReloadPlayerResources();
        }
        private (InstallerInfo, Option, string) GlobalPosition( string rel, Dictionary<string, InstallerInfo> gps )
        {
            string filePath = null;
            foreach( var g in gps )
            {
                foreach( var opt in g.Value.Options )
                {
                    if( opt.OptionFiles.TryGetValue( rel, out filePath ) )
                    {
                        return (g.Value, opt, filePath);
                    }
                }
            }
            return (default( InstallerInfo ), default( Option ), null);
        }
        public void CalculateEffectiveFileList()
        {
            ResolvedFiles.Clear();
            SwappedFiles.Clear();

            var registeredFiles = new Dictionary< string, string >();

            foreach( var (mod, settings) in Mods.GetOrderedAndEnabledModListWithSettings( _plugin.Configuration.InvertModListOrder ) )
            {
                mod.FileConflicts?.Clear();

                // fixup path
                var baseDir = mod.ModBasePath.FullName;

                foreach( var file in mod.ModFiles )
                {
                    var relativeFilePath = file.FullName.Substring( baseDir.Length ).TrimStart( '\\' );

                    string gamePath;
                    bool addFile = true;
                    var gps = mod.Meta.Groups;
                    if( gps.Count >= 1 )
                    {
                        var negivtron = GlobalPosition( relativeFilePath, gps );
                        if( negivtron.Item3 != null )
                        {
                            if( settings.Conf == null )
                            {
                                settings.Conf = new();
                                _plugin.ModManager.Mods.Save();
                            }
                            if( !settings.Conf.ContainsKey( negivtron.Item1.GroupName ) )
                            {
                                settings.Conf[negivtron.Item1.GroupName] = 0;
                                _plugin.ModManager.Mods.Save();
                            }
                            var current = settings.Conf[negivtron.Item1.GroupName];
                            var flag = negivtron.Item1.Options.IndexOf( negivtron.Item2 );
                            switch( negivtron.Item1.SelectionType )
                            {
                                case SelectType.Single:
                                    {
                                        addFile = current == flag;
                                        break;
                                    }
                                case SelectType.Multi:
                                    {
                                        flag = 1 << negivtron.Item1.Options.IndexOf( negivtron.Item2 );
                                        addFile = ( flag & current ) != 0;
                                        break;
                                    }
                            }
                            gamePath = negivtron.Item3;
                        }
                        else
                        {
                            gamePath = relativeFilePath.Replace( '\\', '/' );
                        }
                    }
                    else
                        gamePath = relativeFilePath.Replace( '\\', '/' );
                    if( addFile )
                    {
                        if( !ResolvedFiles.ContainsKey( gamePath ) )                            {
                            ResolvedFiles[ gamePath.ToLowerInvariant() ] = file;
                            registeredFiles[ gamePath ] = mod.Meta.Name;
                        }
                        else if( registeredFiles.TryGetValue( gamePath, out var modName ) )
                        {
                            mod.AddConflict( modName, gamePath );
                        }
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
            _plugin.GameUtils.ReloadPlayerResources();
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