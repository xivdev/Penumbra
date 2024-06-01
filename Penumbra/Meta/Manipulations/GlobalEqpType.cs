using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Penumbra.Meta.Manipulations;

[JsonConverter(typeof(StringEnumConverter))]
public enum GlobalEqpType
{
    DoNotHideEarrings,
    DoNotHideNecklace,
    DoNotHideBracelets,
    DoNotHideRingR,
    DoNotHideRingL,
    DoNotHideHrothgarHats,
    DoNotHideVieraHats,
}

public static class GlobalEqpExtensions
{
    public static bool HasCondition(this GlobalEqpType type)
        => type switch
        {
            GlobalEqpType.DoNotHideEarrings     => true,
            GlobalEqpType.DoNotHideNecklace     => true,
            GlobalEqpType.DoNotHideBracelets    => true,
            GlobalEqpType.DoNotHideRingR        => true,
            GlobalEqpType.DoNotHideRingL        => true,
            GlobalEqpType.DoNotHideHrothgarHats => false,
            GlobalEqpType.DoNotHideVieraHats    => false,
            _                                   => false,
        };


    public static ReadOnlySpan<byte> ToName(this GlobalEqpType type)
        => type switch
        {
            GlobalEqpType.DoNotHideEarrings     => "Always Show Earrings"u8,
            GlobalEqpType.DoNotHideNecklace     => "Always Show Necklaces"u8,
            GlobalEqpType.DoNotHideBracelets    => "Always Show Bracelets"u8,
            GlobalEqpType.DoNotHideRingR        => "Always Show Rings (Right Finger)"u8,
            GlobalEqpType.DoNotHideRingL        => "Always Show Rings (Left Finger)"u8,
            GlobalEqpType.DoNotHideHrothgarHats => "Always Show Hats for Hrothgar"u8,
            GlobalEqpType.DoNotHideVieraHats    => "Always Show Hats for Viera"u8,
            _                                   => "\0"u8,
        };

    public static ReadOnlySpan<byte> ToDescription(this GlobalEqpType type)
        => type switch
        {
            GlobalEqpType.DoNotHideEarrings => "Prevents the game from hiding earrings through other models when a specific earring is worn."u8,
            GlobalEqpType.DoNotHideNecklace =>
                "Prevents the game from hiding necklaces through other models when a specific necklace is worn."u8,
            GlobalEqpType.DoNotHideBracelets =>
                "Prevents the game from hiding bracelets through other models when a specific bracelet is worn."u8,
            GlobalEqpType.DoNotHideRingR =>
                "Prevents the game from hiding rings worn on the right finger through other models when a specific ring is worn on the right finger."u8,
            GlobalEqpType.DoNotHideRingL =>
                "Prevents the game from hiding rings worn on the left finger through other models when a specific ring is worn on the left finger."u8,
            GlobalEqpType.DoNotHideHrothgarHats =>
                "Prevents the game from hiding any hats for Hrothgar that are normally flagged to not display on them."u8,
            GlobalEqpType.DoNotHideVieraHats =>
                "Prevents the game from hiding any hats for Viera that are normally flagged to not display on them."u8,
            _ => "\0"u8,
        };
}
