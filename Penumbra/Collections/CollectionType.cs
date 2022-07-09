using System;
using System.Linq;
using Penumbra.GameData.Enums;

namespace Penumbra.Collections;

public enum CollectionType : byte
{
    // Special Collections
    Yourself = 0,
    PlayerCharacter,
    NonPlayerCharacter,
    Midlander,
    Highlander,
    Wildwood,
    Duskwight,
    Plainsfolk,
    Dunesfolk,
    SeekerOfTheSun,
    KeeperOfTheMoon,
    Seawolf,
    Hellsguard,
    Raen,
    Xaela,
    Helion,
    Lost,
    Rava,
    Veena,
    MidlanderNpc,
    HighlanderNpc,
    WildwoodNpc,
    DuskwightNpc,
    PlainsfolkNpc,
    DunesfolkNpc,
    SeekerOfTheSunNpc,
    KeeperOfTheMoonNpc,
    SeawolfNpc,
    HellsguardNpc,
    RaenNpc,
    XaelaNpc,
    HelionNpc,
    LostNpc,
    RavaNpc,
    VeenaNpc,

    Inactive,  // A collection was added or removed
    Default,   // The default collection was changed
    Character, // A character collection was changed
    Current,   // The current collection was changed.
}

public static class CollectionTypeExtensions
{
    public static bool IsSpecial( this CollectionType collectionType )
        => collectionType is >= CollectionType.Yourself and < CollectionType.Inactive;

    public static readonly CollectionType[] Special = Enum.GetValues< CollectionType >().Where( IsSpecial ).ToArray();

    public static string ToName( this CollectionType collectionType )
        => collectionType switch
        {
            CollectionType.Yourself           => "Your Character",
            CollectionType.PlayerCharacter    => "Player Characters",
            CollectionType.NonPlayerCharacter => "Non-Player Characters",
            CollectionType.Midlander          => SubRace.Midlander.ToName(),
            CollectionType.Highlander         => SubRace.Highlander.ToName(),
            CollectionType.Wildwood           => SubRace.Wildwood.ToName(),
            CollectionType.Duskwight          => SubRace.Duskwight.ToName(),
            CollectionType.Plainsfolk         => SubRace.Plainsfolk.ToName(),
            CollectionType.Dunesfolk          => SubRace.Dunesfolk.ToName(),
            CollectionType.SeekerOfTheSun     => SubRace.SeekerOfTheSun.ToName(),
            CollectionType.KeeperOfTheMoon    => SubRace.KeeperOfTheMoon.ToName(),
            CollectionType.Seawolf            => SubRace.Seawolf.ToName(),
            CollectionType.Hellsguard         => SubRace.Hellsguard.ToName(),
            CollectionType.Raen               => SubRace.Raen.ToName(),
            CollectionType.Xaela              => SubRace.Xaela.ToName(),
            CollectionType.Helion             => SubRace.Helion.ToName(),
            CollectionType.Lost               => SubRace.Lost.ToName(),
            CollectionType.Rava               => SubRace.Rava.ToName(),
            CollectionType.Veena              => SubRace.Veena.ToName(),
            CollectionType.MidlanderNpc       => SubRace.Midlander.ToName()       + " (NPC)",
            CollectionType.HighlanderNpc      => SubRace.Highlander.ToName()      + " (NPC)",
            CollectionType.WildwoodNpc        => SubRace.Wildwood.ToName()        + " (NPC)",
            CollectionType.DuskwightNpc       => SubRace.Duskwight.ToName()       + " (NPC)",
            CollectionType.PlainsfolkNpc      => SubRace.Plainsfolk.ToName()      + " (NPC)",
            CollectionType.DunesfolkNpc       => SubRace.Dunesfolk.ToName()       + " (NPC)",
            CollectionType.SeekerOfTheSunNpc  => SubRace.SeekerOfTheSun.ToName()  + " (NPC)",
            CollectionType.KeeperOfTheMoonNpc => SubRace.KeeperOfTheMoon.ToName() + " (NPC)",
            CollectionType.SeawolfNpc         => SubRace.Seawolf.ToName()         + " (NPC)",
            CollectionType.HellsguardNpc      => SubRace.Hellsguard.ToName()      + " (NPC)",
            CollectionType.RaenNpc            => SubRace.Raen.ToName()            + " (NPC)",
            CollectionType.XaelaNpc           => SubRace.Xaela.ToName()           + " (NPC)",
            CollectionType.HelionNpc          => SubRace.Helion.ToName()          + " (NPC)",
            CollectionType.LostNpc            => SubRace.Lost.ToName()            + " (NPC)",
            CollectionType.RavaNpc            => SubRace.Rava.ToName()            + " (NPC)",
            CollectionType.VeenaNpc           => SubRace.Veena.ToName()           + " (NPC)",
            CollectionType.Inactive           => "Collection",
            CollectionType.Default            => "Default",
            CollectionType.Character          => "Character",
            CollectionType.Current            => "Current",
            _                                 => string.Empty,
        };

