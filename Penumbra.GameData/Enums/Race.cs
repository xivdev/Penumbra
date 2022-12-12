using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using static Penumbra.GameData.Enums.GenderRace;

namespace Penumbra.GameData.Enums;

public enum Race : byte
{
    Unknown,
    Hyur,
    Elezen,
    Lalafell,
    Miqote,
    Roegadyn,
    AuRa,
    Hrothgar,
    Viera,
}

public enum Gender : byte
{
    Unknown,
    Male,
    Female,
    MaleNpc,
    FemaleNpc,
}

public enum ModelRace : byte
{
    Unknown,
    Midlander,
    Highlander,
    Elezen,
    Lalafell,
    Miqote,
    Roegadyn,
    AuRa,
    Hrothgar,
    Viera,
}

public enum SubRace : byte
{
    Unknown,
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
}

// The combined gender-race-npc numerical code as used by the game.
public enum GenderRace : ushort
{
    Unknown             = 0,
    MidlanderMale       = 0101,
    MidlanderMaleNpc    = 0104,
    MidlanderFemale     = 0201,
    MidlanderFemaleNpc  = 0204,
    HighlanderMale      = 0301,
    HighlanderMaleNpc   = 0304,
    HighlanderFemale    = 0401,
    HighlanderFemaleNpc = 0404,
    ElezenMale          = 0501,
    ElezenMaleNpc       = 0504,
    ElezenFemale        = 0601,
    ElezenFemaleNpc     = 0604,
    MiqoteMale          = 0701,
    MiqoteMaleNpc       = 0704,
    MiqoteFemale        = 0801,
    MiqoteFemaleNpc     = 0804,
    RoegadynMale        = 0901,
    RoegadynMaleNpc     = 0904,
    RoegadynFemale      = 1001,
    RoegadynFemaleNpc   = 1004,
    LalafellMale        = 1101,
    LalafellMaleNpc     = 1104,
    LalafellFemale      = 1201,
    LalafellFemaleNpc   = 1204,
    AuRaMale            = 1301,
    AuRaMaleNpc         = 1304,
    AuRaFemale          = 1401,
    AuRaFemaleNpc       = 1404,
    HrothgarMale        = 1501,
    HrothgarMaleNpc     = 1504,
    HrothgarFemale      = 1601,
    HrothgarFemaleNpc   = 1604,
    VieraMale           = 1701,
    VieraMaleNpc        = 1704,
    VieraFemale         = 1801,
    VieraFemaleNpc      = 1804,
    UnknownMaleNpc      = 9104,
    UnknownFemaleNpc    = 9204,
}

public static class RaceEnumExtensions
{
    public static Race ToRace(this ModelRace race)
    {
        return race switch
        {
            ModelRace.Unknown    => Race.Unknown,
            ModelRace.Midlander  => Race.Hyur,
            ModelRace.Highlander => Race.Hyur,
            ModelRace.Elezen     => Race.Elezen,
            ModelRace.Lalafell   => Race.Lalafell,
            ModelRace.Miqote     => Race.Miqote,
            ModelRace.Roegadyn   => Race.Roegadyn,
            ModelRace.AuRa       => Race.AuRa,
            ModelRace.Hrothgar   => Race.Hrothgar,
            ModelRace.Viera      => Race.Viera,
            _                    => throw new ArgumentOutOfRangeException(nameof(race), race, null),
        };
    }

    public static Race ToRace(this SubRace subRace)
    {
        return subRace switch
        {
            SubRace.Unknown         => Race.Unknown,
            SubRace.Midlander       => Race.Hyur,
            SubRace.Highlander      => Race.Hyur,
            SubRace.Wildwood        => Race.Elezen,
            SubRace.Duskwight       => Race.Elezen,
            SubRace.Plainsfolk      => Race.Lalafell,
            SubRace.Dunesfolk       => Race.Lalafell,
            SubRace.SeekerOfTheSun  => Race.Miqote,
            SubRace.KeeperOfTheMoon => Race.Miqote,
            SubRace.Seawolf         => Race.Roegadyn,
            SubRace.Hellsguard      => Race.Roegadyn,
            SubRace.Raen            => Race.AuRa,
            SubRace.Xaela           => Race.AuRa,
            SubRace.Helion          => Race.Hrothgar,
            SubRace.Lost            => Race.Hrothgar,
            SubRace.Rava            => Race.Viera,
            SubRace.Veena           => Race.Viera,
            _                       => throw new ArgumentOutOfRangeException(nameof(subRace), subRace, null),
        };
    }

