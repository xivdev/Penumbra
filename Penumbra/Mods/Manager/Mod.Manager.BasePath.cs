using System;
using System.IO;
using System.Linq;
using Dalamud.Logging;

namespace Penumbra.Mods;

public partial class Mod
{
    public partial class Manager
    {
        public delegate void ModPathChangeDelegate( ModPathChangeType type, Mod mod, DirectoryInfo? oldDirectory,
            DirectoryInfo? newDirectory );

        public event ModPathChangeDelegate ModPathChanged;

        // Rename/Move a mod directory.
        // Updates all collection settings and sort order settings.
        public void MoveModDirectory( Index idx, DirectoryInfo newDirectory )
        {
            var mod = this[ idx ];
            // TODO
        }

        // Delete a mod by its index.
        // Deletes from filesystem as well as from internal data.
        // Updates indices of later mods.
        public void DeleteMod( int idx )
        {
            var mod = this[ idx ];
            if( Directory.Exists( mod.BasePath.FullName ) )
            {
                try
                {
                    Directory.Delete( mod.BasePath.FullName, true );
                }
                catch( Exception e )
                {
                    PluginLog.Error( $"Could not delete the mod {mod.BasePath.Name}:\n{e}" );
                }
            }

            _mods.RemoveAt( idx );
            foreach( var remainingMod in _mods.Skip( idx ) )
            {
                --remainingMod.Index;
            }

            ModPathChanged.Invoke( ModPathChangeType.Deleted, mod, mod.BasePath, null );
        }

        // Load a new mod and add it to the manager if successful.
        public void AddMod( DirectoryInfo modFolder )
        {
            if( _mods.Any( m => m.BasePath.Name == modFolder.Name ) )
            {
                return;
            }

            var mod = LoadMod( modFolder );
            if( mod == null )
            {
                return;
            }

            mod.Index = _mods.Count;
            _mods.Add( mod );
            ModPathChanged.Invoke( ModPathChangeType.Added, mod, null, mod.BasePath );
        }

        // Add new mods to NewMods and remove deleted mods from NewMods.
        private void OnModPathChange( ModPathChangeType type, Mod mod, DirectoryInfo? oldDirectory,
            DirectoryInfo? newDirectory )
        {
            switch( type )
            {
                case ModPathChangeType.Added:
                    NewMods.Add( mod );
                    break;
                case ModPathChangeType.Deleted:
                    NewMods.Remove( mod );
                    break;
            }
        }
    }
}