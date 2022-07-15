using System;
using System.Numerics;
using Dalamud.Logging;
using Newtonsoft.Json;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Util;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Files;

public readonly struct ImcEntry : IEquatable< ImcEntry >
{
    public byte MaterialId { get; init; }
    public byte DecalId { get; init; }
    private readonly ushort _attributeAndSound;
    public byte VfxId { get; init; }
    public byte MaterialAnimationId { get; init; }

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

    public readonly Utf8GamePath Path;
    public readonly int          NumParts;
    public          bool         ChangesSinceLoad = true;

    public ReadOnlySpan< ImcEntry > Span
        => new(( ImcEntry* )( Data + PreambleSize ), ( Length - PreambleSize ) / sizeof( ImcEntry ));

    private static int CountInternal( byte* data )
        => *( ushort* )data;

    private static ushort PartMask( byte* data )
        => *( ushort* )( data + 2 );

    private static ImcEntry* VariantPtr( byte* data, int partIdx, int variantIdx )
    {
        var flag = 1 << partIdx;
        if( ( PartMask( data ) & flag ) == 0 || variantIdx > CountInternal( data ) )
        {
            return null;
        }

        var numParts = BitOperations.PopCount( PartMask( data ) );
        var ptr      = ( ImcEntry* )( data + PreambleSize );
        ptr += variantIdx * numParts + partIdx;
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

        var oldCount = Count;
        *( ushort* )Data = ( ushort )numVariants;
        if( ActualLength > Length )
        {
            var newLength = ( ( ( ActualLength - 1 ) >> 7 ) + 1 ) << 7;
            PluginLog.Verbose( "Resized IMC {Path} from {Length} to {NewLength}.", Path, Length, newLength );
            ResizeResources( newLength );
        }

        var defaultPtr = ( ImcEntry* )( Data + PreambleSize );
        for( var i = oldCount + 1; i < numVariants + 1; ++i )
        {
            Functions.MemCpyUnchecked( defaultPtr + i * NumParts, defaultPtr, NumParts * sizeof( ImcEntry ) );
        }

        PluginLog.Verbose( "Expanded IMC {Path} from {Count} to {NewCount} variants.", Path, oldCount, numVariants );
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
            throw new Exception(
                "Could not obtain default Imc File.\n"
              + "Either the default file does not exist (possibly for offhand files from TexTools) or the installation is corrupted." );
        }

        fixed( byte* ptr = file.Data )
        {
            NumParts = BitOperations.PopCount( *( ushort* )( ptr + 2 ) );
            AllocateData( file.Data.Length );
            Functions.MemCpyUnchecked( Data, ptr, file.Data.Length );
        }
    }

    public static ImcEntry GetDefault( Utf8GamePath path, EquipSlot slot, int variantIdx, out bool exists )
    {
        var file = Dalamud.GameData.GetFile( path.ToString() );
        exists = false;
        if( file == null )
        {
            throw new Exception();
        }

        fixed( byte* ptr = file.Data )
        {
            var entry = VariantPtr( ptr, PartIndex( slot ), variantIdx );
            if( entry != null )
            {
                exists = true;
                return *entry;
            }
            return new ImcEntry();
        }
    }

    public void Replace( ResourceHandle* resource )
    {
        var (data, length) = resource->GetData();
        var newData = Penumbra.MetaFileManager.AllocateDefaultMemory( ActualLength, 8 );
        if( newData == null )
        {
            PluginLog.Error("Could not replace loaded IMC data at 0x{Data:X}, allocation failed."  );
            return;
        }
        Functions.MemCpyUnchecked( newData, Data, ActualLength );

        Penumbra.MetaFileManager.Free( data, length );
        resource->SetData( ( IntPtr )newData, ActualLength );
    }
}