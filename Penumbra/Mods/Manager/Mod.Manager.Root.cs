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

        public void DiscoverMods( string newDir )
        {
            SetBaseDirectory( newDir, false );
            DiscoverMods();
        }

        private void SetBaseDirectory( string newPath, bool firstTime )
        {
            if( !firstTime && string.Equals( newPath, Penumbra.Config.ModDirectory, StringComparison.InvariantCultureIgnoreCase ) )
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
                    Penumbra.Config.ModDirectory = BasePath.FullName;
                    Penumbra.Config.Save();
                }
            }
        }

        public void DiscoverMods()
        {
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
        }
    }
}