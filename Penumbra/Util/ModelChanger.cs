using System;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Logging;
using Penumbra.Mod;

namespace Penumbra.Util;

public static class ModelChanger
{
    private const           string SkinMaterialString = "/mt_c0201b0001_d.mtrl";
    private static readonly byte[] SkinMaterial       = Encoding.UTF8.GetBytes( SkinMaterialString );

    public static int ChangeMtrl( FullPath file, byte from, byte to )
    {
        if( !file.Exists )
        {
            return 0;
        }

        try
        {
            var text     = File.ReadAllBytes( file.FullName );
            var replaced = 0;

            var length = text.Length - SkinMaterial.Length;
            SkinMaterial[ 15 ] = from;
            for( var i = 0; i < length; ++i )
            {
                if( SkinMaterial.Where( ( t, j ) => text[ i + j ] != t ).Any() )
                {
                    continue;
                }

                text[ i + 15 ] =  to;
                i              += SkinMaterial.Length;
                ++replaced;
            }

            if( replaced == 0 )
            {
                return 0;
            }

            File.WriteAllBytes( file.FullName, text );
            return replaced;
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not write .mdl data for file {file.FullName}, replacing {( char )from} with {( char )to}:\n{e}" );
            return -1;
        }
    }

    public static bool ChangeModMaterials( ModData mod, byte from, byte to )
    {
        return mod.Resources.ModFiles
           .Where( f => f.Extension.Equals( ".mdl", StringComparison.InvariantCultureIgnoreCase ) )
           .All( file => ChangeMtrl( file, from, to ) >= 0 );
    }

    public static bool ChangeMtrlBToD( ModData mod )
        => ChangeModMaterials( mod, ( byte )'b', ( byte )'d' );

    public static bool ChangeMtrlDToB( ModData mod )
        => ChangeModMaterials( mod, ( byte )'d', ( byte )'b' );

    public static bool ChangeMtrlEToA( ModData mod )
        => ChangeModMaterials( mod, ( byte )'e', ( byte )'a' );

    public static bool ChangeMtrlAToE( ModData mod )
        => ChangeModMaterials( mod, ( byte )'a', ( byte )'e' );
}