    public static string ToName(this ModelRace modelRace)
    {
        return modelRace switch
        {
            ModelRace.Midlander  => SubRace.Midlander.ToName(),
            ModelRace.Highlander => SubRace.Highlander.ToName(),
            ModelRace.Elezen     => Race.Elezen.ToName(),
            ModelRace.Lalafell   => Race.Lalafell.ToName(),
            ModelRace.Miqote     => Race.Miqote.ToName(),
            ModelRace.Roegadyn   => Race.Roegadyn.ToName(),
            ModelRace.AuRa       => Race.AuRa.ToName(),
            ModelRace.Hrothgar   => Race.Hrothgar.ToName(),
            ModelRace.Viera      => Race.Viera.ToName(),
            _                    => Race.Unknown.ToName(),
        };
    }

    public static string ToName(this Race race)
    {
        return race switch
        {
            Race.Hyur     => "Hyur",
            Race.Elezen   => "Elezen",
            Race.Lalafell => "Lalafell",
            Race.Miqote   => "Miqo'te",
            Race.Roegadyn => "Roegadyn",
            Race.AuRa     => "Au Ra",
            Race.Hrothgar => "Hrothgar",
            Race.Viera    => "Viera",
            _             => "Unknown",
        };
    }

    public static string ToName(this Gender gender)
    {
        return gender switch
        {
            Gender.Male      => "Male",
            Gender.Female    => "Female",
            Gender.MaleNpc   => "Male (NPC)",
            Gender.FemaleNpc => "Female (NPC)",
            _                => "Unknown",
        };
    }

    public static string ToName(this SubRace subRace)
    {
        return subRace switch
        {
            SubRace.Midlander       => "Midlander",
            SubRace.Highlander      => "Highlander",
            SubRace.Wildwood        => "Wildwood",
            SubRace.Duskwight       => "Duskwight",
            SubRace.Plainsfolk      => "Plainsfolk",
            SubRace.Dunesfolk       => "Dunesfolk",
            SubRace.SeekerOfTheSun  => "Seeker Of The Sun",
            SubRace.KeeperOfTheMoon => "Keeper Of The Moon",
            SubRace.Seawolf         => "Seawolf",
            SubRace.Hellsguard      => "Hellsguard",
            SubRace.Raen            => "Raen",
            SubRace.Xaela           => "Xaela",
            SubRace.Helion          => "Hellion",
            SubRace.Lost            => "Lost",
            SubRace.Rava            => "Rava",
            SubRace.Veena           => "Veena",
            _                       => "Unknown",
        };
    }

    public static bool FitsRace(this SubRace subRace, Race race)
        => subRace.ToRace() == race;

    public static byte ToByte(this Gender gender, ModelRace modelRace)
        => (byte)((int)gender | ((int)modelRace << 3));

    public static byte ToByte(this ModelRace modelRace, Gender gender)
        => gender.ToByte(modelRace);

    public static byte ToByte(this GenderRace value)
    {
        var (gender, race) = value.Split();
        return gender.ToByte(race);
    }

