using Penumbra.GameData.Enums;
using System;
using System.Linq;

namespace Penumbra.Collections;

public enum CollectionType : byte
{
    // Special Collections
    Yourself = 0,

    MalePlayerCharacter,
    FemalePlayerCharacter,
    MaleNonPlayerCharacter,
    FemaleNonPlayerCharacter,

    MaleMidlander,
    FemaleMidlander,
    MaleHighlander,
    FemaleHighlander,

    MaleWildwood,
    FemaleWildwood,
    MaleDuskwight,
    FemaleDuskwight,

    MalePlainsfolk,
    FemalePlainsfolk,
    MaleDunesfolk,
    FemaleDunesfolk,

    MaleSeekerOfTheSun,
    FemaleSeekerOfTheSun,
    MaleKeeperOfTheMoon,
    FemaleKeeperOfTheMoon,

    MaleSeawolf,
    FemaleSeawolf,
    MaleHellsguard,
    FemaleHellsguard,

    MaleRaen,
    FemaleRaen,
    MaleXaela,
    FemaleXaela,

    MaleHelion,
    FemaleHelion,
    MaleLost,
    FemaleLost,

    MaleRava,
    FemaleRava,
    MaleVeena,
    FemaleVeena,

    MaleMidlanderNpc,
    FemaleMidlanderNpc,
    MaleHighlanderNpc,
    FemaleHighlanderNpc,

    MaleWildwoodNpc,
    FemaleWildwoodNpc,
    MaleDuskwightNpc,
    FemaleDuskwightNpc,

    MalePlainsfolkNpc,
    FemalePlainsfolkNpc,
    MaleDunesfolkNpc,
    FemaleDunesfolkNpc,

    MaleSeekerOfTheSunNpc,
    FemaleSeekerOfTheSunNpc,
    MaleKeeperOfTheMoonNpc,
    FemaleKeeperOfTheMoonNpc,

    MaleSeawolfNpc,
    FemaleSeawolfNpc,
    MaleHellsguardNpc,
    FemaleHellsguardNpc,

    MaleRaenNpc,
    FemaleRaenNpc,
    MaleXaelaNpc,
    FemaleXaelaNpc,

    MaleHelionNpc,
    FemaleHelionNpc,
    MaleLostNpc,
    FemaleLostNpc,

    MaleRavaNpc,
    FemaleRavaNpc,
    MaleVeenaNpc,
    FemaleVeenaNpc,

    Inactive,   // A collection was added or removed
    Default,    // The default collection was changed
    Interface,  // The ui collection was changed
    Individual, // An individual collection was changed
    Current,    // The current collection was changed
    Temporary,  // A temporary collections was set or deleted via IPC
}

public static class CollectionTypeExtensions
{
    public static bool IsSpecial( this CollectionType collectionType )
        => collectionType is >= CollectionType.Yourself and < CollectionType.Inactive;

    public static readonly (CollectionType, string, string)[] Special = Enum.GetValues< CollectionType >()
       .Where( IsSpecial )
       .Select( s => ( s, s.ToName(), s.ToDescription() ) )
       .ToArray();

    public static CollectionType FromParts( Gender gender, bool npc )
    {
        gender = gender switch
        {
            Gender.MaleNpc   => Gender.Male,
            Gender.FemaleNpc => Gender.Female,
            _                => gender,
        };

        return ( gender, npc ) switch
        {
            (Gender.Male, false)   => CollectionType.MalePlayerCharacter,
            (Gender.Female, false) => CollectionType.FemalePlayerCharacter,
            (Gender.Male, true)    => CollectionType.MaleNonPlayerCharacter,
            (Gender.Female, true)  => CollectionType.FemaleNonPlayerCharacter,
            _                      => CollectionType.Inactive,
        };
    }

