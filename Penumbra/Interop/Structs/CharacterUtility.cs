using System;
using System.Runtime.InteropServices;

namespace Penumbra.Interop.Structs;

[StructLayout( LayoutKind.Explicit )]
public unsafe struct CharacterUtility
{
    public const int NumResources = 85;
    public const int EqpIdx       = 0;
    public const int GmpIdx       = 1;
    public const int HumanCmpIdx  = 63;
    public const int FaceEstIdx   = 64;
    public const int HairEstIdx   = 65;
    public const int BodyEstIdx   = 66;
    public const int HeadEstIdx   = 67;

    public static int EqdpIdx( ushort raceCode, bool accessory )
        => ( accessory ? 28 : 0 )
          + raceCode switch
            {
                0101 => 2,
                0201 => 3,
                0301 => 4,
                0401 => 5,
                0501 => 6,
                0601 => 7,
                0701 => 8,
                0801 => 9,
                0901 => 10,
                1001 => 11,
                1101 => 12,
                1201 => 13,
                1301 => 14,
                1401 => 15,
                1501 => 16,
                1601 => 17, // Does not exist yet
                1701 => 18,
                1801 => 19,
                0104 => 20,
                0204 => 21,
                0504 => 22,
                0604 => 23,
                0704 => 24,
                0804 => 25,
                1304 => 26,
                1404 => 27,
                9104 => 28,
                9204 => 29,
                _    => throw new ArgumentException(),
            };

    [FieldOffset( 0 )]
    public void* VTable;

    [FieldOffset( 8 )]
    public fixed ulong Resources[NumResources];

    [FieldOffset( 8 + EqpIdx * 8 )]
    public ResourceHandle* EqpResource;

    [FieldOffset( 8 + GmpIdx * 8 )]
    public ResourceHandle* GmpResource;

    public ResourceHandle* Resource( int idx )
        => ( ResourceHandle* )Resources[ idx ];

    public ResourceHandle* EqdpResource( ushort raceCode, bool accessory )
        => Resource( EqdpIdx( raceCode, accessory ) );

    [FieldOffset( 8 + HumanCmpIdx * 8 )]
    public ResourceHandle* HumanCmpResource;

    [FieldOffset( 8 + FaceEstIdx * 8 )]
    public ResourceHandle* FaceEstResource;

    [FieldOffset( 8 + HairEstIdx * 8 )]
    public ResourceHandle* HairEstResource;

    [FieldOffset( 8 + BodyEstIdx * 8 )]
    public ResourceHandle* BodyEstResource;

    [FieldOffset( 8 + HeadEstIdx * 8 )]
    public ResourceHandle* HeadEstResource;

    // not included resources have no known use case.
}