using System;
using System.IO;
using Dalamud.Logging;

namespace Penumbra.Mods;

public partial class Mod2
{
    public partial class Manager
    {
        public delegate void ModPathChangeDelegate( ModPathChangeType type, Mod2 mod, DirectoryInfo? oldDirectory,
            DirectoryInfo? newDirectory );

        public event ModPathChangeDelegate? ModPathChanged;

        public void MoveModDirectory( Index idx, DirectoryInfo newDirectory )
        {
            var mod = this[ idx ];
            // TODO
        }

        public void DeleteMod( Index idx )
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

            // TODO
            // mod.Order.ParentFolder.RemoveMod( mod );
            // _mods.RemoveAt( idx );
            //for( var i = idx; i < _mods.Count; ++i )
            //{
            //    --_mods[i].Index;
            //}

            ModPathChanged?.Invoke( ModPathChangeType.Deleted, mod, mod.BasePath, null );
        }

        public Mod2 AddMod( DirectoryInfo modFolder )
        {
            // TODO

            //var mod = LoadMod( StructuredMods, modFolder );
            //if( mod == null )
            //{
            //    return -1;
            //}
            //
            //if( Config.ModSortOrder.TryGetValue( mod.BasePath.Name, out var sortOrder ) )
            //{
            //    if( SetSortOrderPath( mod, sortOrder ) )
            //    {
            //        Config.Save();
            //    }
            //}
            //
            //if( _mods.Any( m => m.BasePath.Name == modFolder.Name ) )
            //{
            //    return -1;
            //}
            //
            //_mods.Add( mod );
            //ModChange?.Invoke( ChangeType.Added, _mods.Count - 1, mod );
            //
            return this[^1];
        }
    }
}