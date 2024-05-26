namespace Penumbra.Meta.Manipulations;

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
            GlobalEqpType.DoNotHideEarrings     => "Do Not Hide Earrings"u8,
            GlobalEqpType.DoNotHideNecklace     => "Do Not Hide Necklaces"u8,
            GlobalEqpType.DoNotHideBracelets    => "Do Not Hide Bracelets"u8,
            GlobalEqpType.DoNotHideRingR        => "Do Not Hide Rings (Right Finger)"u8,
            GlobalEqpType.DoNotHideRingL        => "Do Not Hide Rings (Left Finger)"u8,
            GlobalEqpType.DoNotHideHrothgarHats => "Do Not Hide Hats for Hrothgar"u8,
            GlobalEqpType.DoNotHideVieraHats    => "Do Not Hide Hats for Viera"u8,
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
