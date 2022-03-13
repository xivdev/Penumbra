using System;
using System.Numerics;
using Dalamud.Logging;
using Dalamud.Memory;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Util;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Files;

public readonly struct ImcEntry : IEquatable< ImcEntry >
{
    public readonly  byte   MaterialId;
    public readonly  byte   DecalId;
    private readonly ushort _attributeAndSound;
    public readonly  byte   VfxId;
    public readonly  byte   MaterialAnimationId;

    public ushort AttributeMask
        => ( ushort )( _attributeAndSound & 0x3FF );

    public byte SoundId
        => ( byte )( _attributeAndSound >> 10 );

    public bool Equals( ImcEntry other )
        => MaterialId           == other.MaterialId
         && DecalId             == other.DecalId
         && _attributeAndSound  == other._attributeAndSound
         && VfxId               == other.VfxId
         && MaterialAnimationId == other.MaterialAnimationId;

    public override bool Equals( object? obj )
        => obj is ImcEntry other && Equals( other );

    public override int GetHashCode()
        => HashCode.Combine( MaterialId, DecalId, _attributeAndSound, VfxId, MaterialAnimationId );
}

public unsafe class ImcFile : MetaBaseFile
{
    private const int PreambleSize = 4;

    public int ActualLength
        => NumParts * sizeof( ImcEntry ) * ( Count + 1 ) + PreambleSize;

    public int Count
        => *( ushort* )Data;

    public ushort PartMask
        => *( ushort* )( Data + 2 );

    public readonly int          NumParts;
    public readonly Utf8GamePath Path;

    public ImcEntry* DefaultPartPtr( int partIdx )
    {
        var flag = 1 << partIdx;
        if( ( PartMask & flag ) == 0 )
        {
            return null;
        }

        return ( ImcEntry* )( Data + PreambleSize ) + partIdx;
    }

    public ImcEntry* VariantPtr( int partIdx, int variantIdx )
    {
        var flag = 1 << partIdx;
        if( ( PartMask & flag ) == 0 || variantIdx >= Count )
        {
            return null;
        }

        var numParts = NumParts;
        var ptr      = ( ImcEntry* )( Data + PreambleSize );
        ptr += numParts;
        ptr += variantIdx * numParts;
        ptr += partIdx;
        return ptr;
    }

    public static int PartIndex( EquipSlot slot )
        => slot switch
        {
            EquipSlot.Head    => 0,
            EquipSlot.Ears    => 0,
            EquipSlot.Body    => 1,
            EquipSlot.Neck    => 1,
            EquipSlot.Hands   => 2,
            EquipSlot.Wrists  => 2,
            EquipSlot.Legs    => 3,
            EquipSlot.RFinger => 3,
            EquipSlot.Feet    => 4,
            EquipSlot.LFinger => 4,
            _                 => 0,
        };

    public bool EnsureVariantCount( int numVariants )
    {
        if( numVariants <= Count )
        {
            return true;
        }

        var numParts = NumParts;
        if( ActualLength > Length )
        {
            PluginLog.Warning( "Adding too many variants to IMC, size exceeded." );
            return false;
        }

        var defaultPtr = ( ImcEntry* )( Data + PreambleSize );
        var endPtr     = defaultPtr + ( numVariants + 1 ) * numParts;
        for( var ptr = defaultPtr + numParts; ptr < endPtr; ptr += numParts )
        {
            Functions.MemCpyUnchecked( ptr, defaultPtr, numParts * sizeof( ImcEntry ) );
        }

        PluginLog.Verbose( "Expanded imc from {Count} to {NewCount} variants.", Count, numVariants );
        *( ushort* )Count = ( ushort )numVariants;
        return true;
    }

    public bool SetEntry( int partIdx, int variantIdx, ImcEntry entry )
    {
        var numParts = NumParts;
        if( partIdx >= numParts )
        {
            return false;
        }

        EnsureVariantCount( variantIdx + 1 );

        var variantPtr = VariantPtr( partIdx, variantIdx );
        if( variantPtr == null )
        {
            PluginLog.Error( "Error during expansion of imc file." );
            return false;
        }

        if( variantPtr->Equals( entry ) )
        {
            return false;
        }

        *variantPtr = entry;
        return true;
    }


    public ImcFile( Utf8GamePath path )
        : base( 0 )
    {
        var file = Dalamud.GameData.GetFile( path.ToString() );
        if( file == null )
        {
            throw new Exception();
        }

        fixed( byte* ptr = file.Data )
        {
            NumParts = BitOperations.PopCount( *( ushort* )( ptr + 2 ) );
            AllocateData( file.Data.Length + sizeof( ImcEntry ) * 100 * NumParts );
            Functions.MemCpyUnchecked( Data, ptr, file.Data.Length );
        }
    }

    public void Replace( ResourceHandle* resource )
    {
        var (data, length) = resource->GetData();
        if( data == IntPtr.Zero )
        {
            return;
        }

        var requiredLength = ActualLength;
        if( length >= requiredLength )
        {
            Functions.MemCpyUnchecked( ( void* )data, Data, requiredLength );
            Functions.MemSet( ( byte* )data + requiredLength, 0, length - requiredLength );
            return;
        }

        MemoryHelper.GameFree( ref data, ( ulong )length );
        var file = ( byte* )MemoryHelper.GameAllocateDefault( ( ulong )requiredLength );
        Functions.MemCpyUnchecked( file, Data, requiredLength );
        resource->SetData( ( IntPtr )file, requiredLength );
    }
}