using System;
using System.IO;
using Dalamud.Logging;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    public sealed partial class Manager
    {
        public DirectoryInfo BasePath { get; private set; } = null!;
        public bool Valid { get; private set; }

        public event Action? ModDiscoveryStarted;
        public event Action? ModDiscoveryFinished;

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
                        PluginLog.Error( $"Could not create specified mod directory {newDir.FullName}:\n{e}" );
                    }
                }

                BasePath = newDir;
                Valid    = Directory.Exists( newDir.FullName );
                if( Penumbra.Config.ModDirectory != BasePath.FullName )
                {
                    PluginLog.Information( "Set new mod base directory from {OldDirectory:l} to {NewDirectory:l}.", Penumbra.Config.ModDirectory, BasePath.FullName );
                    Penumbra.Config.ModDirectory = BasePath.FullName;
                    Penumbra.Config.Save();
                }
            }
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
                    var mod = LoadMod( modFolder );
                    if( mod == null )
                    {
                        continue;
                    }

                    mod.Index = _mods.Count;
                    _mods.Add( mod );
                }
            }

            ModDiscoveryFinished?.Invoke();
            PluginLog.Information( "Rediscovered mods." );
        }
    }
}