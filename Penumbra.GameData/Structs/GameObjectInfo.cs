using System;
using System.Runtime.InteropServices;
using Dalamud;
using Penumbra.GameData.Enums;

namespace Penumbra.GameData.Structs;

[StructLayout( LayoutKind.Explicit )]
public struct GameObjectInfo : IComparable
{
    public static GameObjectInfo Equipment( FileType type, ushort setId, GenderRace gr = GenderRace.Unknown
        , EquipSlot slot = EquipSlot.Unknown, byte variant = 0 )
        => new()
        {
            FileType   = type,
            ObjectType = slot.IsAccessory() ? ObjectType.Accessory : ObjectType.Equipment,
            PrimaryId  = setId,
            GenderRace = gr,
            Variant    = variant,
            EquipSlot  = slot,
        };

    public static GameObjectInfo Weapon( FileType type, ushort setId, ushort weaponId, byte variant = 0 )
        => new()
        {
            FileType    = type,
            ObjectType  = ObjectType.Weapon,
            PrimaryId   = setId,
            SecondaryId = weaponId,
            Variant     = variant,
        };

    public static GameObjectInfo Customization( FileType type, CustomizationType customizationType, ushort id = 0
        , GenderRace gr = GenderRace.Unknown, BodySlot bodySlot = BodySlot.Unknown, byte variant = 0 )
        => new()
        {
            FileType          = type,
            ObjectType        = ObjectType.Character,
            PrimaryId         = id,
            GenderRace        = gr,
            BodySlot          = bodySlot,
            Variant           = variant,
            CustomizationType = customizationType,
        };

    public static GameObjectInfo Monster( FileType type, ushort monsterId, ushort bodyId, byte variant = 0 )
        => new()
        {
            FileType    = type,
            ObjectType  = ObjectType.Monster,
            PrimaryId   = monsterId,
            SecondaryId = bodyId,
            Variant     = variant,
        };

    public static GameObjectInfo DemiHuman( FileType type, ushort demiHumanId, ushort bodyId, EquipSlot slot = EquipSlot.Unknown,
        byte variant = 0
    )
        => new()
        {
            FileType    = type,
            ObjectType  = ObjectType.DemiHuman,
            PrimaryId   = demiHumanId,
            SecondaryId = bodyId,
            Variant     = variant,
            EquipSlot   = slot,
        };

    public static GameObjectInfo Map( FileType type, byte c1, byte c2, byte c3, byte c4, byte variant, byte suffix = 0 )
        => new()
        {
            FileType   = type,
            ObjectType = ObjectType.Map,
            MapC1      = c1,
            MapC2      = c2,
            MapC3      = c3,
            MapC4      = c4,
            MapSuffix  = suffix,
            Variant    = variant,
        };

    public static GameObjectInfo Icon( FileType type, uint iconId, bool hq, bool hr, ClientLanguage lang = ClientLanguage.English )
        => new()
        {
            FileType   = type,
            ObjectType = ObjectType.Icon,
            IconId     = iconId,
            IconHqHr   = ( byte )( hq ? hr ? 3 : 1 : hr ? 2 : 0 ),
            Language   = lang,
        };


    [FieldOffset( 0 )]
    public readonly ulong Identifier;

    [FieldOffset( 0 )]
    public FileType FileType;

    [FieldOffset( 1 )]
    public ObjectType ObjectType;


    [FieldOffset( 2 )]
    public ushort PrimaryId; // Equipment, Weapon, Customization, Monster, DemiHuman

    [FieldOffset( 2 )]
    public uint IconId; // Icon

    [FieldOffset( 2 )]
    public byte MapC1; // Map

    [FieldOffset( 3 )]
    public byte MapC2; // Map

    [FieldOffset( 4 )]
    public ushort SecondaryId; // Weapon, Monster, Demihuman

    [FieldOffset( 4 )]
    public byte MapC3; // Map

    [FieldOffset( 4 )]
    private byte _genderRaceByte; // Equipment, Customization

    public GenderRace GenderRace
    {
        get => Names.GenderRaceFromByte( _genderRaceByte );
        set => _genderRaceByte = value.ToByte();
    }

    [FieldOffset( 5 )]
    public BodySlot BodySlot; // Customization

    [FieldOffset( 5 )]
    public byte MapC4; // Map

    [FieldOffset( 6 )]
    public byte Variant; // Equipment, Weapon, Customization, Map, Monster, Demihuman

    [FieldOffset( 6 )]
    public byte IconHqHr; // Icon

    [FieldOffset( 7 )]
    public EquipSlot EquipSlot; // Equipment, Demihuman

    [FieldOffset( 7 )]
    public CustomizationType CustomizationType; // Customization

    [FieldOffset( 7 )]
    public ClientLanguage Language; // Icon

    [FieldOffset( 7 )]
    public byte MapSuffix;

    public override int GetHashCode()
        => Identifier.GetHashCode();

    public int CompareTo( object? r )
        => Identifier.CompareTo( r );
}