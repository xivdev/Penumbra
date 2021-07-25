using System;
using System.ComponentModel;
using Penumbra.GameData.Enums;

namespace Penumbra.GameData.Structs
{
    [Flags]
    public enum EqpEntry : ulong
    {
        BodyEnabled      = 0x00_01ul,
        BodyHideWaist    = 0x00_02ul,
        _2               = 0x00_04ul,
        BodyHideGlovesS  = 0x00_08ul,
        _4               = 0x00_10ul,
        BodyHideGlovesM  = 0x00_20ul,
        BodyHideGlovesL  = 0x00_40ul,
        BodyHideGorget   = 0x00_80ul,
        BodyShowLeg      = 0x01_00ul,
        BodyShowHand     = 0x02_00ul,
        BodyShowHead     = 0x04_00ul,
        BodyShowNecklace = 0x08_00ul,
        BodyShowBracelet = 0x10_00ul,
        BodyShowTail     = 0x20_00ul,
        _14              = 0x40_00ul,
        _15              = 0x80_00ul,
        BodyMask         = 0xFF_FFul,

        LegsEnabled      = 0x01ul << 16,
        LegsHideKneePads = 0x02ul << 16,
        LegsHideBootsS   = 0x04ul << 16,
        LegsHideBootsM   = 0x08ul << 16,
        _20              = 0x10ul << 16,
        LegsShowFoot     = 0x20ul << 16,
        LegsShowTail     = 0x40ul << 16,
        _23              = 0x80ul << 16,
        LegsMask         = 0xFFul << 16,

        HandsEnabled     = 0x01ul << 24,
        HandsHideElbow   = 0x02ul << 24,
        HandsHideForearm = 0x04ul << 24,
        _27              = 0x08ul << 24,
        HandShowBracelet = 0x10ul << 24,
        HandShowRingL    = 0x20ul << 24,
        HandShowRingR    = 0x40ul << 24,
        _31              = 0x80ul << 24,
        HandsMask        = 0xFFul << 24,

        FeetEnabled   = 0x01ul << 32,
        FeetHideKnee  = 0x02ul << 32,
        FeetHideCalf  = 0x04ul << 32,
        FeetHideAnkle = 0x08ul << 32,
        _36           = 0x10ul << 32,
        _37           = 0x20ul << 32,
        _38           = 0x40ul << 32,
        _39           = 0x80ul << 32,
        FeetMask      = 0xFFul << 32,

        HeadEnabled           = 0x00_00_01ul << 40,
        HeadHideScalp         = 0x00_00_02ul << 40,
        HeadHideHair          = 0x00_00_04ul << 40,
        HeadShowHairOverride  = 0x00_00_08ul << 40,
        HeadHideNeck          = 0x00_00_10ul << 40,
        HeadShowNecklace      = 0x00_00_20ul << 40,
        _46                   = 0x00_00_40ul << 40,
        HeadShowEarrings      = 0x00_00_80ul << 40,
        HeadShowEarringsHuman = 0x00_01_00ul << 40,
        HeadShowEarringsAura  = 0x00_02_00ul << 40,
        HeadShowEarHuman      = 0x00_04_00ul << 40,
        HeadShowEarMiqote     = 0x00_08_00ul << 40,
        HeadShowEarAuRa       = 0x00_10_00ul << 40,
        HeadShowEarViera      = 0x00_20_00ul << 40,
        _54                   = 0x00_40_00ul << 40,
        _55                   = 0x00_80_00ul << 40,
        HeadShowHrothgarHat   = 0x01_00_00ul << 40,
        HeadShowVieraHat      = 0x02_00_00ul << 40,
        _58                   = 0x04_00_00ul << 40,
        _59                   = 0x08_00_00ul << 40,
        _60                   = 0x10_00_00ul << 40,
        _61                   = 0x20_00_00ul << 40,
        _62                   = 0x40_00_00ul << 40,
        _63                   = 0x80_00_00ul << 40,
        HeadMask              = 0xFF_FF_FFul << 40,
    }

    public static class Eqp
    {
        public static (int, int) BytesAndOffset( EquipSlot slot )
        {
            return slot switch
            {
                EquipSlot.Body  => ( 2, 0 ),
                EquipSlot.Legs  => ( 1, 2 ),
                EquipSlot.Hands => ( 1, 3 ),
                EquipSlot.Feet  => ( 1, 4 ),
                EquipSlot.Head  => ( 3, 5 ),
                _               => throw new InvalidEnumArgumentException(),
            };
        }

        public static EqpEntry FromSlotAndBytes( EquipSlot slot, byte[] value )
        {
            EqpEntry ret = 0;
            var (bytes, offset) = BytesAndOffset( slot );
            if( bytes != value.Length )
            {
                throw new ArgumentException();
            }

            for( var i = 0; i < bytes; ++i )
            {
                ret |= ( EqpEntry )( ( ulong )value[ i ] << ( ( offset + i ) * 8 ) );
            }

            return ret;
        }

        public static EqpEntry Mask( EquipSlot slot )
        {
            return slot switch
            {
                EquipSlot.Body  => EqpEntry.BodyMask,
                EquipSlot.Head  => EqpEntry.HeadMask,
                EquipSlot.Legs  => EqpEntry.LegsMask,
                EquipSlot.Feet  => EqpEntry.FeetMask,
                EquipSlot.Hands => EqpEntry.HandsMask,
                _               => 0,
            };
        }
    }
}