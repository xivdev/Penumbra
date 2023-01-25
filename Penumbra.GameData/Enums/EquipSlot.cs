using System;
using System.Collections.Generic;
using System.Linq;

namespace Penumbra.GameData.Enums;

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
    ChestHands        = 22,
    Nothing           = 23,
    All               = 24, // Not officially existing
}

public static class EquipSlotExtensions
{
    public static EquipSlot ToEquipSlot(this uint value)
        => value switch
        {
            0  => EquipSlot.Head,
            1  => EquipSlot.Body,
            2  => EquipSlot.Hands,
            3  => EquipSlot.Legs,
            4  => EquipSlot.Feet,
            5  => EquipSlot.Ears,
            6  => EquipSlot.Neck,
            7  => EquipSlot.Wrists,
            8  => EquipSlot.RFinger,
            9  => EquipSlot.LFinger,
            10 => EquipSlot.MainHand,
            11 => EquipSlot.OffHand,
            _  => EquipSlot.Unknown,
        };

    public static uint ToIndex(this EquipSlot slot)
        => slot switch
        {
            EquipSlot.Head     => 0,
            EquipSlot.Body     => 1,
            EquipSlot.Hands    => 2,
            EquipSlot.Legs     => 3,
            EquipSlot.Feet     => 4,
            EquipSlot.Ears     => 5,
            EquipSlot.Neck     => 6,
            EquipSlot.Wrists   => 7,
            EquipSlot.RFinger  => 8,
            EquipSlot.LFinger  => 9,
            EquipSlot.MainHand => 10,
            EquipSlot.OffHand  => 11,
            _                  => uint.MaxValue,
        };

    public static string ToSuffix(this EquipSlot value)
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
            _                 => "unk",
        };
    }

    public static EquipSlot ToSlot(this EquipSlot value)
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
            EquipSlot.ChestHands        => EquipSlot.Body,
            _                           => EquipSlot.Unknown,
        };
    }

    public static string ToName(this EquipSlot value)
    {
        return value switch
        {
            EquipSlot.Head              => "Head",
            EquipSlot.Hands             => "Hands",
            EquipSlot.Legs              => "Legs",
            EquipSlot.Feet              => "Feet",
            EquipSlot.Body              => "Body",
            EquipSlot.Ears              => "Earrings",
            EquipSlot.Neck              => "Necklace",
            EquipSlot.RFinger           => "Right Ring",
            EquipSlot.LFinger           => "Left Ring",
            EquipSlot.Wrists            => "Bracelets",
            EquipSlot.MainHand          => "Primary Weapon",
            EquipSlot.OffHand           => "Secondary Weapon",
            EquipSlot.Belt              => "Belt",
            EquipSlot.BothHand          => "Primary Weapon",
            EquipSlot.HeadBody          => "Head and Body",
            EquipSlot.BodyHandsLegsFeet => "Costume",
            EquipSlot.SoulCrystal       => "Soul Crystal",
            EquipSlot.LegsFeet          => "Bottom",
            EquipSlot.FullBody          => "Costume",
            EquipSlot.BodyHands         => "Top",
            EquipSlot.BodyLegsFeet      => "Costume",
            EquipSlot.All               => "Costume",
            _                           => "Unknown",
        };
    }

    public static bool IsEquipment(this EquipSlot value)
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

    public static bool IsAccessory(this EquipSlot value)
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

    public static bool IsEquipmentPiece(this EquipSlot value)
    {
        return value switch
        {
            // Accessories
            EquipSlot.RFinger => true,
            EquipSlot.Wrists  => true,
            EquipSlot.Ears    => true,
            EquipSlot.Neck    => true,
            // Equipment
            EquipSlot.Head              => true,
            EquipSlot.Body              => true,
            EquipSlot.Hands             => true,
            EquipSlot.Legs              => true,
            EquipSlot.Feet              => true,
            EquipSlot.BodyHands         => true,
            EquipSlot.BodyHandsLegsFeet => true,
            EquipSlot.BodyLegsFeet      => true,
            EquipSlot.FullBody          => true,
            EquipSlot.HeadBody          => true,
            EquipSlot.LegsFeet          => true,
            EquipSlot.ChestHands        => true,
            _                           => false,
        };
    }

    public static readonly EquipSlot[] EquipmentSlots = Enum.GetValues<EquipSlot>().Where(e => e.IsEquipment()).ToArray();
    public static readonly EquipSlot[] AccessorySlots = Enum.GetValues<EquipSlot>().Where(e => e.IsAccessory()).ToArray();
    public static readonly EquipSlot[] EqdpSlots      = EquipmentSlots.Concat(AccessorySlots).ToArray();
}

public static partial class Names
{
    public static readonly Dictionary<string, EquipSlot> SuffixToEquipSlot = new()
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