    public static CollectionType FromParts( SubRace race, Gender gender, bool npc )
    {
        gender = gender switch
        {
            Gender.MaleNpc   => Gender.Male,
            Gender.FemaleNpc => Gender.Female,
            _                => gender,
        };

        return ( race, gender, npc ) switch
        {
            (SubRace.Midlander, Gender.Male, false)       => CollectionType.MaleMidlander,
            (SubRace.Highlander, Gender.Male, false)      => CollectionType.MaleHighlander,
            (SubRace.Wildwood, Gender.Male, false)        => CollectionType.MaleWildwood,
            (SubRace.Duskwight, Gender.Male, false)       => CollectionType.MaleDuskwight,
            (SubRace.Plainsfolk, Gender.Male, false)      => CollectionType.MalePlainsfolk,
            (SubRace.Dunesfolk, Gender.Male, false)       => CollectionType.MaleDunesfolk,
            (SubRace.SeekerOfTheSun, Gender.Male, false)  => CollectionType.MaleSeekerOfTheSun,
            (SubRace.KeeperOfTheMoon, Gender.Male, false) => CollectionType.MaleKeeperOfTheMoon,
            (SubRace.Seawolf, Gender.Male, false)         => CollectionType.MaleSeawolf,
            (SubRace.Hellsguard, Gender.Male, false)      => CollectionType.MaleHellsguard,
            (SubRace.Raen, Gender.Male, false)            => CollectionType.MaleRaen,
            (SubRace.Xaela, Gender.Male, false)           => CollectionType.MaleXaela,
            (SubRace.Helion, Gender.Male, false)          => CollectionType.MaleHelion,
            (SubRace.Lost, Gender.Male, false)            => CollectionType.MaleLost,
            (SubRace.Rava, Gender.Male, false)            => CollectionType.MaleRava,
            (SubRace.Veena, Gender.Male, false)           => CollectionType.MaleVeena,

            (SubRace.Midlander, Gender.Female, false)       => CollectionType.FemaleMidlander,
            (SubRace.Highlander, Gender.Female, false)      => CollectionType.FemaleHighlander,
            (SubRace.Wildwood, Gender.Female, false)        => CollectionType.FemaleWildwood,
            (SubRace.Duskwight, Gender.Female, false)       => CollectionType.FemaleDuskwight,
            (SubRace.Plainsfolk, Gender.Female, false)      => CollectionType.FemalePlainsfolk,
            (SubRace.Dunesfolk, Gender.Female, false)       => CollectionType.FemaleDunesfolk,
            (SubRace.SeekerOfTheSun, Gender.Female, false)  => CollectionType.FemaleSeekerOfTheSun,
            (SubRace.KeeperOfTheMoon, Gender.Female, false) => CollectionType.FemaleKeeperOfTheMoon,
            (SubRace.Seawolf, Gender.Female, false)         => CollectionType.FemaleSeawolf,
            (SubRace.Hellsguard, Gender.Female, false)      => CollectionType.FemaleHellsguard,
            (SubRace.Raen, Gender.Female, false)            => CollectionType.FemaleRaen,
            (SubRace.Xaela, Gender.Female, false)           => CollectionType.FemaleXaela,
            (SubRace.Helion, Gender.Female, false)          => CollectionType.FemaleHelion,
            (SubRace.Lost, Gender.Female, false)            => CollectionType.FemaleLost,
            (SubRace.Rava, Gender.Female, false)            => CollectionType.FemaleRava,
            (SubRace.Veena, Gender.Female, false)           => CollectionType.FemaleVeena,

            (SubRace.Midlander, Gender.Male, true)       => CollectionType.MaleMidlanderNpc,
            (SubRace.Highlander, Gender.Male, true)      => CollectionType.MaleHighlanderNpc,
            (SubRace.Wildwood, Gender.Male, true)        => CollectionType.MaleWildwoodNpc,
            (SubRace.Duskwight, Gender.Male, true)       => CollectionType.MaleDuskwightNpc,
            (SubRace.Plainsfolk, Gender.Male, true)      => CollectionType.MalePlainsfolkNpc,
            (SubRace.Dunesfolk, Gender.Male, true)       => CollectionType.MaleDunesfolkNpc,
            (SubRace.SeekerOfTheSun, Gender.Male, true)  => CollectionType.MaleSeekerOfTheSunNpc,
            (SubRace.KeeperOfTheMoon, Gender.Male, true) => CollectionType.MaleKeeperOfTheMoonNpc,
            (SubRace.Seawolf, Gender.Male, true)         => CollectionType.MaleSeawolfNpc,
            (SubRace.Hellsguard, Gender.Male, true)      => CollectionType.MaleHellsguardNpc,
            (SubRace.Raen, Gender.Male, true)            => CollectionType.MaleRaenNpc,
            (SubRace.Xaela, Gender.Male, true)           => CollectionType.MaleXaelaNpc,
            (SubRace.Helion, Gender.Male, true)          => CollectionType.MaleHelionNpc,
            (SubRace.Lost, Gender.Male, true)            => CollectionType.MaleLostNpc,
            (SubRace.Rava, Gender.Male, true)            => CollectionType.MaleRavaNpc,
            (SubRace.Veena, Gender.Male, true)           => CollectionType.MaleVeenaNpc,

            (SubRace.Midlander, Gender.Female, true)       => CollectionType.FemaleMidlanderNpc,
            (SubRace.Highlander, Gender.Female, true)      => CollectionType.FemaleHighlanderNpc,
            (SubRace.Wildwood, Gender.Female, true)        => CollectionType.FemaleWildwoodNpc,
            (SubRace.Duskwight, Gender.Female, true)       => CollectionType.FemaleDuskwightNpc,
            (SubRace.Plainsfolk, Gender.Female, true)      => CollectionType.FemalePlainsfolkNpc,
            (SubRace.Dunesfolk, Gender.Female, true)       => CollectionType.FemaleDunesfolkNpc,
            (SubRace.SeekerOfTheSun, Gender.Female, true)  => CollectionType.FemaleSeekerOfTheSunNpc,
            (SubRace.KeeperOfTheMoon, Gender.Female, true) => CollectionType.FemaleKeeperOfTheMoonNpc,
            (SubRace.Seawolf, Gender.Female, true)         => CollectionType.FemaleSeawolfNpc,
            (SubRace.Hellsguard, Gender.Female, true)      => CollectionType.FemaleHellsguardNpc,
            (SubRace.Raen, Gender.Female, true)            => CollectionType.FemaleRaenNpc,
            (SubRace.Xaela, Gender.Female, true)           => CollectionType.FemaleXaelaNpc,
            (SubRace.Helion, Gender.Female, true)          => CollectionType.FemaleHelionNpc,
            (SubRace.Lost, Gender.Female, true)            => CollectionType.FemaleLostNpc,
            (SubRace.Rava, Gender.Female, true)            => CollectionType.FemaleRavaNpc,
            (SubRace.Veena, Gender.Female, true)           => CollectionType.FemaleVeenaNpc,
            _                                              => CollectionType.Inactive,
        };
    }

