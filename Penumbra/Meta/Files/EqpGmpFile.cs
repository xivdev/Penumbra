using System;
using System.Collections.Generic;
using System.Numerics;
using Penumbra.GameData.Structs;
using Penumbra.GameData.Util;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Files;

// EQP/GMP Structure:
// 64 x [Block collapsed or not bit]
// 159 x [EquipmentParameter:ulong]
// (CountSetBits(Block Collapsed or not) - 1) x 160 x [EquipmentParameter:ulong]
// Item 0 does not exist and is sent to Item 1 instead.
public unsafe class ExpandedEqpGmpBase : MetaBaseFile
{
    protected const int BlockSize = 160;
    protected const int NumBlocks = 64;
    protected const int EntrySize = 8;
    protected const int MaxSize   = BlockSize * NumBlocks * EntrySize;

    public const int Count = BlockSize * NumBlocks;

    public ulong ControlBlock
        => *( ulong* )Data;

    protected T Get< T >( int idx ) where T : unmanaged
    {
        return idx switch
        {
            >= Count => throw new IndexOutOfRangeException(),
            <= 1     => *( ( T* )Data + 1 ),
            _        => *( ( T* )Data + idx ),
        };
    }

    protected void Set< T >( int idx, T value ) where T : unmanaged
    {
        idx = idx switch
        {
            >= Count => throw new IndexOutOfRangeException(),
            <= 0     => 1,
            _        => idx,
        };

        *( ( T* )Data + idx ) = value;
    }

    protected virtual void SetEmptyBlock( int idx )
    {
        Functions.MemSet( Data + idx * BlockSize * EntrySize, 0, BlockSize * EntrySize );
    }

    public sealed override void Reset()
    {
        var ptr          = ( byte* )DefaultData.Data;
        var controlBlock = *( ulong* )ptr;
        *( ulong* )ptr = ulong.MaxValue;
        for( var i = 0; i < 64; ++i )
        {
            var collapsed = ( ( controlBlock >> i ) & 1 ) == 0;
            if( !collapsed )
            {
                Functions.MemCpyUnchecked( Data + i * BlockSize * EntrySize, ptr + i * BlockSize * EntrySize, BlockSize * EntrySize );
            }
            else
            {
                SetEmptyBlock( i );
            }
        }
    }

    public ExpandedEqpGmpBase( bool gmp )
        : base( gmp ? CharacterUtility.GmpIdx : CharacterUtility.EqpIdx )
    {
        AllocateData( MaxSize );
        Reset();
    }

    protected static T GetDefault< T >( int fileIdx, int setIdx, T def ) where T : unmanaged
    {
        var data = ( byte* )Penumbra.CharacterUtility.DefaultResources[ fileIdx ].Address;
        if( setIdx == 0 )
        {
            setIdx = 1;
        }

        var blockIdx = setIdx / BlockSize;
        if( blockIdx >= NumBlocks )
        {
            return def;
        }

        var control  = *( ulong* )data;
        var blockBit = 1ul << blockIdx;
        if( ( control & blockBit ) == 0 )
        {
            return def;
        }

        var count = BitOperations.PopCount( control & ( blockBit - 1 ) );
        var idx   = setIdx % BlockSize;
        var ptr   = ( T* )data + BlockSize * count + idx;
        return *ptr;
    }
}

public sealed class ExpandedEqpFile : ExpandedEqpGmpBase
{
    public ExpandedEqpFile()
        : base( false )
    { }

    public EqpEntry this[ int idx ]
    {
        get => Get< EqpEntry >( idx );
        set => Set( idx, value );
    }

    public static EqpEntry GetDefault( int setIdx )
        => GetDefault( CharacterUtility.EqpIdx, setIdx, Eqp.DefaultEntry );

    protected override unsafe void SetEmptyBlock( int idx )
    {
        var blockPtr = ( ulong* )( Data + idx * BlockSize * EntrySize );
        var endPtr   = blockPtr + BlockSize;
        for( var ptr = blockPtr; ptr < endPtr; ++ptr )
        {
            *ptr = ( ulong )Eqp.DefaultEntry;
        }
    }

    public void Reset( IEnumerable< int > entries )
    {
        foreach( var entry in entries )
        {
            this[ entry ] = GetDefault( entry );
        }
    }
}

public sealed class ExpandedGmpFile : ExpandedEqpGmpBase
{
    public ExpandedGmpFile()
        : base( true )
    { }

    public GmpEntry this[ int idx ]
    {
        get => Get< GmpEntry >( idx );
        set => Set( idx, value );
    }

    public static GmpEntry GetDefault( int setIdx )
        => GetDefault( CharacterUtility.GmpIdx, setIdx, GmpEntry.Default );

    public void Reset( IEnumerable< int > entries )
    {
        foreach( var entry in entries )
        {
            this[ entry ] = GetDefault( entry );
        }
    }
}