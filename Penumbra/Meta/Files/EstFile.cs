using System;
using System.Runtime.InteropServices;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Util;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta.Files;

// EST Structure:
// 1x [NumEntries : UInt32]
// Apparently entries need to be sorted.
// #NumEntries x [SetId : UInt16] [RaceId : UInt16]
// #NumEntries x [SkeletonId : UInt16]
public sealed unsafe class EstFile : MetaBaseFile
{
    private const ushort EntryDescSize = 4;
    private const ushort EntrySize     = 2;
    private const int    IncreaseSize  = 100;

    public int Count
        => *( int* )Data;

    private int Size
        => 4 + Count * ( EntryDescSize + EntrySize );

    public enum EstEntryChange
    {
        Unchanged,
        Changed,
        Added,
        Removed,
    }

    public ushort this[ GenderRace genderRace, ushort setId ]
    {
        get
        {
            var (idx, exists) = FindEntry( genderRace, setId );
            if( !exists )
            {
                return 0;
            }

            return *( ushort* )( Data + EntryDescSize * ( Count + 1 ) + EntrySize * idx );
        }
        set => SetEntry( genderRace, setId, value );
    }

    private void InsertEntry( int idx, GenderRace genderRace, ushort setId, ushort skeletonId )
    {
        if( Length < Size + EntryDescSize + EntrySize )
        {
            var data   = Data;
            var length = Length;
            AllocateData( length + IncreaseSize * ( EntryDescSize + EntrySize ) );
            Functions.MemCpyUnchecked( Data, data, length );
            Functions.MemSet( Data + length, 0, IncreaseSize * ( EntryDescSize + EntrySize ) );
            GC.RemoveMemoryPressure( length );
            Marshal.FreeHGlobal( ( IntPtr )data );
        }

        var control = ( uint* )( Data   + 4 );
        var entries = ( ushort* )( Data + 4 * ( Count + 1 ) );

        for( var i = Count; i > idx; --i )
        {
            *( entries + i + 2 ) = entries[ i - 1 ];
        }

        for( var i = idx - 1; i >= 0; --i )
        {
            *( entries + i + 2 ) = entries[ i ];
        }

        for( var i = Count; i > idx; --i )
        {
            *( control + i ) = control[ i - 1 ];
        }

        *( int* )Data = Count + 1;

        *( ushort* )control         = setId;
        *( ( ushort* )control + 1 ) = ( ushort )genderRace;
        control[ idx ]              = skeletonId;
    }

    private void RemoveEntry( int idx )
    {
        var entries = ( ushort* )( Data + 4 * Count );
        var control = ( uint* )( Data   + 4 );
        *( int* )Data = Count - 1;
        var count = Count;

        for( var i = idx; i < count; ++i )
        {
            control[ i ] = control[ i + 1 ];
        }

        for( var i = 0; i < count; ++i )
        {
            entries[ i ] = entries[ i + 1 ];
        }

        entries[ count ]     = 0;
        entries[ count + 1 ] = 0;
        entries[ count + 2 ] = 0;
    }

    [StructLayout( LayoutKind.Sequential, Size = 4 )]
    private struct Info : IComparable< Info >
    {
        public readonly ushort     SetId;
        public readonly GenderRace GenderRace;

        public Info( GenderRace gr, ushort setId )
        {
            GenderRace = gr;
            SetId      = setId;
        }

        public int CompareTo( Info other )
        {
            var genderRaceComparison = GenderRace.CompareTo( other.GenderRace );
            return genderRaceComparison != 0 ? genderRaceComparison : SetId.CompareTo( other.SetId );
        }
    }

    private static (int, bool) FindEntry( ReadOnlySpan< Info > data, GenderRace genderRace, ushort setId )
    {
        var idx = data.BinarySearch( new Info( genderRace, setId ) );
        return idx < 0 ? ( ~idx, false ) : ( idx, true );
    }

    private (int, bool) FindEntry( GenderRace genderRace, ushort setId )
    {
        var span = new ReadOnlySpan< Info >( Data + 4, Count );
        return FindEntry( span, genderRace, setId );
    }

    public EstEntryChange SetEntry( GenderRace genderRace, ushort setId, ushort skeletonId )
    {
        var (idx, exists) = FindEntry( genderRace, setId );
        if( exists )
        {
            var value = *( ushort* )( Data + 4 * ( Count + 1 ) + 2 * idx );
            if( value == skeletonId )
            {
                return EstEntryChange.Unchanged;
            }

            if( skeletonId == 0 )
            {
                RemoveEntry( idx );
                return EstEntryChange.Removed;
            }

            *( ushort* )( Data + 4 * ( Count + 1 ) + 2 * idx ) = skeletonId;
            return EstEntryChange.Changed;
        }

        if( skeletonId == 0 )
        {
            return EstEntryChange.Unchanged;
        }

        InsertEntry( idx, genderRace, setId, skeletonId );
        return EstEntryChange.Added;
    }

    public override void Reset()
    {
        var (d, length) = DefaultData;
        var data = ( byte* )d;
        Functions.MemCpyUnchecked( Data, data, length );
        Functions.MemSet( Data + length, 0, Length - length );
    }

    public EstFile( EstManipulation.EstType estType )
        : base( ( int )estType )
    {
        var length = DefaultData.Length;
        AllocateData( length + IncreaseSize * ( EntryDescSize + EntrySize ) );
        Reset();
    }

    public ushort GetDefault( GenderRace genderRace, ushort setId )
        => GetDefault( ( EstManipulation.EstType )Index, genderRace, setId );

    public static ushort GetDefault( EstManipulation.EstType estType, GenderRace genderRace, ushort setId )
    {
        var data  = ( byte* )Penumbra.CharacterUtility.DefaultResources[ ( int )estType ].Address;
        var count = *( int* )data;
        var span  = new ReadOnlySpan< Info >( data + 4, count );
        var (idx, found) = FindEntry( span, genderRace, setId );
        if( !found )
        {
            return 0;
        }

        return *( ushort* )( data + 4 + count * EntryDescSize + idx * EntrySize );
    }
}