    public static bool TryParse( string text, out CollectionType type )
    {
        if( Enum.TryParse( text, true, out type ) )
            return type is not CollectionType.Inactive and not CollectionType.Temporary;

        if( string.Equals( text, "character", StringComparison.OrdinalIgnoreCase ) )
        {
            type = CollectionType.Individual;
            return true;
        }

        if( string.Equals( text, "base", StringComparison.OrdinalIgnoreCase ) )
        {
            type = CollectionType.Default;
            return true;
        }

        if( string.Equals( text, "ui", StringComparison.OrdinalIgnoreCase ) )
        {
            type = CollectionType.Interface;
            return true;
        }

        if( string.Equals( text, "selected", StringComparison.OrdinalIgnoreCase ) )
        {
            type = CollectionType.Current;
            return true;
        }

        foreach( var t in Enum.GetValues< CollectionType >() )
        {
            if( t is CollectionType.Inactive or CollectionType.Temporary )
                continue;

            if( string.Equals( text, t.ToName(), StringComparison.OrdinalIgnoreCase ) )
            {
                type = t;
                return true;
            }
        }

        return false;
    }

    public static string ToName( this CollectionType collectionType )
        => collectionType switch
        {
            CollectionType.Yourself                 => "Your Character",
            CollectionType.MalePlayerCharacter      => "Male Player Characters",
            CollectionType.MaleNonPlayerCharacter   => "Male Non-Player Characters",
            CollectionType.MaleMidlander            => $"Male {SubRace.Midlander.ToName()}",
            CollectionType.MaleHighlander           => $"Male {SubRace.Highlander.ToName()}",
            CollectionType.MaleWildwood             => $"Male {SubRace.Wildwood.ToName()}",
            CollectionType.MaleDuskwight            => $"Male {SubRace.Duskwight.ToName()}",
            CollectionType.MalePlainsfolk           => $"Male {SubRace.Plainsfolk.ToName()}",
            CollectionType.MaleDunesfolk            => $"Male {SubRace.Dunesfolk.ToName()}",
            CollectionType.MaleSeekerOfTheSun       => $"Male {SubRace.SeekerOfTheSun.ToName()}",
            CollectionType.MaleKeeperOfTheMoon      => $"Male {SubRace.KeeperOfTheMoon.ToName()}",
            CollectionType.MaleSeawolf              => $"Male {SubRace.Seawolf.ToName()}",
            CollectionType.MaleHellsguard           => $"Male {SubRace.Hellsguard.ToName()}",
            CollectionType.MaleRaen                 => $"Male {SubRace.Raen.ToName()}",
            CollectionType.MaleXaela                => $"Male {SubRace.Xaela.ToName()}",
            CollectionType.MaleHelion               => $"Male {SubRace.Helion.ToName()}",
            CollectionType.MaleLost                 => $"Male {SubRace.Lost.ToName()}",
            CollectionType.MaleRava                 => $"Male {SubRace.Rava.ToName()}",
            CollectionType.MaleVeena                => $"Male {SubRace.Veena.ToName()}",
            CollectionType.MaleMidlanderNpc         => $"Male {SubRace.Midlander.ToName()} (NPC)",
            CollectionType.MaleHighlanderNpc        => $"Male {SubRace.Highlander.ToName()} (NPC)",
            CollectionType.MaleWildwoodNpc          => $"Male {SubRace.Wildwood.ToName()} (NPC)",
            CollectionType.MaleDuskwightNpc         => $"Male {SubRace.Duskwight.ToName()} (NPC)",
            CollectionType.MalePlainsfolkNpc        => $"Male {SubRace.Plainsfolk.ToName()} (NPC)",
            CollectionType.MaleDunesfolkNpc         => $"Male {SubRace.Dunesfolk.ToName()} (NPC)",
            CollectionType.MaleSeekerOfTheSunNpc    => $"Male {SubRace.SeekerOfTheSun.ToName()} (NPC)",
            CollectionType.MaleKeeperOfTheMoonNpc   => $"Male {SubRace.KeeperOfTheMoon.ToName()} (NPC)",
            CollectionType.MaleSeawolfNpc           => $"Male {SubRace.Seawolf.ToName()} (NPC)",
            CollectionType.MaleHellsguardNpc        => $"Male {SubRace.Hellsguard.ToName()} (NPC)",
            CollectionType.MaleRaenNpc              => $"Male {SubRace.Raen.ToName()} (NPC)",
            CollectionType.MaleXaelaNpc             => $"Male {SubRace.Xaela.ToName()} (NPC)",
            CollectionType.MaleHelionNpc            => $"Male {SubRace.Helion.ToName()} (NPC)",
            CollectionType.MaleLostNpc              => $"Male {SubRace.Lost.ToName()} (NPC)",
            CollectionType.MaleRavaNpc              => $"Male {SubRace.Rava.ToName()} (NPC)",
            CollectionType.MaleVeenaNpc             => $"Male {SubRace.Veena.ToName()} (NPC)",
            CollectionType.FemalePlayerCharacter    => "Female Player Characters",
            CollectionType.FemaleNonPlayerCharacter => "Female Non-Player Characters",
            CollectionType.FemaleMidlander          => $"Female {SubRace.Midlander.ToName()}",
            CollectionType.FemaleHighlander         => $"Female {SubRace.Highlander.ToName()}",
            CollectionType.FemaleWildwood           => $"Female {SubRace.Wildwood.ToName()}",
            CollectionType.FemaleDuskwight          => $"Female {SubRace.Duskwight.ToName()}",
            CollectionType.FemalePlainsfolk         => $"Female {SubRace.Plainsfolk.ToName()}",
            CollectionType.FemaleDunesfolk          => $"Female {SubRace.Dunesfolk.ToName()}",
            CollectionType.FemaleSeekerOfTheSun     => $"Female {SubRace.SeekerOfTheSun.ToName()}",
            CollectionType.FemaleKeeperOfTheMoon    => $"Female {SubRace.KeeperOfTheMoon.ToName()}",
            CollectionType.FemaleSeawolf            => $"Female {SubRace.Seawolf.ToName()}",
            CollectionType.FemaleHellsguard         => $"Female {SubRace.Hellsguard.ToName()}",
            CollectionType.FemaleRaen               => $"Female {SubRace.Raen.ToName()}",
            CollectionType.FemaleXaela              => $"Female {SubRace.Xaela.ToName()}",
            CollectionType.FemaleHelion             => $"Female {SubRace.Helion.ToName()}",
            CollectionType.FemaleLost               => $"Female {SubRace.Lost.ToName()}",
            CollectionType.FemaleRava               => $"Female {SubRace.Rava.ToName()}",
            CollectionType.FemaleVeena              => $"Female {SubRace.Veena.ToName()}",
            CollectionType.FemaleMidlanderNpc       => $"Female {SubRace.Midlander.ToName()} (NPC)",
            CollectionType.FemaleHighlanderNpc      => $"Female {SubRace.Highlander.ToName()} (NPC)",
            CollectionType.FemaleWildwoodNpc        => $"Female {SubRace.Wildwood.ToName()} (NPC)",
            CollectionType.FemaleDuskwightNpc       => $"Female {SubRace.Duskwight.ToName()} (NPC)",
            CollectionType.FemalePlainsfolkNpc      => $"Female {SubRace.Plainsfolk.ToName()} (NPC)",
            CollectionType.FemaleDunesfolkNpc       => $"Female {SubRace.Dunesfolk.ToName()} (NPC)",
            CollectionType.FemaleSeekerOfTheSunNpc  => $"Female {SubRace.SeekerOfTheSun.ToName()} (NPC)",
            CollectionType.FemaleKeeperOfTheMoonNpc => $"Female {SubRace.KeeperOfTheMoon.ToName()} (NPC)",
            CollectionType.FemaleSeawolfNpc         => $"Female {SubRace.Seawolf.ToName()} (NPC)",
            CollectionType.FemaleHellsguardNpc      => $"Female {SubRace.Hellsguard.ToName()} (NPC)",
            CollectionType.FemaleRaenNpc            => $"Female {SubRace.Raen.ToName()} (NPC)",
            CollectionType.FemaleXaelaNpc           => $"Female {SubRace.Xaela.ToName()} (NPC)",
            CollectionType.FemaleHelionNpc          => $"Female {SubRace.Helion.ToName()} (NPC)",
            CollectionType.FemaleLostNpc            => $"Female {SubRace.Lost.ToName()} (NPC)",
            CollectionType.FemaleRavaNpc            => $"Female {SubRace.Rava.ToName()} (NPC)",
            CollectionType.FemaleVeenaNpc           => $"Female {SubRace.Veena.ToName()} (NPC)",
            CollectionType.Inactive                 => "Collection",
            CollectionType.Default                  => "Default",
            CollectionType.Interface                => "Interface",
            CollectionType.Individual               => "Individual",
            CollectionType.Current                  => "Current",
            _                                       => string.Empty,
        };

