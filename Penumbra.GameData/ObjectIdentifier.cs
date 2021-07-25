using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.GameData.Util;

namespace Penumbra.GameData
{
    public static class ObjectIdentifier
    {
        private static ObjectIdentification? _identification = null;

        public static bool Initialize( DalamudPluginInterface pi )
        {
            if( _identification != null )
            {
                return true;
            }

            try
            {
                _identification = new ObjectIdentification( pi );
                return true;
            }
            catch( Exception e )
            {
                _identification = null;
                PluginLog.Error( $"Failure while initializing Object Identifier:\n{e}" );
                return false;
            }
        }

        private static void Verify()
        {
            if( _identification == null )
            {
                throw new Exception( "Object Identifier not initialized." );
            }
        }

        public static void Identify( IDictionary< string, object? > set, GamePath path )
        {
            Verify();
            _identification!.Identify( set, path );
        }

        public static Dictionary< string, object? > Identify( GamePath path )
        {
            Dictionary< string, object? > ret = new();
            Identify( ret, path );
            return ret;
        }

        public static Item? Identify( SetId setId, WeaponType weaponType, ushort variant, EquipSlot slot )
        {
            Verify();
            return _identification!.Identify( setId, weaponType, variant, slot );
        }
    }
}
