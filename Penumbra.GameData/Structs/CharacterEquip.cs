using System;
using Penumbra.GameData.Enums;
using Penumbra.String.Functions;

namespace Penumbra.GameData.Structs;

public readonly unsafe struct CharacterEquip
{
    public static readonly CharacterEquip Null = new(null);

    private readonly CharacterArmor* _armor;

    public IntPtr Address
        => ( IntPtr )_armor;

    public ref CharacterArmor this[ int idx ]
        => ref _armor[ idx ];

    public ref CharacterArmor this[ uint idx ]
        => ref _armor[ idx ];

    public ref CharacterArmor this[ EquipSlot slot ]
        => ref _armor[ IndexOf( slot ) ];

    public ref CharacterArmor Head
        => ref _armor[ 0 ];

    public ref CharacterArmor Body
        => ref _armor[ 1 ];

    public ref CharacterArmor Hands
        => ref _armor[ 2 ];

    public ref CharacterArmor Legs
        => ref _armor[ 3 ];

    public ref CharacterArmor Feet
        => ref _armor[ 4 ];

    public ref CharacterArmor Ears
        => ref _armor[ 5 ];

    public ref CharacterArmor Neck
        => ref _armor[ 6 ];

    public ref CharacterArmor Wrists
        => ref _armor[ 7 ];

    public ref CharacterArmor RFinger
        => ref _armor[ 8 ];

    public ref CharacterArmor LFinger
        => ref _armor[ 9 ];

    public CharacterEquip( CharacterArmor* val )
        => _armor = val;

    public static implicit operator CharacterEquip( CharacterArmor* val )
        => new(val);

    public static implicit operator CharacterEquip( IntPtr val )
        => new(( CharacterArmor* )val);

    public static implicit operator CharacterEquip( ReadOnlySpan< CharacterArmor > val )
    {
        if( val.Length != 10 )
        {
            throw new ArgumentException( "Invalid number of equipment pieces in span." );
        }

        fixed( CharacterArmor* ptr = val )
        {
            return new CharacterEquip( ptr );
        }
    }

    public static implicit operator bool( CharacterEquip equip )
        => equip._armor != null;

    public static bool operator true( CharacterEquip equip )
        => equip._armor != null;

    public static bool operator false( CharacterEquip equip )
        => equip._armor == null;

    public static bool operator !( CharacterEquip equip )
        => equip._armor == null;

    private static int IndexOf( EquipSlot slot )
    {
        return slot switch
        {
            EquipSlot.Head    => 0,
            EquipSlot.Body    => 1,
            EquipSlot.Hands   => 2,
            EquipSlot.Legs    => 3,
            EquipSlot.Feet    => 4,
            EquipSlot.Ears    => 5,
            EquipSlot.Neck    => 6,
            EquipSlot.Wrists  => 7,
            EquipSlot.RFinger => 8,
            EquipSlot.LFinger => 9,
            _                 => throw new ArgumentOutOfRangeException( nameof( slot ), slot, null ),
        };
    }


    public void Load( CharacterEquip source )
    {
        MemoryUtility.MemCpyUnchecked( _armor, source._armor, sizeof( CharacterArmor ) * 10 );
    }

    public bool Equals( CharacterEquip other )
        => MemoryUtility.MemCmpUnchecked( ( void* )_armor, ( void* )other._armor, sizeof( CharacterArmor ) * 10 ) == 0;
}