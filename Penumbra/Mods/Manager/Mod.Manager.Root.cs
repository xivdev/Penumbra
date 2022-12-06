using System;
using System.IO;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    public sealed partial class Manager
    {
        public DirectoryInfo BasePath { get; private set; } = null!;
        private DirectoryInfo? _exportDirectory;

        public DirectoryInfo ExportDirectory
            => _exportDirectory ?? BasePath;

        public bool Valid { get; private set; }

        public event Action? ModDiscoveryStarted;
        public event Action? ModDiscoveryFinished;
        public event Action< string, bool > ModDirectoryChanged;

        // Change the mod base directory and discover available mods.
        public void DiscoverMods( string newDir )
        {
            SetBaseDirectory( newDir, false );
            DiscoverMods();
        }

        // Set the mod base directory.
        // If its not the first time, check if it is the same directory as before.
        // Also checks if the directory is available and tries to create it if it is not.
        private void SetBaseDirectory( string newPath, bool firstTime )
        {
            if( !firstTime && string.Equals( newPath, Penumbra.Config.ModDirectory, StringComparison.OrdinalIgnoreCase ) )
            {
                return;
            }

            if( newPath.Length == 0 )
            {
                Valid    = false;
                BasePath = new DirectoryInfo( "." );
                if( Penumbra.Config.ModDirectory != BasePath.FullName )
                {
                    ModDirectoryChanged.Invoke( string.Empty, false );
                }
            }
            else
            {
                var newDir = new DirectoryInfo( newPath );
                if( !newDir.Exists )
                {
                    try
                    {
                        Directory.CreateDirectory( newDir.FullName );
                        newDir.Refresh();
                    }
                    catch( Exception e )
                    {
                        Penumbra.Log.Error( $"Could not create specified mod directory {newDir.FullName}:\n{e}" );
                    }
                }

                BasePath = newDir;
                Valid    = Directory.Exists( newDir.FullName );
                if( Penumbra.Config.ModDirectory != BasePath.FullName )
                {
                    ModDirectoryChanged.Invoke( BasePath.FullName, Valid );
                }
            }
        }

        private static void OnModDirectoryChange( string newPath, bool _ )
        {
            Penumbra.Log.Information( $"Set new mod base directory from {Penumbra.Config.ModDirectory} to {newPath}." );
            Penumbra.Config.ModDirectory = newPath;
            Penumbra.Config.Save();
        }

        // Discover new mods.
        public void DiscoverMods()
        {
            NewMods.Clear();
            ModDiscoveryStarted?.Invoke();
            _mods.Clear();
            BasePath.Refresh();

            if( Valid && BasePath.Exists )
            {
                foreach( var modFolder in BasePath.EnumerateDirectories() )
                {
                    var mod = LoadMod( modFolder, false );
                    if( mod == null )
                    {
                        continue;
                    }

                    mod.Index = _mods.Count;
                    _mods.Add( mod );
                }
            }

            ModDiscoveryFinished?.Invoke();
            Penumbra.Log.Information( "Rediscovered mods." );

            if( MigrateModBackups )
            {
                ModBackup.MigrateZipToPmp( this );
            }
        }

        public void UpdateExportDirectory( string newDirectory, bool change )
        {
            if( newDirectory.Length == 0 )
            {
                if( _exportDirectory == null )
                {
                    return;
                }

                _exportDirectory                = null;
                Penumbra.Config.ExportDirectory = string.Empty;
                Penumbra.Config.Save();
                return;
            }

            var dir = new DirectoryInfo( newDirectory );
            if( dir.FullName.Equals( _exportDirectory?.FullName, StringComparison.OrdinalIgnoreCase ) )
            {
                return;
            }

            if( !dir.Exists )
            {
                try
                {
                    Directory.CreateDirectory( dir.FullName );
                }
                catch( Exception e )
                {
                    Penumbra.Log.Error( $"Could not create Export Directory:\n{e}" );
                    return;
                }
            }

            if( change )
            {
                foreach( var mod in _mods )
                {
                    new ModBackup( mod ).Move( dir.FullName );
                }
            }

            _exportDirectory = dir;

            if( change )
            {
                Penumbra.Config.ExportDirectory = dir.FullName;
                Penumbra.Config.Save();
            }
        }
    }
}