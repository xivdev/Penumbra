using System;
using System.Linq;
using System.Runtime.InteropServices;
using Penumbra.GameData.Enums;

namespace Penumbra.Interop.Structs;

[StructLayout( LayoutKind.Explicit )]
public unsafe struct CharacterUtilityData
{
    public const int IndexTransparentTex = 72;
    public const int IndexDecalTex       = 73;

    public static readonly MetaIndex[] EqdpIndices = Enum.GetNames< MetaIndex >()
       .Zip( Enum.GetValues< MetaIndex >() )
       .Where( n => n.First.StartsWith( "Eqdp" ) )
       .Select( n => n.Second ).ToArray();

    public const int TotalNumResources = 87;

    /// <summary> Obtain the index for the eqdp file corresponding to the given race code and accessory. </summary>
    public static MetaIndex EqdpIdx( GenderRace raceCode, bool accessory )
        => +( int )raceCode switch
        {
            0101 => accessory ? MetaIndex.Eqdp0101Acc : MetaIndex.Eqdp0101,
            0201 => accessory ? MetaIndex.Eqdp0201Acc : MetaIndex.Eqdp0201,
            0301 => accessory ? MetaIndex.Eqdp0301Acc : MetaIndex.Eqdp0301,
            0401 => accessory ? MetaIndex.Eqdp0401Acc : MetaIndex.Eqdp0401,
            0501 => accessory ? MetaIndex.Eqdp0501Acc : MetaIndex.Eqdp0501,
            0601 => accessory ? MetaIndex.Eqdp0601Acc : MetaIndex.Eqdp0601,
            0701 => accessory ? MetaIndex.Eqdp0701Acc : MetaIndex.Eqdp0701,
            0801 => accessory ? MetaIndex.Eqdp0801Acc : MetaIndex.Eqdp0801,
            0901 => accessory ? MetaIndex.Eqdp0901Acc : MetaIndex.Eqdp0901,
            1001 => accessory ? MetaIndex.Eqdp1001Acc : MetaIndex.Eqdp1001,
            1101 => accessory ? MetaIndex.Eqdp1101Acc : MetaIndex.Eqdp1101,
            1201 => accessory ? MetaIndex.Eqdp1201Acc : MetaIndex.Eqdp1201,
            1301 => accessory ? MetaIndex.Eqdp1301Acc : MetaIndex.Eqdp1301,
            1401 => accessory ? MetaIndex.Eqdp1401Acc : MetaIndex.Eqdp1401,
            1501 => accessory ? MetaIndex.Eqdp1501Acc : MetaIndex.Eqdp1501,
            //1601 => accessory ? MetaIndex.Eqdp1601Acc : MetaIndex.Eqdp1601, Female Hrothgar
            1701 => accessory ? MetaIndex.Eqdp1701Acc : MetaIndex.Eqdp1701,
            1801 => accessory ? MetaIndex.Eqdp1801Acc : MetaIndex.Eqdp1801,
            0104 => accessory ? MetaIndex.Eqdp0104Acc : MetaIndex.Eqdp0104,
            0204 => accessory ? MetaIndex.Eqdp0204Acc : MetaIndex.Eqdp0204,
            0504 => accessory ? MetaIndex.Eqdp0504Acc : MetaIndex.Eqdp0504,
            0604 => accessory ? MetaIndex.Eqdp0604Acc : MetaIndex.Eqdp0604,
            0704 => accessory ? MetaIndex.Eqdp0704Acc : MetaIndex.Eqdp0704,
            0804 => accessory ? MetaIndex.Eqdp0804Acc : MetaIndex.Eqdp0804,
            1304 => accessory ? MetaIndex.Eqdp1304Acc : MetaIndex.Eqdp1304,
            1404 => accessory ? MetaIndex.Eqdp1404Acc : MetaIndex.Eqdp1404,
            9104 => accessory ? MetaIndex.Eqdp9104Acc : MetaIndex.Eqdp9104,
            9204 => accessory ? MetaIndex.Eqdp9204Acc : MetaIndex.Eqdp9204,
            _    => ( MetaIndex )( -1 ),
        };

    [FieldOffset( 0 )]
    public void* VTable;

    [FieldOffset( 8 )]
    public fixed ulong Resources[TotalNumResources];

    [FieldOffset( 8 + ( int )MetaIndex.Eqp * 8 )]
    public ResourceHandle* EqpResource;

    [FieldOffset( 8 + ( int )MetaIndex.Gmp * 8 )]
    public ResourceHandle* GmpResource;

    public ResourceHandle* Resource( int idx )
        => ( ResourceHandle* )Resources[ idx ];

    public ResourceHandle* Resource( MetaIndex idx )
        => Resource( ( int )idx );

    public ResourceHandle* EqdpResource( GenderRace raceCode, bool accessory )
        => Resource( ( int )EqdpIdx( raceCode, accessory ) );

    [FieldOffset( 8 + ( int )MetaIndex.HumanCmp * 8 )]
    public ResourceHandle* HumanCmpResource;

    [FieldOffset( 8 + ( int )MetaIndex.FaceEst * 8 )]
    public ResourceHandle* FaceEstResource;

    [FieldOffset( 8 + ( int )MetaIndex.HairEst * 8 )]
    public ResourceHandle* HairEstResource;

    [FieldOffset( 8 + ( int )MetaIndex.BodyEst * 8 )]
    public ResourceHandle* BodyEstResource;

    [FieldOffset( 8 + ( int )MetaIndex.HeadEst * 8 )]
    public ResourceHandle* HeadEstResource;

    [FieldOffset( 8 + IndexTransparentTex * 8 )]
    public TextureResourceHandle* TransparentTexResource;

    [FieldOffset( 8 + IndexDecalTex * 8 )]
    public TextureResourceHandle* DecalTexResource;

    // not included resources have no known use case.
}