    public static (Gender, ModelRace) Split(this GenderRace value)
    {
        return value switch
        {
            Unknown             => (Gender.Unknown, ModelRace.Unknown),
            MidlanderMale       => (Gender.Male, ModelRace.Midlander),
            MidlanderMaleNpc    => (Gender.MaleNpc, ModelRace.Midlander),
            MidlanderFemale     => (Gender.Female, ModelRace.Midlander),
            MidlanderFemaleNpc  => (Gender.FemaleNpc, ModelRace.Midlander),
            HighlanderMale      => (Gender.Male, ModelRace.Highlander),
            HighlanderMaleNpc   => (Gender.MaleNpc, ModelRace.Highlander),
            HighlanderFemale    => (Gender.Female, ModelRace.Highlander),
            HighlanderFemaleNpc => (Gender.FemaleNpc, ModelRace.Highlander),
            ElezenMale          => (Gender.Male, ModelRace.Elezen),
            ElezenMaleNpc       => (Gender.MaleNpc, ModelRace.Elezen),
            ElezenFemale        => (Gender.Female, ModelRace.Elezen),
            ElezenFemaleNpc     => (Gender.FemaleNpc, ModelRace.Elezen),
            LalafellMale        => (Gender.Male, ModelRace.Lalafell),
            LalafellMaleNpc     => (Gender.MaleNpc, ModelRace.Lalafell),
            LalafellFemale      => (Gender.Female, ModelRace.Lalafell),
            LalafellFemaleNpc   => (Gender.FemaleNpc, ModelRace.Lalafell),
            MiqoteMale          => (Gender.Male, ModelRace.Miqote),
            MiqoteMaleNpc       => (Gender.MaleNpc, ModelRace.Miqote),
            MiqoteFemale        => (Gender.Female, ModelRace.Miqote),
            MiqoteFemaleNpc     => (Gender.FemaleNpc, ModelRace.Miqote),
            RoegadynMale        => (Gender.Male, ModelRace.Roegadyn),
            RoegadynMaleNpc     => (Gender.MaleNpc, ModelRace.Roegadyn),
            RoegadynFemale      => (Gender.Female, ModelRace.Roegadyn),
            RoegadynFemaleNpc   => (Gender.FemaleNpc, ModelRace.Roegadyn),
            AuRaMale            => (Gender.Male, ModelRace.AuRa),
            AuRaMaleNpc         => (Gender.MaleNpc, ModelRace.AuRa),
            AuRaFemale          => (Gender.Female, ModelRace.AuRa),
            AuRaFemaleNpc       => (Gender.FemaleNpc, ModelRace.AuRa),
            HrothgarMale        => (Gender.Male, ModelRace.Hrothgar),
            HrothgarMaleNpc     => (Gender.MaleNpc, ModelRace.Hrothgar),
            HrothgarFemale      => (Gender.Female, ModelRace.Hrothgar),
            HrothgarFemaleNpc   => (Gender.FemaleNpc, ModelRace.Hrothgar),
            VieraMale           => (Gender.Male, ModelRace.Viera),
            VieraMaleNpc        => (Gender.Male, ModelRace.Viera),
            VieraFemale         => (Gender.Female, ModelRace.Viera),
            VieraFemaleNpc      => (Gender.FemaleNpc, ModelRace.Viera),
            UnknownMaleNpc      => (Gender.MaleNpc, ModelRace.Unknown),
            UnknownFemaleNpc    => (Gender.FemaleNpc, ModelRace.Unknown),
            _                   => throw new InvalidEnumArgumentException(),
        };
    }

    public static bool IsValid(this GenderRace value)
        => value != Unknown && Enum.IsDefined(typeof(GenderRace), value);

    public static string ToRaceCode(this GenderRace value)
    {
        return value switch
        {
            MidlanderMale       => "0101",
            MidlanderMaleNpc    => "0104",
            MidlanderFemale     => "0201",
            MidlanderFemaleNpc  => "0204",
            HighlanderMale      => "0301",
            HighlanderMaleNpc   => "0304",
            HighlanderFemale    => "0401",
            HighlanderFemaleNpc => "0404",
            ElezenMale          => "0501",
            ElezenMaleNpc       => "0504",
            ElezenFemale        => "0601",
            ElezenFemaleNpc     => "0604",
            MiqoteMale          => "0701",
            MiqoteMaleNpc       => "0704",
            MiqoteFemale        => "0801",
            MiqoteFemaleNpc     => "0804",
            RoegadynMale        => "0901",
            RoegadynMaleNpc     => "0904",
            RoegadynFemale      => "1001",
            RoegadynFemaleNpc   => "1004",
            LalafellMale        => "1101",
            LalafellMaleNpc     => "1104",
            LalafellFemale      => "1201",
            LalafellFemaleNpc   => "1204",
            AuRaMale            => "1301",
            AuRaMaleNpc         => "1304",
            AuRaFemale          => "1401",
            AuRaFemaleNpc       => "1404",
            HrothgarMale        => "1501",
            HrothgarMaleNpc     => "1504",
            HrothgarFemale      => "1601",
            HrothgarFemaleNpc   => "1604",
            VieraMale           => "1701",
            VieraMaleNpc        => "1704",
            VieraFemale         => "1801",
            VieraFemaleNpc      => "1804",
            UnknownMaleNpc      => "9104",
            UnknownFemaleNpc    => "9204",
            _                   => string.Empty,
        };
    }

