using System.Collections.Generic;
using System.ComponentModel;

namespace Penumbra.GameData.Enums
{
    public enum EquipSlot : byte
    {
        Unknown           = 0,
        MainHand          = 1,
        OffHand           = 2,
        Head              = 3,
        Body              = 4,
        Hands             = 5,
        Belt              = 6,
        Legs              = 7,
        Feet              = 8,
        Ears              = 9,
        Neck              = 10,
        Wrists            = 11,
        RFinger           = 12,
        BothHand          = 13,
        LFinger           = 14, // Not officially existing, means "weapon could be equipped in either hand" for the game.
        HeadBody          = 15,
        BodyHandsLegsFeet = 16,
        SoulCrystal       = 17,
        LegsFeet          = 18,
        FullBody          = 19,
        BodyHands         = 20,
        BodyLegsFeet      = 21,
        All               = 22, // Not officially existing
    }

    public static class EquipSlotEnumExtension
    {
        public static string ToSuffix( this EquipSlot value )
        {
            return value switch
            {
                EquipSlot.Head    => "met",
                EquipSlot.Hands   => "glv",
                EquipSlot.Legs    => "dwn",
                EquipSlot.Feet    => "sho",
                EquipSlot.Body    => "top",
                EquipSlot.Ears    => "ear",
                EquipSlot.Neck    => "nek",
                EquipSlot.RFinger => "rir",
                EquipSlot.LFinger => "ril",
                EquipSlot.Wrists  => "wrs",
                _                 => throw new InvalidEnumArgumentException(),
            };
        }

        public static EquipSlot ToSlot( this EquipSlot value )
        {
            return value switch
            {
                EquipSlot.MainHand          => EquipSlot.MainHand,
                EquipSlot.OffHand           => EquipSlot.OffHand,
                EquipSlot.Head              => EquipSlot.Head,
                EquipSlot.Body              => EquipSlot.Body,
                EquipSlot.Hands             => EquipSlot.Hands,
                EquipSlot.Belt              => EquipSlot.Belt,
                EquipSlot.Legs              => EquipSlot.Legs,
                EquipSlot.Feet              => EquipSlot.Feet,
                EquipSlot.Ears              => EquipSlot.Ears,
                EquipSlot.Neck              => EquipSlot.Neck,
                EquipSlot.Wrists            => EquipSlot.Wrists,
                EquipSlot.RFinger           => EquipSlot.RFinger,
                EquipSlot.BothHand          => EquipSlot.MainHand,
                EquipSlot.LFinger           => EquipSlot.RFinger,
                EquipSlot.HeadBody          => EquipSlot.Body,
                EquipSlot.BodyHandsLegsFeet => EquipSlot.Body,
                EquipSlot.SoulCrystal       => EquipSlot.SoulCrystal,
                EquipSlot.LegsFeet          => EquipSlot.Legs,
                EquipSlot.FullBody          => EquipSlot.Body,
                EquipSlot.BodyHands         => EquipSlot.Body,
                EquipSlot.BodyLegsFeet      => EquipSlot.Body,
                _                           => throw new InvalidEnumArgumentException(),
            };
        }

        public static bool IsEquipment( this EquipSlot value )
        {
            return value switch
            {
                EquipSlot.Head  => true,
                EquipSlot.Hands => true,
                EquipSlot.Legs  => true,
                EquipSlot.Feet  => true,
                EquipSlot.Body  => true,
                _               => false,
            };
        }

        public static bool IsAccessory( this EquipSlot value )
        {
            return value switch
            {
                EquipSlot.Ears    => true,
                EquipSlot.Neck    => true,
                EquipSlot.RFinger => true,
                EquipSlot.LFinger => true,
                EquipSlot.Wrists  => true,
                _                 => false,
            };
        }
    }

    public static partial class Names
    {
        public static readonly Dictionary< string, EquipSlot > SuffixToEquipSlot = new()
        {
            { EquipSlot.Head.ToSuffix(), EquipSlot.Head },
            { EquipSlot.Hands.ToSuffix(), EquipSlot.Hands },
            { EquipSlot.Legs.ToSuffix(), EquipSlot.Legs },
            { EquipSlot.Feet.ToSuffix(), EquipSlot.Feet },
            { EquipSlot.Body.ToSuffix(), EquipSlot.Body },
            { EquipSlot.Ears.ToSuffix(), EquipSlot.Ears },
            { EquipSlot.Neck.ToSuffix(), EquipSlot.Neck },
            { EquipSlot.RFinger.ToSuffix(), EquipSlot.RFinger },
            { EquipSlot.LFinger.ToSuffix(), EquipSlot.LFinger },
            { EquipSlot.Wrists.ToSuffix(), EquipSlot.Wrists },
        };
    }
}