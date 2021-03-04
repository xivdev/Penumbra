using System.Collections.Generic;
using System.ComponentModel;

namespace Penumbra.Game
{
    public enum EquipSlot : byte
    {
        Unknown           = 0,
        MainHand          = 1,
        Offhand           = 2,
        Head              = 3,
        Body              = 4,
        Hands             = 5,
        Belt              = 6,
        Legs              = 7,
        Feet              = 8,
        Ears              = 9,
        Neck              = 10,
        RingR             = 12,
        RingL             = 14,
        Wrists            = 11,
        BothHand          = 13,
        HeadBody          = 15,
        BodyHandsLegsFeet = 16,
        SoulCrystal       = 17,
        LegsFeet          = 18,
        FullBody          = 19,
        BodyHands         = 20,
        BodyLegsFeet      = 21,
        All               = 22
    }

    public static class EquipSlotEnumExtension
    {
        public static string ToSuffix( this EquipSlot value )
        {
            return value switch
            {
                EquipSlot.Head   => "met",
                EquipSlot.Hands  => "glv",
                EquipSlot.Legs   => "dwn",
                EquipSlot.Feet   => "sho",
                EquipSlot.Body   => "top",
                EquipSlot.Ears   => "ear",
                EquipSlot.Neck   => "nek",
                EquipSlot.RingR  => "rir",
                EquipSlot.RingL  => "ril",
                EquipSlot.Wrists => "wrs",
                _                => throw new InvalidEnumArgumentException()
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
                _               => false
            };
        }

        public static bool IsAccessory( this EquipSlot value )
        {
            return value switch
            {
                EquipSlot.Ears   => true,
                EquipSlot.Neck   => true,
                EquipSlot.RingR  => true,
                EquipSlot.RingL  => true,
                EquipSlot.Wrists => true,
                _                => false
            };
        }
    }

    public static partial class GameData
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
            { EquipSlot.RingR.ToSuffix(), EquipSlot.RingR },
            { EquipSlot.RingL.ToSuffix(), EquipSlot.RingL },
            { EquipSlot.Wrists.ToSuffix(), EquipSlot.Wrists }
        };
    }
}