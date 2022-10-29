using System;
using System.IO;
using Penumbra.String;
using Penumbra.String.Functions;

namespace Penumbra.GameData.Enums;

public enum ResourceType : uint
{
    Aet  = 0x00616574,
    Amb  = 0x00616D62,
    Atch = 0x61746368,
    Atex = 0x61746578,
    Avfx = 0x61766678,
    Awt  = 0x00617774,
    Cmp  = 0x00636D70,
    Dic  = 0x00646963,
    Eid  = 0x00656964,
    Envb = 0x656E7662,
    Eqdp = 0x65716470,
    Eqp  = 0x00657170,
    Essb = 0x65737362,
    Est  = 0x00657374,
    Exd  = 0x00657864,
    Exh  = 0x00657868,
    Exl  = 0x0065786C,
    Fdt  = 0x00666474,
    Gfd  = 0x00676664,
    Ggd  = 0x00676764,
    Gmp  = 0x00676D70,
    Gzd  = 0x00677A64,
    Imc  = 0x00696D63,
    Lcb  = 0x006C6362,
    Lgb  = 0x006C6762,
    Luab = 0x6C756162,
    Lvb  = 0x006C7662,
    Mdl  = 0x006D646C,
    Mlt  = 0x006D6C74,
    Mtrl = 0x6D74726C,
    Obsb = 0x6F627362,
    Pap  = 0x00706170,
    Pbd  = 0x00706264,
    Pcb  = 0x00706362,
    Phyb = 0x70687962,
    Plt  = 0x00706C74,
    Scd  = 0x00736364,
    Sgb  = 0x00736762,
    Shcd = 0x73686364,
    Shpk = 0x7368706B,
    Sklb = 0x736B6C62,
    Skp  = 0x00736B70,
    Stm  = 0x0073746D,
    Svb  = 0x00737662,
    Tera = 0x74657261,
    Tex  = 0x00746578,
    Tmb  = 0x00746D62,
    Ugd  = 0x00756764,
    Uld  = 0x00756C64,
    Waoe = 0x77616F65,
    Wtd  = 0x00777464,
}

public static class ResourceTypeExtensions
{
    public static ResourceType FromBytes( byte a1, byte a2, byte a3 )
        => ( ResourceType )( ( ( uint )ByteStringFunctions.AsciiToLower( a1 ) << 16 )
          | ( ( uint )ByteStringFunctions.AsciiToLower( a2 )                  << 8 )
          | ByteStringFunctions.AsciiToLower( a3 ) );

    public static ResourceType FromBytes( byte a1, byte a2, byte a3, byte a4 )
        => ( ResourceType )( ( ( uint )ByteStringFunctions.AsciiToLower( a1 ) << 24 )
          | ( ( uint )ByteStringFunctions.AsciiToLower( a2 )                  << 16 )
          | ( ( uint )ByteStringFunctions.AsciiToLower( a3 )                  << 8 )
          | ByteStringFunctions.AsciiToLower( a4 ) );

    public static ResourceType FromBytes( char a1, char a2, char a3 )
        => FromBytes( ( byte )a1, ( byte )a2, ( byte )a3 );

    public static ResourceType FromBytes( char a1, char a2, char a3, char a4 )
        => FromBytes( ( byte )a1, ( byte )a2, ( byte )a3, ( byte )a4 );

    public static ResourceType FromString( string path )
    {
        var ext = Path.GetExtension( path.AsSpan() );
        ext = ext.Length == 0 ? path.AsSpan() : ext[ 1.. ];

        return ext.Length switch
        {
            0 => 0,
            1 => ( ResourceType )ext[ ^1 ],
            2 => FromBytes( '\0', ext[ ^2 ], ext[ ^1 ] ),
            3 => FromBytes( ext[ ^3 ], ext[ ^2 ], ext[ ^1 ] ),
            _ => FromBytes( ext[ ^4 ], ext[ ^3 ], ext[ ^2 ], ext[ ^1 ] ),
        };
    }

    public static ResourceType FromString( ByteString path )
    {
        var extIdx = path.LastIndexOf( ( byte )'.' );
        var ext    = extIdx == -1 ? path : extIdx == path.Length - 1 ? ByteString.Empty : path.Substring( extIdx + 1 );

        return ext.Length switch
        {
            0 => 0,
            1 => ( ResourceType )ext[ ^1 ],
            2 => FromBytes( 0, ext[ ^2 ], ext[ ^1 ] ),
            3 => FromBytes( ext[ ^3 ], ext[ ^2 ], ext[ ^1 ] ),
            _ => FromBytes( ext[ ^4 ], ext[ ^3 ], ext[ ^2 ], ext[ ^1 ] ),
        };
    }
}