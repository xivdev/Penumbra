using System;
using System.Collections;
using System.Numerics;
using Dalamud.Logging;
using Dalamud.Memory;
using Newtonsoft.Json;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Util;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta.Files;

public readonly struct ImcEntry : IEquatable< ImcEntry >
{
    public readonly  byte   MaterialId;
    public readonly  byte   DecalId;
    private readonly ushort _attributeAndSound;
    public readonly  byte   VfxId;
    public readonly  byte   MaterialAnimationId;

    public ushort AttributeMask
    {
        get => ( ushort )( _attributeAndSound & 0x3FF );
        init => _attributeAndSound = ( ushort )( ( _attributeAndSound & ~0x3FF ) | ( value & 0x3FF ) );
    }

    public byte SoundId
    {
        get => ( byte )( _attributeAndSound >> 10 );
        init => _attributeAndSound = ( ushort )( AttributeMask | ( value << 10 ) );
    }

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

    [JsonConstructor]
    public ImcEntry( byte materialId, byte decalId, ushort attributeMask, byte soundId, byte vfxId, byte materialAnimationId )
    {
        MaterialId          = materialId;
        DecalId             = decalId;
        _attributeAndSound  = 0;
        VfxId               = vfxId;
        MaterialAnimationId = materialAnimationId;
        AttributeMask       = attributeMask;
        SoundId             = soundId;
    }
}

public unsafe class ImcFile : MetaBaseFile
{
    private const int PreambleSize = 4;

    public int ActualLength
        => NumParts * sizeof( ImcEntry ) * ( Count + 1 ) + PreambleSize;

    public int Count
        => CountInternal( Data );

    public readonly int          NumParts;
    public readonly Utf8GamePath Path;

    private static int CountInternal( byte* data )
        => *( ushort* )data;

    private static ushort PartMask( byte* data )
        => *( ushort* )( data + 2 );

    private static ImcEntry* DefaultPartPtr( byte* data, int partIdx )
    {
        var flag = 1 << partIdx;
        if( ( PartMask( data ) & flag ) == 0 )
        {
            return null;
        }

        return ( ImcEntry* )( data + PreambleSize ) + partIdx;
    }

    private static ImcEntry* VariantPtr( byte* data, int partIdx, int variantIdx )
    {
        if( variantIdx == 0 )
        {
            return DefaultPartPtr( data, partIdx );
        }

        --variantIdx;
        var flag = 1 << partIdx;

        if( ( PartMask( data ) & flag ) == 0 || variantIdx >= CountInternal( data ) )
        {
            return null;
        }

        var numParts = BitOperations.PopCount( PartMask( data ) );
        var ptr      = ( ImcEntry* )( data + PreambleSize );
        ptr += numParts;
        ptr += variantIdx * numParts;
        ptr += partIdx;
        return ptr;
    }

    public ImcEntry GetEntry( int partIdx, int variantIdx )
    {
        var ptr = VariantPtr( Data, partIdx, variantIdx );
        return ptr == null ? new ImcEntry() : *ptr;
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

        if( ActualLength > Length )
        {
            PluginLog.Warning( "Adding too many variants to IMC, size exceeded." );
            return false;
        }

        var defaultPtr = ( ImcEntry* )( Data + PreambleSize );
        var endPtr     = defaultPtr + ( numVariants + 1 ) * NumParts;
        for( var ptr = defaultPtr + NumParts; ptr < endPtr; ptr += NumParts )
        {
            Functions.MemCpyUnchecked( ptr, defaultPtr, NumParts * sizeof( ImcEntry ) );
        }

        PluginLog.Verbose( "Expanded imc from {Count} to {NewCount} variants.", Count, numVariants );
        *( ushort* )Data = ( ushort )numVariants;
        return true;
    }

    public bool SetEntry( int partIdx, int variantIdx, ImcEntry entry )
    {
        if( partIdx >= NumParts )
        {
            return false;
        }

        EnsureVariantCount( variantIdx );

        var variantPtr = VariantPtr( Data, partIdx, variantIdx );
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


    public override void Reset()
    {
        var file = Dalamud.GameData.GetFile( Path.ToString() );
        fixed( byte* ptr = file!.Data )
        {
            Functions.MemCpyUnchecked( Data, ptr, file.Data.Length );
            Functions.MemSet( Data + file.Data.Length, 0, Length - file.Data.Length );
        }
    }

    public ImcFile( Utf8GamePath path )
        : base( 0 )
    {
        Path = path;
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
            Functions.MemSet( Data + file.Data.Length, 0, sizeof( ImcEntry ) * 100 * NumParts );
        }
    }

    public static ImcEntry GetDefault( Utf8GamePath path, EquipSlot slot, int variantIdx )
    {
        var file = Dalamud.GameData.GetFile( path.ToString() );
        if( file == null )
        {
            throw new Exception();
        }

        fixed( byte* ptr = file.Data )
        {
            var entry = VariantPtr( ptr, PartIndex( slot ), variantIdx );
            return entry == null ? new ImcEntry() : *entry;
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