    public static GenderRace[] Dependencies(this GenderRace raceCode)
        => DependencyList.TryGetValue(raceCode, out var dep) ? dep : Array.Empty<GenderRace>();

    public static IEnumerable<GenderRace> OnlyDependencies(this GenderRace raceCode)
        => DependencyList.TryGetValue(raceCode, out var dep) ? dep.Skip(1) : Array.Empty<GenderRace>();

    private static readonly Dictionary<GenderRace, GenderRace[]> DependencyList = new()
    {
        // @formatter:off
        [MidlanderMale]       = new[]{ MidlanderMale                                                                                                                                       },
        [HighlanderMale]      = new[]{ HighlanderMale,      MidlanderMale                                                                                                                  },
        [ElezenMale]          = new[]{ ElezenMale,          MidlanderMale                                                                                                                  },
        [MiqoteMale]          = new[]{ MiqoteMale,          MidlanderMale                                                                                                                  },
        [RoegadynMale]        = new[]{ RoegadynMale,        MidlanderMale                                                                                                                  },
        [LalafellMale]        = new[]{ LalafellMale,        MidlanderMale                                                                                                                  },
        [AuRaMale]            = new[]{ AuRaMale,            MidlanderMale                                                                                                                  },
        [HrothgarMale]        = new[]{ HrothgarMale,        RoegadynMale,       MidlanderMale                                                                                              },
        [VieraMale]           = new[]{ VieraMale,           MidlanderMale                                                                                                                  },
        [MidlanderFemale]     = new[]{ MidlanderFemale,     MidlanderMale                                                                                                                  },
        [HighlanderFemale]    = new[]{ HighlanderFemale,    MidlanderFemale,    MidlanderMale                                                                                              },
        [ElezenFemale]        = new[]{ ElezenFemale,        MidlanderFemale,    MidlanderMale                                                                                              },
        [MiqoteFemale]        = new[]{ MiqoteFemale,        MidlanderFemale,    MidlanderMale                                                                                              },
        [RoegadynFemale]      = new[]{ RoegadynFemale,      MidlanderFemale,    MidlanderMale                                                                                              },
        [LalafellFemale]      = new[]{ LalafellFemale,      LalafellMale,       MidlanderMale                                                                                              },
        [AuRaFemale]          = new[]{ AuRaFemale,          MidlanderFemale,    MidlanderMale                                                                                              },
        [HrothgarFemale]      = new[]{ HrothgarFemale,      RoegadynFemale,     MidlanderFemale,    MidlanderMale                                                                          },
        [VieraFemale]         = new[]{ VieraFemale,         MidlanderFemale,    MidlanderMale                                                                                              },
        [MidlanderMaleNpc]    = new[]{ MidlanderMaleNpc,    MidlanderMale                                                                                                                  },
        [HighlanderMaleNpc]   = new[]{ HighlanderMaleNpc,   HighlanderMale,     MidlanderMaleNpc,   MidlanderMale                                                                          },
        [ElezenMaleNpc]       = new[]{ ElezenMaleNpc,       ElezenMale,         MidlanderMaleNpc,   MidlanderMale                                                                          },
        [MiqoteMaleNpc]       = new[]{ MiqoteMaleNpc,       MiqoteMale,         MidlanderMaleNpc,   MidlanderMale                                                                          },
        [RoegadynMaleNpc]     = new[]{ RoegadynMaleNpc,     RoegadynMale,       MidlanderMaleNpc,   MidlanderMale                                                                          },
        [LalafellMaleNpc]     = new[]{ LalafellMaleNpc,     LalafellMale,       MidlanderMaleNpc,   MidlanderMale                                                                          },
        [AuRaMaleNpc]         = new[]{ AuRaMaleNpc,         AuRaMale,           MidlanderMaleNpc,   MidlanderMale                                                                          },
        [HrothgarMaleNpc]     = new[]{ HrothgarMaleNpc,     HrothgarMale,       RoegadynMaleNpc,    RoegadynMale,     MidlanderMaleNpc,  MidlanderMale                                     },
        [VieraMaleNpc]        = new[]{ VieraMaleNpc,        VieraMale,          MidlanderMaleNpc,   MidlanderMale                                                                          },
        [MidlanderFemaleNpc]  = new[]{ MidlanderFemaleNpc,  MidlanderFemale,    MidlanderMaleNpc,   MidlanderMale                                                                          },
        [HighlanderFemaleNpc] = new[]{ HighlanderFemaleNpc, HighlanderFemale,   MidlanderFemaleNpc, MidlanderFemale,  MidlanderMaleNpc,   MidlanderMale                                    },
        [ElezenFemaleNpc]     = new[]{ ElezenFemaleNpc,     ElezenFemale,       MidlanderFemaleNpc, MidlanderFemale,  MidlanderMaleNpc,   MidlanderMale                                    },
        [MiqoteFemaleNpc]     = new[]{ MiqoteFemaleNpc,     MiqoteFemale,       MidlanderFemaleNpc, MidlanderFemale,  MidlanderMaleNpc,   MidlanderMale                                    },
        [RoegadynFemaleNpc]   = new[]{ RoegadynFemaleNpc,   RoegadynFemale,     MidlanderFemaleNpc, MidlanderFemale,  MidlanderMaleNpc,   MidlanderMale                                    },
        [LalafellFemaleNpc]   = new[]{ LalafellFemaleNpc,   LalafellFemale,     LalafellMaleNpc,    LalafellMale,     MidlanderMaleNpc,   MidlanderMale                                    },
        [AuRaFemaleNpc]       = new[]{ AuRaFemaleNpc,       AuRaFemale,         MidlanderFemaleNpc, MidlanderFemale,  MidlanderMaleNpc,   MidlanderMale                                    },
        [HrothgarFemaleNpc]   = new[]{ HrothgarFemaleNpc,   HrothgarFemale,     RoegadynFemaleNpc,  RoegadynFemale,   MidlanderFemaleNpc, MidlanderFemale, MidlanderMaleNpc, MidlanderMale },
        [VieraFemaleNpc]      = new[]{ VieraFemaleNpc,      VieraFemale,        MidlanderFemaleNpc, MidlanderFemale,  MidlanderMaleNpc,   MidlanderMale                                    },
        [UnknownMaleNpc]      = new[]{ UnknownMaleNpc,      MidlanderMaleNpc,   MidlanderMale                                                                                              },
        [UnknownFemaleNpc]    = new[]{ UnknownFemaleNpc,    MidlanderFemaleNpc, MidlanderFemale,    MidlanderMaleNpc, MidlanderMale                                                        },
        // @formatter:on
    };
}

