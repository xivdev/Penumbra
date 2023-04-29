using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.String.Classes;

namespace Penumbra.Meta.Manipulations;

[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public readonly struct ImcManipulation : IMetaManipulation< ImcManipulation >
{
    public ImcEntry Entry { get; private init; }
    public ushort PrimaryId { get; private init; }
    public ushort SecondaryId { get; private init; }
    public byte Variant { get; private init; }

    [JsonConverter( typeof( StringEnumConverter ) )]
    public ObjectType ObjectType { get; private init; }

    [JsonConverter( typeof( StringEnumConverter ) )]
    public EquipSlot EquipSlot { get; private init; }

    [JsonConverter( typeof( StringEnumConverter ) )]
    public BodySlot BodySlot { get; private init; }

    public ImcManipulation( EquipSlot equipSlot, ushort variant, ushort primaryId, ImcEntry entry )
    {
        Entry       = entry;
        PrimaryId   = primaryId;
        Variant     = ( byte )Math.Clamp( variant, ( ushort )0, byte.MaxValue );
        SecondaryId = 0;
        ObjectType  = equipSlot.IsAccessory() ? ObjectType.Accessory : ObjectType.Equipment;
        EquipSlot   = equipSlot;
        BodySlot    = variant > byte.MaxValue ? BodySlot.Body : BodySlot.Unknown;
    }

    // Variants were initially ushorts but got shortened to bytes.
    // There are still some manipulations around that have values > 255 for variant,
    // so we change the unused value to something nonsensical in that case, just so they do not compare equal,
    // and clamp the variant to 255.
    [JsonConstructor]
    internal ImcManipulation( ObjectType objectType, BodySlot bodySlot, ushort primaryId, ushort secondaryId, ushort variant,
        EquipSlot equipSlot, ImcEntry entry )
    {
        Entry      = entry;
        ObjectType = objectType;
        PrimaryId  = primaryId;
        Variant    = ( byte )Math.Clamp( variant, ( ushort )0, byte.MaxValue );

        if( objectType is ObjectType.Accessory or ObjectType.Equipment )
        {
            BodySlot    = variant > byte.MaxValue ? BodySlot.Body : BodySlot.Unknown;
            SecondaryId = 0;
            EquipSlot   = equipSlot;
        }
        else if( objectType is ObjectType.DemiHuman )
        {
            BodySlot    = variant > byte.MaxValue ? BodySlot.Body : BodySlot.Unknown;
            SecondaryId = secondaryId;
            EquipSlot   = equipSlot == EquipSlot.Unknown ? EquipSlot.Head : equipSlot;
        }
        else
        {
            BodySlot    = bodySlot;
            SecondaryId = secondaryId;
            EquipSlot   = variant > byte.MaxValue ? EquipSlot.All : EquipSlot.Unknown;
        }
    }

    public bool Valid
        => ObjectType switch
        {
            ObjectType.Accessory => BodySlot  == BodySlot.Unknown,
            ObjectType.Equipment => BodySlot  == BodySlot.Unknown,
            ObjectType.DemiHuman => BodySlot  == BodySlot.Unknown,
            _                    => EquipSlot == EquipSlot.Unknown,
        };

    public ImcManipulation Copy( ImcEntry entry )
        => new(ObjectType, BodySlot, PrimaryId, SecondaryId, Variant, EquipSlot, entry);

    public override string ToString()
        => ObjectType is ObjectType.Equipment or ObjectType.Accessory
            ? $"Imc - {PrimaryId} - {EquipSlot} - {Variant}"
            : $"Imc - {PrimaryId} - {ObjectType} - {SecondaryId} - {BodySlot} - {Variant}";

    public bool Equals( ImcManipulation other )
        => PrimaryId    == other.PrimaryId
         && Variant     == other.Variant
         && SecondaryId == other.SecondaryId
         && ObjectType  == other.ObjectType
         && EquipSlot   == other.EquipSlot
         && BodySlot    == other.BodySlot;

    public override bool Equals( object? obj )
        => obj is ImcManipulation other && Equals( other );

    public override int GetHashCode()
        => HashCode.Combine( PrimaryId, Variant, SecondaryId, ( int )ObjectType, ( int )EquipSlot, ( int )BodySlot );

    public int CompareTo( ImcManipulation other )
    {
        var o = ObjectType.CompareTo( other.ObjectType );
        if( o != 0 )
        {
            return o;
        }

        var i = PrimaryId.CompareTo( other.PrimaryId );
        if( i != 0 )
        {
            return i;
        }

        if( ObjectType is ObjectType.Equipment or ObjectType.Accessory )
        {
            var e = EquipSlot.CompareTo( other.EquipSlot );
            return e != 0 ? e : Variant.CompareTo( other.Variant );
        }

        if( ObjectType is ObjectType.DemiHuman )
        {
            var e = EquipSlot.CompareTo( other.EquipSlot );
            if( e != 0 )
            {
                return e;
            }
        }

        var s = SecondaryId.CompareTo( other.SecondaryId );
        if( s != 0 )
        {
            return s;
        }

        var b = BodySlot.CompareTo( other.BodySlot );
        return b != 0 ? b : Variant.CompareTo( other.Variant );
    }

    public MetaIndex FileIndex()
        => ( MetaIndex )( -1 );

    public Utf8GamePath GamePath()
    {
        return ObjectType switch
        {
            ObjectType.Accessory => Utf8GamePath.FromString( GamePaths.Accessory.Imc.Path( PrimaryId ), out var p ) ? p : Utf8GamePath.Empty,
            ObjectType.Equipment => Utf8GamePath.FromString( GamePaths.Equipment.Imc.Path( PrimaryId ), out var p ) ? p : Utf8GamePath.Empty,
            ObjectType.DemiHuman => Utf8GamePath.FromString( GamePaths.DemiHuman.Imc.Path( PrimaryId, SecondaryId ), out var p ) ? p : Utf8GamePath.Empty,
            ObjectType.Monster   => Utf8GamePath.FromString( GamePaths.Monster.Imc.Path( PrimaryId, SecondaryId ), out var p ) ? p : Utf8GamePath.Empty,
            ObjectType.Weapon    => Utf8GamePath.FromString( GamePaths.Weapon.Imc.Path( PrimaryId, SecondaryId ), out var p ) ? p : Utf8GamePath.Empty,
            _                    => throw new NotImplementedException(),
        };
    }

    public bool Apply( ImcFile file )
        => file.SetEntry( ImcFile.PartIndex( EquipSlot ), Variant, Entry );
}