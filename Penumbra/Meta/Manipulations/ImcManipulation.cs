using System;
using System.Runtime.InteropServices;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Files;

namespace Penumbra.Meta.Manipulations;

[StructLayout( LayoutKind.Sequential )]
public readonly struct ImcManipulation : IEquatable< ImcManipulation >
{
    public readonly ImcEntry   Entry;
    public readonly ushort     PrimaryId;
    public readonly ushort     Variant;
    public readonly ushort     SecondaryId;
    public readonly ObjectType ObjectType;
    public readonly EquipSlot  EquipSlot;
    public readonly BodySlot   BodySlot;

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