public static partial class Names
{
    public static GenderRace GenderRaceFromCode(string code)
    {
        return code switch
        {
            "0101" => MidlanderMale,
            "0104" => MidlanderMaleNpc,
            "0201" => MidlanderFemale,
            "0204" => MidlanderFemaleNpc,
            "0301" => HighlanderMale,
            "0304" => HighlanderMaleNpc,
            "0401" => HighlanderFemale,
            "0404" => HighlanderFemaleNpc,
            "0501" => ElezenMale,
            "0504" => ElezenMaleNpc,
            "0601" => ElezenFemale,
            "0604" => ElezenFemaleNpc,
            "0701" => MiqoteMale,
            "0704" => MiqoteMaleNpc,
            "0801" => MiqoteFemale,
            "0804" => MiqoteFemaleNpc,
            "0901" => RoegadynMale,
            "0904" => RoegadynMaleNpc,
            "1001" => RoegadynFemale,
            "1004" => RoegadynFemaleNpc,
            "1101" => LalafellMale,
            "1104" => LalafellMaleNpc,
            "1201" => LalafellFemale,
            "1204" => LalafellFemaleNpc,
            "1301" => AuRaMale,
            "1304" => AuRaMaleNpc,
            "1401" => AuRaFemale,
            "1404" => AuRaFemaleNpc,
            "1501" => HrothgarMale,
            "1504" => HrothgarMaleNpc,
            "1601" => HrothgarFemale,
            "1604" => HrothgarFemaleNpc,
            "1701" => VieraMale,
            "1704" => VieraMaleNpc,
            "1801" => VieraFemale,
            "1804" => VieraFemaleNpc,
            "9104" => UnknownMaleNpc,
            "9204" => UnknownFemaleNpc,
            _      => Unknown,
        };
    }