    public static string ToDescription( this CollectionType collectionType )
        => collectionType switch
        {
            CollectionType.Yourself => "This collection applies to your own character, regardless of its name.\n"
              + "It takes precedence before all other collections except for explicitly named character collections.",
            CollectionType.PlayerCharacter =>
                "This collection applies to all player characters that do not have a more specific character or racial collections associated.",
            CollectionType.NonPlayerCharacter =>
                "This collection applies to all human non-player characters except those explicitly named. It takes precedence before the default and racial collections.",
            CollectionType.Midlander =>
                "This collection applies to all player character Midlander Hyur that do not have a more specific character collection associated.",
            CollectionType.Highlander =>
                "This collection applies to all player character Highlander Hyur that do not have a more specific character collection associated.",
            CollectionType.Wildwood =>
                "This collection applies to all player character Wildwood Elezen that do not have a more specific character collection associated.",
            CollectionType.Duskwight =>
                "This collection applies to all player character Duskwight Elezen that do not have a more specific character collection associated.",
            CollectionType.Plainsfolk =>
                "This collection applies to all player character Plainsfolk Lalafell that do not have a more specific character collection associated.",
            CollectionType.Dunesfolk =>
                "This collection applies to all player character Dunesfolk Lalafell that do not have a more specific character collection associated.",
            CollectionType.SeekerOfTheSun =>
                "This collection applies to all player character Seekers of the Sun that do not have a more specific character collection associated.",
            CollectionType.KeeperOfTheMoon =>
                "This collection applies to all player character Keepers of the Moon that do not have a more specific character collection associated.",
            CollectionType.Seawolf =>
                "This collection applies to all player character Sea Wolf Roegadyn that do not have a more specific character collection associated.",
            CollectionType.Hellsguard =>
                "This collection applies to all player character Hellsguard Roegadyn that do not have a more specific character collection associated.",
            CollectionType.Raen =>
                "This collection applies to all player character Raen Au Ra that do not have a more specific character collection associated.",
            CollectionType.Xaela =>
                "This collection applies to all player character Xaela Au Ra that do not have a more specific character collection associated.",
            CollectionType.Helion =>
                "This collection applies to all player character Helion Hrothgar that do not have a more specific character collection associated.",
            CollectionType.Lost =>
                "This collection applies to all player character Lost Hrothgar that do not have a more specific character collection associated.",
            CollectionType.Rava =>
                "This collection applies to all player character Rava Viera that do not have a more specific character collection associated.",
            CollectionType.Veena =>
                "This collection applies to all player character Veena Viera that do not have a more specific character collection associated.",
            CollectionType.MidlanderNpc =>
                "This collection applies to all non-player character Midlander Hyur that do not have a more specific character collection associated.",
            CollectionType.HighlanderNpc =>
                "This collection applies to all non-player character Highlander Hyur that do not have a more specific character collection associated.",
            CollectionType.WildwoodNpc =>
                "This collection applies to all non-player character Wildwood Elezen that do not have a more specific character collection associated.",
            CollectionType.DuskwightNpc =>
                "This collection applies to all non-player character Duskwight Elezen that do not have a more specific character collection associated.",
            CollectionType.PlainsfolkNpc =>
                "This collection applies to all non-player character Plainsfolk Lalafell that do not have a more specific character collection associated.",
            CollectionType.DunesfolkNpc =>
                "This collection applies to all non-player character Dunesfolk Lalafell that do not have a more specific character collection associated.",
            CollectionType.SeekerOfTheSunNpc =>
                "This collection applies to all non-player character Seekers of the Sun that do not have a more specific character collection associated.",
            CollectionType.KeeperOfTheMoonNpc =>
                "This collection applies to all non-player character Keepers of the Moon that do not have a more specific character collection associated.",
            CollectionType.SeawolfNpc =>
                "This collection applies to all non-player character Sea Wolf Roegadyn that do not have a more specific character collection associated.",
            CollectionType.HellsguardNpc =>
                "This collection applies to all non-player character Hellsguard Roegadyn that do not have a more specific character collection associated.",
            CollectionType.RaenNpc =>
                "This collection applies to all non-player character Raen Au Ra that do not have a more specific character collection associated.",
            CollectionType.XaelaNpc =>
                "This collection applies to all non-player character Xaela Au Ra that do not have a more specific character collection associated.",
            CollectionType.HelionNpc =>
                "This collection applies to all non-player character Helion Hrothgar that do not have a more specific character collection associated.",
            CollectionType.LostNpc =>
                "This collection applies to all non-player character Lost Hrothgar that do not have a more specific character collection associated.",
            CollectionType.RavaNpc =>
                "This collection applies to all non-player character Rava Viera that do not have a more specific character collection associated.",
            CollectionType.VeenaNpc =>
                "This collection applies to all non-player character Veena Viera that do not have a more specific character collection associated.",
            _ => string.Empty,
        };
}