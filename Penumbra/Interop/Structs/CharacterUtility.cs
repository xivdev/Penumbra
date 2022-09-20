using System;
using System.Linq;
using System.Runtime.InteropServices;
using Penumbra.GameData.Enums;

namespace Penumbra.Interop.Structs;

[StructLayout( LayoutKind.Explicit )]
public unsafe struct CharacterUtility
{
    public enum Index : int
    {
        Eqp = 0,
        Gmp = 2,

        Eqdp0101 = 3,
        Eqdp0201,
        Eqdp0301,
        Eqdp0401,
        Eqdp0501,
        Eqdp0601,
        Eqdp0701,
        Eqdp0801,
        Eqdp0901,
        Eqdp1001,
        Eqdp1101,
        Eqdp1201,
        Eqdp1301,
        Eqdp1401,
        Eqdp1501,

        //Eqdp1601, // TODO: female Hrothgar
        Eqdp1701 = Eqdp1501 + 2,
        Eqdp1801,
        Eqdp0104,
        Eqdp0204,
        Eqdp0504,
        Eqdp0604,
        Eqdp0704,
        Eqdp0804,
        Eqdp1304,
        Eqdp1404,
        Eqdp9104,
        Eqdp9204,

        Eqdp0101Acc,
        Eqdp0201Acc,
        Eqdp0301Acc,
        Eqdp0401Acc,
        Eqdp0501Acc,
        Eqdp0601Acc,
        Eqdp0701Acc,
        Eqdp0801Acc,
        Eqdp0901Acc,
        Eqdp1001Acc,
        Eqdp1101Acc,
        Eqdp1201Acc,
        Eqdp1301Acc,
        Eqdp1401Acc,
        Eqdp1501Acc,

        //Eqdp1601Acc, // TODO: female Hrothgar
        Eqdp1701Acc = Eqdp1501Acc + 2,
        Eqdp1801Acc,
        Eqdp0104Acc,
        Eqdp0204Acc,
        Eqdp0504Acc,
        Eqdp0604Acc,
        Eqdp0704Acc,
        Eqdp0804Acc,
        Eqdp1304Acc,
        Eqdp1404Acc,
        Eqdp9104Acc,
        Eqdp9204Acc,

        HumanCmp = 64,
        FaceEst,
        HairEst,
        HeadEst,
        BodyEst,
    }

    public const int IndexTransparentTex = 72;
    public const int IndexDecalTex       = 73;

    public static readonly Index[] EqdpIndices = Enum.GetNames< Index >()
       .Zip( Enum.GetValues< Index >() )
       .Where( n => n.First.StartsWith( "Eqdp" ) )
       .Select( n => n.Second ).ToArray();

    public const int TotalNumResources = 87;

    public static Index EqdpIdx( GenderRace raceCode, bool accessory )
        => +( int )raceCode switch
        {
            0101 => accessory ? Index.Eqdp0101Acc : Index.Eqdp0101,
            0201 => accessory ? Index.Eqdp0201Acc : Index.Eqdp0201,
            0301 => accessory ? Index.Eqdp0301Acc : Index.Eqdp0301,
            0401 => accessory ? Index.Eqdp0401Acc : Index.Eqdp0401,
            0501 => accessory ? Index.Eqdp0501Acc : Index.Eqdp0501,
            0601 => accessory ? Index.Eqdp0601Acc : Index.Eqdp0601,
            0701 => accessory ? Index.Eqdp0701Acc : Index.Eqdp0701,
            0801 => accessory ? Index.Eqdp0801Acc : Index.Eqdp0801,
            0901 => accessory ? Index.Eqdp0901Acc : Index.Eqdp0901,
            1001 => accessory ? Index.Eqdp1001Acc : Index.Eqdp1001,
            1101 => accessory ? Index.Eqdp1101Acc : Index.Eqdp1101,
            1201 => accessory ? Index.Eqdp1201Acc : Index.Eqdp1201,
            1301 => accessory ? Index.Eqdp1301Acc : Index.Eqdp1301,
            1401 => accessory ? Index.Eqdp1401Acc : Index.Eqdp1401,
            1501 => accessory ? Index.Eqdp1501Acc : Index.Eqdp1501,
            //1601 => accessory ? RelevantIndex.Eqdp1601Acc : RelevantIndex.Eqdp1601, Female Hrothgar
            1701 => accessory ? Index.Eqdp1701Acc : Index.Eqdp1701,
            1801 => accessory ? Index.Eqdp1801Acc : Index.Eqdp1801,
            0104 => accessory ? Index.Eqdp0104Acc : Index.Eqdp0104,
            0204 => accessory ? Index.Eqdp0204Acc : Index.Eqdp0204,
            0504 => accessory ? Index.Eqdp0504Acc : Index.Eqdp0504,
            0604 => accessory ? Index.Eqdp0604Acc : Index.Eqdp0604,
            0704 => accessory ? Index.Eqdp0704Acc : Index.Eqdp0704,
            0804 => accessory ? Index.Eqdp0804Acc : Index.Eqdp0804,
            1304 => accessory ? Index.Eqdp1304Acc : Index.Eqdp1304,
            1404 => accessory ? Index.Eqdp1404Acc : Index.Eqdp1404,
            9104 => accessory ? Index.Eqdp9104Acc : Index.Eqdp9104,
            9204 => accessory ? Index.Eqdp9204Acc : Index.Eqdp9204,
            _    => ( Index )( -1 ),
        };

    [FieldOffset( 0 )]
    public void* VTable;

    [FieldOffset( 8 )]
    public fixed ulong Resources[TotalNumResources];

    [FieldOffset( 8 + ( int )Index.Eqp * 8 )]
    public ResourceHandle* EqpResource;

    [FieldOffset( 8 + ( int )Index.Gmp * 8 )]
    public ResourceHandle* GmpResource;

    public ResourceHandle* Resource( int idx )
        => ( ResourceHandle* )Resources[ idx ];

    public ResourceHandle* Resource( Index idx )
        => Resource( ( int )idx );

    public ResourceHandle* EqdpResource( GenderRace raceCode, bool accessory )
        => Resource( ( int )EqdpIdx( raceCode, accessory ) );

    [FieldOffset( 8 + ( int )Index.HumanCmp * 8 )]
    public ResourceHandle* HumanCmpResource;

    [FieldOffset( 8 + ( int )Index.FaceEst * 8 )]
    public ResourceHandle* FaceEstResource;

    [FieldOffset( 8 + ( int )Index.HairEst * 8 )]
    public ResourceHandle* HairEstResource;

    [FieldOffset( 8 + ( int )Index.BodyEst * 8 )]
    public ResourceHandle* BodyEstResource;

    [FieldOffset( 8 + ( int )Index.HeadEst * 8 )]
    public ResourceHandle* HeadEstResource;

    [FieldOffset( 8 + IndexTransparentTex * 8 )]
    public TextureResourceHandle* TransparentTexResource;

    [FieldOffset( 8 + IndexDecalTex * 8 )]
    public TextureResourceHandle* DecalTexResource;

    // not included resources have no known use case.
}