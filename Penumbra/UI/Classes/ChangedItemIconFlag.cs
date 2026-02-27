using Luna.Generators;
using Penumbra.Api.Enums;

namespace Penumbra.UI.Classes;

[Flags]
[NamedEnum(Utf16: false)]
public enum ChangedItemIconFlag : uint
{
    [Name("Head")]
    Head = 0x00_00_01,

    [Name("Body")]
    Body = 0x00_00_02,

    [Name("Hands")]
    Hands = 0x00_00_04,

    [Name("Legs")]
    Legs = 0x00_00_08,

    [Name("Feet")]
    Feet = 0x00_00_10,

    [Name("Earrings")]
    Ears = 0x00_00_20,

    [Name("Necklace")]
    Neck = 0x00_00_40,

    [Name("Bracelets")]
    Wrists = 0x00_00_80,

    [Name("Ring")]
    Finger = 0x00_01_00,

    [Name("Monster")]
    Monster = 0x00_02_00,

    [Name("Demi-Human")]
    Demihuman = 0x00_04_00,

    [Name("Customization")]
    Customization = 0x00_08_00,

    [Name("Action")]
    Action = 0x00_10_00,

    [Name("Weapon (Mainhand)")]
    Mainhand = 0x00_20_00,

    [Name("Weapon (Offhand)")]
    Offhand = 0x00_40_00,

    [Name("Other")]
    Unknown = 0x00_80_00,

    [Name("Emote")]
    Emote = 0x01_00_00,
}

public static class ChangedItemFlagExtensions
{
    public static readonly IReadOnlyList<ChangedItemIconFlag> Order =
    [
        ChangedItemIconFlag.Head,
        ChangedItemIconFlag.Body,
        ChangedItemIconFlag.Hands,
        ChangedItemIconFlag.Legs,
        ChangedItemIconFlag.Feet,
        ChangedItemIconFlag.Ears,
        ChangedItemIconFlag.Neck,
        ChangedItemIconFlag.Wrists,
        ChangedItemIconFlag.Finger,
        ChangedItemIconFlag.Mainhand,
        ChangedItemIconFlag.Offhand,
        ChangedItemIconFlag.Customization,
        ChangedItemIconFlag.Action,
        ChangedItemIconFlag.Emote,
        ChangedItemIconFlag.Monster,
        ChangedItemIconFlag.Demihuman,
        ChangedItemIconFlag.Unknown,
    ];

    public const           ChangedItemIconFlag AllFlags      = (ChangedItemIconFlag)0x01FFFF;
    public static readonly int                 NumCategories = Order.Count;
    public const           ChangedItemIconFlag DefaultFlags  = AllFlags;

    public static ChangedItemIcon ToApiIcon(this ChangedItemIconFlag iconFlag)
        => iconFlag switch
        {
            ChangedItemIconFlag.Head          => ChangedItemIcon.Head,
            ChangedItemIconFlag.Body          => ChangedItemIcon.Body,
            ChangedItemIconFlag.Hands         => ChangedItemIcon.Hands,
            ChangedItemIconFlag.Legs          => ChangedItemIcon.Legs,
            ChangedItemIconFlag.Feet          => ChangedItemIcon.Feet,
            ChangedItemIconFlag.Ears          => ChangedItemIcon.Ears,
            ChangedItemIconFlag.Neck          => ChangedItemIcon.Neck,
            ChangedItemIconFlag.Wrists        => ChangedItemIcon.Wrists,
            ChangedItemIconFlag.Finger        => ChangedItemIcon.Finger,
            ChangedItemIconFlag.Monster       => ChangedItemIcon.Monster,
            ChangedItemIconFlag.Demihuman     => ChangedItemIcon.Demihuman,
            ChangedItemIconFlag.Customization => ChangedItemIcon.Customization,
            ChangedItemIconFlag.Action        => ChangedItemIcon.Action,
            ChangedItemIconFlag.Emote         => ChangedItemIcon.Emote,
            ChangedItemIconFlag.Mainhand      => ChangedItemIcon.Mainhand,
            ChangedItemIconFlag.Offhand       => ChangedItemIcon.Offhand,
            ChangedItemIconFlag.Unknown       => ChangedItemIcon.Unknown,
            _                                 => ChangedItemIcon.None,
        };

    public static ChangedItemIconFlag ToFlag(this ChangedItemIcon icon)
        => icon switch
        {
            ChangedItemIcon.Unknown       => ChangedItemIconFlag.Unknown,
            ChangedItemIcon.Head          => ChangedItemIconFlag.Head,
            ChangedItemIcon.Body          => ChangedItemIconFlag.Body,
            ChangedItemIcon.Hands         => ChangedItemIconFlag.Hands,
            ChangedItemIcon.Legs          => ChangedItemIconFlag.Legs,
            ChangedItemIcon.Feet          => ChangedItemIconFlag.Feet,
            ChangedItemIcon.Ears          => ChangedItemIconFlag.Ears,
            ChangedItemIcon.Neck          => ChangedItemIconFlag.Neck,
            ChangedItemIcon.Wrists        => ChangedItemIconFlag.Wrists,
            ChangedItemIcon.Finger        => ChangedItemIconFlag.Finger,
            ChangedItemIcon.Mainhand      => ChangedItemIconFlag.Mainhand,
            ChangedItemIcon.Offhand       => ChangedItemIconFlag.Offhand,
            ChangedItemIcon.Customization => ChangedItemIconFlag.Customization,
            ChangedItemIcon.Monster       => ChangedItemIconFlag.Monster,
            ChangedItemIcon.Demihuman     => ChangedItemIconFlag.Demihuman,
            ChangedItemIcon.Action        => ChangedItemIconFlag.Action,
            ChangedItemIcon.Emote         => ChangedItemIconFlag.Emote,
            _                             => ChangedItemIconFlag.Unknown,
        };
}
