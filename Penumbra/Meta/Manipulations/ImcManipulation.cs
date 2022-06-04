using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

[StructLayout( LayoutKind.Sequential, Pack = 1 )]
public readonly struct ImcManipulation : IMetaManipulation< ImcManipulation >
{
    public ImcEntry Entry { get; init; }
    public ushort PrimaryId { get; init; }
    public ushort Variant { get; init; }
    public ushort SecondaryId { get; init; }

    [JsonConverter( typeof( StringEnumConverter ) )]
    public ObjectType ObjectType { get; init; }

    [JsonConverter( typeof( StringEnumConverter ) )]
    public EquipSlot EquipSlot { get; init; }

    [JsonConverter( typeof( StringEnumConverter ) )]
    public BodySlot BodySlot { get; init; }

    public ImcManipulation( EquipSlot equipSlot, ushort variant, ushort primaryId, ImcEntry entry )
    {
        Entry       = entry;
        PrimaryId   = primaryId;
        Variant     = variant;
        SecondaryId = 0;
        ObjectType  = equipSlot.IsAccessory() ? ObjectType.Accessory : ObjectType.Equipment;
        EquipSlot   = equipSlot;
        BodySlot    = BodySlot.Unknown;
    }

    public ImcManipulation( ObjectType objectType, BodySlot bodySlot, ushort primaryId, ushort secondaryId, ushort variant,
        ImcEntry entry )
    {
        Entry       = entry;
        ObjectType  = objectType;
        BodySlot    = bodySlot;
        SecondaryId = secondaryId;
        PrimaryId   = primaryId;
        Variant     = variant;
        EquipSlot   = EquipSlot.Unknown;
    }

    [JsonConstructor]
    internal ImcManipulation( ObjectType objectType, BodySlot bodySlot, ushort primaryId, ushort secondaryId, ushort variant,
        EquipSlot equipSlot, ImcEntry entry )
    {
        Entry      = entry;
        ObjectType = objectType;
        PrimaryId  = primaryId;
        Variant    = variant;
        if( objectType is ObjectType.Accessory or ObjectType.Equipment )
        {
            BodySlot    = BodySlot.Unknown;
            SecondaryId = 0;
            EquipSlot   = equipSlot;
        }
        else
        {
            BodySlot    = bodySlot;
            SecondaryId = secondaryId;
            EquipSlot   = EquipSlot.Unknown;
        }
    }

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

        var s = SecondaryId.CompareTo( other.SecondaryId );
        if( s != 0 )
        {
            return s;
        }

        var b = BodySlot.CompareTo( other.BodySlot );
        return b != 0 ? b : Variant.CompareTo( other.Variant );
    }

    public int FileIndex()
        => -1;

    public Utf8GamePath GamePath()
    {
        return ObjectType switch
        {
            ObjectType.Accessory => Utf8GamePath.FromString( $"chara/accessory/a{PrimaryId:D4}/a{PrimaryId:D4}.imc", out var p )
                ? p
                : Utf8GamePath.Empty,
            ObjectType.Equipment => Utf8GamePath.FromString( $"chara/equipment/e{PrimaryId:D4}/e{PrimaryId:D4}.imc", out var p )
                ? p
                : Utf8GamePath.Empty,
            ObjectType.DemiHuman => Utf8GamePath.FromString(
                $"chara/demihuman/d{PrimaryId:D4}/obj/equipment/e{SecondaryId:D4}/e{SecondaryId:D4}.imc", out var p )
                ? p
                : Utf8GamePath.Empty,
            ObjectType.Monster => Utf8GamePath.FromString( $"chara/monster/m{PrimaryId:D4}/obj/body/b{SecondaryId:D4}/b{SecondaryId:D4}.imc",
                out var p )
                ? p
                : Utf8GamePath.Empty,
            ObjectType.Weapon => Utf8GamePath.FromString( $"chara/weapon/w{PrimaryId:D4}/obj/body/b{SecondaryId:D4}/b{SecondaryId:D4}.imc",
                out var p )
                ? p
                : Utf8GamePath.Empty,
            _ => throw new NotImplementedException(),
        };
    }

    public bool Apply( ImcFile file )
        => file.SetEntry( ImcFile.PartIndex( EquipSlot ), Variant, Entry );
}