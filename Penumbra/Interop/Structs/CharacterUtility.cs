using System;
using System.Linq;
using System.Runtime.InteropServices;
using Penumbra.GameData.Enums;

namespace Penumbra.Interop.Structs;

[StructLayout( LayoutKind.Explicit )]
public unsafe struct CharacterUtility
{
    // TODO: female Hrothgar
    public static readonly int[] EqdpIndices
        = Enumerable.Range( EqdpStartIdx, NumEqdpFiles ).Where( i => i != EqdpStartIdx + 15 && i != EqdpStartIdx + 15 + NumEqdpFiles / 2 ).ToArray();

    public const int NumResources = 85;
    public const int EqpIdx       = 0;
    public const int GmpIdx       = 1;
    public const int HumanCmpIdx  = 63;
    public const int FaceEstIdx   = 64;
    public const int HairEstIdx   = 65;
    public const int HeadEstIdx   = 66;
    public const int BodyEstIdx   = 67;
    public const int EqdpStartIdx = 2;
    public const int NumEqdpFiles = 2 * 28;

    public static int EqdpIdx( GenderRace raceCode, bool accessory )
        => ( accessory ? NumEqdpFiles / 2 : 0 )
          + ( int )raceCode switch
            {
                0101 => EqdpStartIdx,
                0201 => EqdpStartIdx + 1,
                0301 => EqdpStartIdx + 2,
                0401 => EqdpStartIdx + 3,
                0501 => EqdpStartIdx + 4,
                0601 => EqdpStartIdx + 5,
                0701 => EqdpStartIdx + 6,
                0801 => EqdpStartIdx + 7,
                0901 => EqdpStartIdx + 8,
                1001 => EqdpStartIdx + 9,
                1101 => EqdpStartIdx + 10,
                1201 => EqdpStartIdx + 11,
                1301 => EqdpStartIdx + 12,
                1401 => EqdpStartIdx + 13,
                1501 => EqdpStartIdx + 14,
                1601 => EqdpStartIdx + 15, // TODO: female Hrothgar
                1701 => EqdpStartIdx + 16,
                1801 => EqdpStartIdx + 17,
                0104 => EqdpStartIdx + 18,
                0204 => EqdpStartIdx + 19,
                0504 => EqdpStartIdx + 20,
                0604 => EqdpStartIdx + 21,
                0704 => EqdpStartIdx + 22,
                0804 => EqdpStartIdx + 23,
                1304 => EqdpStartIdx + 24,
                1404 => EqdpStartIdx + 25,
                9104 => EqdpStartIdx + 26,
                9204 => EqdpStartIdx + 27,
                _    => -1,
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

    public ResourceHandle* EqdpResource( GenderRace raceCode, bool accessory )
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