    public static GenderRace GenderRaceFromByte(byte value)
    {
        var gender = (Gender)(value & 0b111);
        var race   = (ModelRace)(value >> 3);
        return CombinedRace(gender, race);
    }

    public static GenderRace CombinedRace(Gender gender, ModelRace modelRace)
    {
        return gender switch
        {
            Gender.Male => modelRace switch
            {
                ModelRace.Midlander  => MidlanderMale,
                ModelRace.Highlander => HighlanderMale,
                ModelRace.Elezen     => ElezenMale,
                ModelRace.Lalafell   => LalafellMale,
                ModelRace.Miqote     => MiqoteMale,
                ModelRace.Roegadyn   => RoegadynMale,
                ModelRace.AuRa       => AuRaMale,
                ModelRace.Hrothgar   => HrothgarMale,
                ModelRace.Viera      => VieraMale,
                _                    => Unknown,
            },
            Gender.MaleNpc => modelRace switch
            {
                ModelRace.Midlander  => MidlanderMaleNpc,
                ModelRace.Highlander => HighlanderMaleNpc,
                ModelRace.Elezen     => ElezenMaleNpc,
                ModelRace.Lalafell   => LalafellMaleNpc,
                ModelRace.Miqote     => MiqoteMaleNpc,
                ModelRace.Roegadyn   => RoegadynMaleNpc,
                ModelRace.AuRa       => AuRaMaleNpc,
                ModelRace.Hrothgar   => HrothgarMaleNpc,
                ModelRace.Viera      => VieraMaleNpc,
                _                    => Unknown,
            },
            Gender.Female => modelRace switch
            {
                ModelRace.Midlander  => MidlanderFemale,
                ModelRace.Highlander => HighlanderFemale,
                ModelRace.Elezen     => ElezenFemale,
                ModelRace.Lalafell   => LalafellFemale,
                ModelRace.Miqote     => MiqoteFemale,
                ModelRace.Roegadyn   => RoegadynFemale,
                ModelRace.AuRa       => AuRaFemale,
                ModelRace.Hrothgar   => HrothgarFemale,
                ModelRace.Viera      => VieraFemale,
                _                    => Unknown,
            },
            Gender.FemaleNpc => modelRace switch
            {
                ModelRace.Midlander  => MidlanderFemaleNpc,
                ModelRace.Highlander => HighlanderFemaleNpc,
                ModelRace.Elezen     => ElezenFemaleNpc,
                ModelRace.Lalafell   => LalafellFemaleNpc,
                ModelRace.Miqote     => MiqoteFemaleNpc,
                ModelRace.Roegadyn   => RoegadynFemaleNpc,
                ModelRace.AuRa       => AuRaFemaleNpc,
                ModelRace.Hrothgar   => HrothgarFemaleNpc,
                ModelRace.Viera      => VieraFemaleNpc,
                _                    => Unknown,
            },
            _ => Unknown,
        };
    }
}