    public static string ToDescription( this CollectionType collectionType )
        => collectionType switch
        {
            CollectionType.Yourself => "This collection applies to your own character, regardless of its name.\n"
              + "It takes precedence before all other collections except for explicitly named character collections.",
            CollectionType.MalePlayerCharacter =>
                "This collection applies to all male player characters that do not have a more specific character or racial collections associated.",
            CollectionType.MaleNonPlayerCharacter =>
                "This collection applies to all human male non-player characters except those explicitly named. It takes precedence before the default and racial collections.",
            CollectionType.MaleMidlander =>
                "This collection applies to all male player character Midlander Hyur that do not have a more specific character collection associated.",
            CollectionType.MaleHighlander =>
                "This collection applies to all male player character Highlander Hyur that do not have a more specific character collection associated.",
            CollectionType.MaleWildwood =>
                "This collection applies to all male player character Wildwood Elezen that do not have a more specific character collection associated.",
            CollectionType.MaleDuskwight =>
                "This collection applies to all male player character Duskwight Elezen that do not have a more specific character collection associated.",
            CollectionType.MalePlainsfolk =>
                "This collection applies to all male player character Plainsfolk Lalafell that do not have a more specific character collection associated.",
            CollectionType.MaleDunesfolk =>
                "This collection applies to all male player character Dunesfolk Lalafell that do not have a more specific character collection associated.",
            CollectionType.MaleSeekerOfTheSun =>
                "This collection applies to all male player character Seekers of the Sun that do not have a more specific character collection associated.",
            CollectionType.MaleKeeperOfTheMoon =>
                "This collection applies to all male player character Keepers of the Moon that do not have a more specific character collection associated.",
            CollectionType.MaleSeawolf =>
                "This collection applies to all male player character Sea Wolf Roegadyn that do not have a more specific character collection associated.",
            CollectionType.MaleHellsguard =>
                "This collection applies to all male player character Hellsguard Roegadyn that do not have a more specific character collection associated.",
            CollectionType.MaleRaen =>
                "This collection applies to all male player character Raen Au Ra that do not have a more specific character collection associated.",
            CollectionType.MaleXaela =>
                "This collection applies to all male player character Xaela Au Ra that do not have a more specific character collection associated.",
            CollectionType.MaleHelion =>
                "This collection applies to all male player character Helion Hrothgar that do not have a more specific character collection associated.",
            CollectionType.MaleLost =>
                "This collection applies to all male player character Lost Hrothgar that do not have a more specific character collection associated.",
            CollectionType.MaleRava =>
                "This collection applies to all male player character Rava Viera that do not have a more specific character collection associated.",
            CollectionType.MaleVeena =>
                "This collection applies to all male player character Veena Viera that do not have a more specific character collection associated.",
            CollectionType.MaleMidlanderNpc =>
                "This collection applies to all male non-player character Midlander Hyur that do not have a more specific character collection associated.",
            CollectionType.MaleHighlanderNpc =>
                "This collection applies to all male non-player character Highlander Hyur that do not have a more specific character collection associated.",
            CollectionType.MaleWildwoodNpc =>
                "This collection applies to all male non-player character Wildwood Elezen that do not have a more specific character collection associated.",
            CollectionType.MaleDuskwightNpc =>
                "This collection applies to all male non-player character Duskwight Elezen that do not have a more specific character collection associated.",
            CollectionType.MalePlainsfolkNpc =>
                "This collection applies to all male non-player character Plainsfolk Lalafell that do not have a more specific character collection associated.",
            CollectionType.MaleDunesfolkNpc =>
                "This collection applies to all male non-player character Dunesfolk Lalafell that do not have a more specific character collection associated.",
            CollectionType.MaleSeekerOfTheSunNpc =>
                "This collection applies to all male non-player character Seekers of the Sun that do not have a more specific character collection associated.",
            CollectionType.MaleKeeperOfTheMoonNpc =>
                "This collection applies to all male non-player character Keepers of the Moon that do not have a more specific character collection associated.",
            CollectionType.MaleSeawolfNpc =>
                "This collection applies to all male non-player character Sea Wolf Roegadyn that do not have a more specific character collection associated.",
            CollectionType.MaleHellsguardNpc =>
                "This collection applies to all male non-player character Hellsguard Roegadyn that do not have a more specific character collection associated.",
            CollectionType.MaleRaenNpc =>
                "This collection applies to all male non-player character Raen Au Ra that do not have a more specific character collection associated.",
            CollectionType.MaleXaelaNpc =>
                "This collection applies to all male non-player character Xaela Au Ra that do not have a more specific character collection associated.",
            CollectionType.MaleHelionNpc =>
                "This collection applies to all male non-player character Helion Hrothgar that do not have a more specific character collection associated.",
            CollectionType.MaleLostNpc =>
                "This collection applies to all male non-player character Lost Hrothgar that do not have a more specific character collection associated.",
            CollectionType.MaleRavaNpc =>
                "This collection applies to all male non-player character Rava Viera that do not have a more specific character collection associated.",
            CollectionType.MaleVeenaNpc =>
                "This collection applies to all male non-player character Veena Viera that do not have a more specific character collection associated.",
            CollectionType.FemalePlayerCharacter =>
                "This collection applies to all female player characters that do not have a more specific character or racial collections associated.",
            CollectionType.FemaleNonPlayerCharacter =>
                "This collection applies to all human female non-player characters except those explicitly named. It takes precedence before the default and racial collections.",
            CollectionType.FemaleMidlander =>
                "This collection applies to all female player character Midlander Hyur that do not have a more specific character collection associated.",
            CollectionType.FemaleHighlander =>
                "This collection applies to all female player character Highlander Hyur that do not have a more specific character collection associated.",
            CollectionType.FemaleWildwood =>
                "This collection applies to all female player character Wildwood Elezen that do not have a more specific character collection associated.",
            CollectionType.FemaleDuskwight =>
                "This collection applies to all female player character Duskwight Elezen that do not have a more specific character collection associated.",
            CollectionType.FemalePlainsfolk =>
                "This collection applies to all female player character Plainsfolk Lalafell that do not have a more specific character collection associated.",
            CollectionType.FemaleDunesfolk =>
                "This collection applies to all female player character Dunesfolk Lalafell that do not have a more specific character collection associated.",
            CollectionType.FemaleSeekerOfTheSun =>
                "This collection applies to all female player character Seekers of the Sun that do not have a more specific character collection associated.",
            CollectionType.FemaleKeeperOfTheMoon =>
                "This collection applies to all female player character Keepers of the Moon that do not have a more specific character collection associated.",
            CollectionType.FemaleSeawolf =>
                "This collection applies to all female player character Sea Wolf Roegadyn that do not have a more specific character collection associated.",
            CollectionType.FemaleHellsguard =>
                "This collection applies to all female player character Hellsguard Roegadyn that do not have a more specific character collection associated.",
            CollectionType.FemaleRaen =>
                "This collection applies to all female player character Raen Au Ra that do not have a more specific character collection associated.",
            CollectionType.FemaleXaela =>
                "This collection applies to all female player character Xaela Au Ra that do not have a more specific character collection associated.",
            CollectionType.FemaleHelion =>
                "This collection applies to all female player character Helion Hrothgar that do not have a more specific character collection associated.",
            CollectionType.FemaleLost =>
                "This collection applies to all female player character Lost Hrothgar that do not have a more specific character collection associated.",
            CollectionType.FemaleRava =>
                "This collection applies to all female player character Rava Viera that do not have a more specific character collection associated.",
            CollectionType.FemaleVeena =>
                "This collection applies to all female player character Veena Viera that do not have a more specific character collection associated.",
            CollectionType.FemaleMidlanderNpc =>
                "This collection applies to all female non-player character Midlander Hyur that do not have a more specific character collection associated.",
            CollectionType.FemaleHighlanderNpc =>
                "This collection applies to all female non-player character Highlander Hyur that do not have a more specific character collection associated.",
            CollectionType.FemaleWildwoodNpc =>
                "This collection applies to all female non-player character Wildwood Elezen that do not have a more specific character collection associated.",
            CollectionType.FemaleDuskwightNpc =>
                "This collection applies to all female non-player character Duskwight Elezen that do not have a more specific character collection associated.",
            CollectionType.FemalePlainsfolkNpc =>
                "This collection applies to all female non-player character Plainsfolk Lalafell that do not have a more specific character collection associated.",
            CollectionType.FemaleDunesfolkNpc =>
                "This collection applies to all female non-player character Dunesfolk Lalafell that do not have a more specific character collection associated.",
            CollectionType.FemaleSeekerOfTheSunNpc =>
                "This collection applies to all female non-player character Seekers of the Sun that do not have a more specific character collection associated.",
            CollectionType.FemaleKeeperOfTheMoonNpc =>
                "This collection applies to all female non-player character Keepers of the Moon that do not have a more specific character collection associated.",
            CollectionType.FemaleSeawolfNpc =>
                "This collection applies to all female non-player character Sea Wolf Roegadyn that do not have a more specific character collection associated.",
            CollectionType.FemaleHellsguardNpc =>
                "This collection applies to all female non-player character Hellsguard Roegadyn that do not have a more specific character collection associated.",
            CollectionType.FemaleRaenNpc =>
                "This collection applies to all female non-player character Raen Au Ra that do not have a more specific character collection associated.",
            CollectionType.FemaleXaelaNpc =>
                "This collection applies to all female non-player character Xaela Au Ra that do not have a more specific character collection associated.",
            CollectionType.FemaleHelionNpc =>
                "This collection applies to all female non-player character Helion Hrothgar that do not have a more specific character collection associated.",
            CollectionType.FemaleLostNpc =>
                "This collection applies to all female non-player character Lost Hrothgar that do not have a more specific character collection associated.",
            CollectionType.FemaleRavaNpc =>
                "This collection applies to all female non-player character Rava Viera that do not have a more specific character collection associated.",
            CollectionType.FemaleVeenaNpc =>
                "This collection applies to all female non-player character Veena Viera that do not have a more specific character collection associated.",
            _ => string.Empty,
        };
}