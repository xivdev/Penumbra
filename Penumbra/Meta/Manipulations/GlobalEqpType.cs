using Luna.Generators;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Penumbra.Meta.Manipulations;

[NamedEnum(Utf16: false)]
[TooltipEnum]
[JsonConverter(typeof(StringEnumConverter))]
public enum GlobalEqpType
{
    [Name("Always Show Earrings")]
    [Tooltip("Prevents the game from hiding earrings through other models when a specific earring is worn.")]
    DoNotHideEarrings,

    [Name("Always Show Necklaces")]
    [Tooltip("Prevents the game from hiding necklaces through other models when a specific necklace is worn.")]
    DoNotHideNecklace,

    [Name("Always Show Bracelets")]
    [Tooltip("Prevents the game from hiding bracelets through other models when a specific bracelet is worn.")]
    DoNotHideBracelets,

    [Name("Always Show Rings (Right Finger)")]
    [Tooltip(
        "Prevents the game from hiding rings worn on the right finger through other models when a specific ring is worn on the right finger.")]
    DoNotHideRingR,

    [Name("Always Show Rings (Left Finger)")]
    [Tooltip(
        "Prevents the game from hiding rings worn on the left finger through other models when a specific ring is worn on the left finger.")]
    DoNotHideRingL,

    [Name("Always Show Hats for Hrothgar")]
    [Tooltip("Prevents the game from hiding any hats for Hrothgar that are normally flagged to not display on them.")]
    DoNotHideHrothgarHats,

    [Name("Always Show Hats for Viera")]
    [Tooltip("Prevents the game from hiding any hats for Viera that are normally flagged to not display on them.")]
    DoNotHideVieraHats,

    [Name("Always Hide Horns (Au Ra)")]
    [Tooltip("Forces the game to hide Au Ra horns regardless of headwear.")]
    HideHorns,

    [Name("Always Hide Horns (Viera)")]
    [Tooltip("Forces the game to hide Viera ears regardless of headwear.")]
    HideVieraEars,

    [Name("Always Hide Horns (Miqo'te)")]
    [Tooltip("Forces the game to hide Miqo'te ears regardless of headwear.")]
    HideMiqoteEars,
}

public static partial class GlobalEqpExtensions
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
            GlobalEqpType.HideHorns             => false,
            GlobalEqpType.HideVieraEars         => false,
            GlobalEqpType.HideMiqoteEars        => false,
            _                                   => false,
        };
}
