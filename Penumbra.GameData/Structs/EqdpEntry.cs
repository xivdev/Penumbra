using System;
using System.ComponentModel;
using Penumbra.GameData.Enums;

namespace Penumbra.GameData.Structs
{
    [Flags]
    public enum EqdpEntry : ushort
    {
        Invalid  = 0,
        Head1    = 0b0000000001,
        Head2    = 0b0000000010,
        HeadMask = 0b0000000011,

        Body1    = 0b0000000100,
        Body2    = 0b0000001000,
        BodyMask = 0b0000001100,

        Hands1    = 0b0000010000,
        Hands2    = 0b0000100000,
        HandsMask = 0b0000110000,

        Legs1    = 0b0001000000,
        Legs2    = 0b0010000000,
        LegsMask = 0b0011000000,

        Feet1    = 0b0100000000,
        Feet2    = 0b1000000000,
        FeetMask = 0b1100000000,

        Ears1    = 0b0000000001,
        Ears2    = 0b0000000010,
        EarsMask = 0b0000000011,

        Neck1    = 0b0000000100,
        Neck2    = 0b0000001000,
        NeckMask = 0b0000001100,

        Wrists1    = 0b0000010000,
        Wrists2    = 0b0000100000,
        WristsMask = 0b0000110000,

        RingR1    = 0b0001000000,
        RingR2    = 0b0010000000,
        RingRMask = 0b0011000000,

        RingL1    = 0b0100000000,
        RingL2    = 0b1000000000,
        RingLMask = 0b1100000000,
    }

    public static class Eqdp
    {
        public static int Offset( EquipSlot slot )
        {
            return slot switch
            {
                EquipSlot.Head   => 0,
                EquipSlot.Body   => 2,
                EquipSlot.Hands  => 4,
                EquipSlot.Legs   => 6,
                EquipSlot.Feet   => 8,
                EquipSlot.Ears   => 0,
                EquipSlot.Neck   => 2,
                EquipSlot.Wrists => 4,
                EquipSlot.RingR  => 6,
                EquipSlot.RingL  => 8,
                _                => throw new InvalidEnumArgumentException(),
            };
        }

        public static EqdpEntry FromSlotAndBits( EquipSlot slot, bool bit1, bool bit2 )
        {
            EqdpEntry ret    = 0;
            var       offset = Offset( slot );
            if( bit1 )
            {
                ret |= ( EqdpEntry )( 1 << offset );
            }

            if( bit2 )
            {
                ret |= ( EqdpEntry )( 1 << ( offset + 1 ) );
            }

            return ret;
        }

        public static EqdpEntry Mask( EquipSlot slot )
        {
            return slot switch
            {
                EquipSlot.Head   => EqdpEntry.HeadMask,
                EquipSlot.Body   => EqdpEntry.BodyMask,
                EquipSlot.Hands  => EqdpEntry.HandsMask,
                EquipSlot.Legs   => EqdpEntry.LegsMask,
                EquipSlot.Feet   => EqdpEntry.FeetMask,
                EquipSlot.Ears   => EqdpEntry.EarsMask,
                EquipSlot.Neck   => EqdpEntry.NeckMask,
                EquipSlot.Wrists => EqdpEntry.WristsMask,
                EquipSlot.RingR  => EqdpEntry.RingRMask,
                EquipSlot.RingL  => EqdpEntry.RingLMask,
                _                => 0,
            };
        }
    }
}