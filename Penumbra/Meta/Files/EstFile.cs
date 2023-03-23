using System;
using System.Runtime.InteropServices;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Functions;

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
    private const int    IncreaseSize  = 512;

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
            ResizeResources( Length + IncreaseSize );
        }

        var control = ( Info* )( Data      + 4 );
        var entries = ( ushort* )( control + Count );

        for( var i = Count - 1; i >= idx; --i )
        {
            entries[ i + 3 ] = entries[ i ];
        }

        entries[ idx + 2 ] = skeletonId;

        for( var i = idx - 1; i >= 0; --i )
        {
            entries[ i + 2 ] = entries[ i ];
        }

        for( var i = Count - 1; i >= idx; --i )
        {
            control[ i + 1 ] = control[ i ];
        }

        control[ idx ] = new Info( genderRace, setId );

        *( int* )Data = Count + 1;
    }

    private void RemoveEntry( int idx )
    {
        var control = ( Info* )( Data      + 4 );
        var entries = ( ushort* )( control + Count );

        for( var i = idx; i < Count; ++i )
        {
            control[ i ] = control[ i + 1 ];
        }

        for( var i = 0; i < idx; ++i )
        {
            entries[ i - 2 ] = entries[ i ];
        }

        for( var i = idx; i < Count - 1; ++i )
        {
            entries[ i - 2 ] = entries[ i + 1 ];
        }

        entries[ Count - 3 ] = 0;
        entries[ Count - 2 ] = 0;
        entries[ Count - 1 ] = 0;
        *( int* )Data        = Count - 1;
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
        MemoryUtility.MemCpyUnchecked( Data, data, length );
        MemoryUtility.MemSet( Data + length, 0, Length - length );
    }

    public EstFile( EstManipulation.EstType estType )
        : base( ( MetaIndex )estType )
    {
        var length = DefaultData.Length;
        AllocateData( length + IncreaseSize );
        Reset();
    }

    public ushort GetDefault( GenderRace genderRace, ushort setId )
        => GetDefault( Index, genderRace, setId );

    public static ushort GetDefault( CharacterUtility.InternalIndex index, GenderRace genderRace, ushort setId )
    {
        var data  = ( byte* )Penumbra.CharacterUtility.DefaultResource( index ).Address;
        var count = *( int* )data;
        var span  = new ReadOnlySpan< Info >( data + 4, count );
        var (idx, found) = FindEntry( span, genderRace, setId );
        if( !found )
        {
            return 0;
        }

        return *( ushort* )( data + 4 + count * EntryDescSize + idx * EntrySize );
    }

    public static ushort GetDefault( MetaIndex metaIndex, GenderRace genderRace, ushort setId )
        => GetDefault( CharacterUtility.ReverseIndices[ ( int )metaIndex ], genderRace, setId );

    public static ushort GetDefault( EstManipulation.EstType estType, GenderRace genderRace, ushort setId )
        => GetDefault( ( MetaIndex )estType, genderRace, setId );
}