using System;
using System.Collections.Generic;
using System.ComponentModel;

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
    public static Race ToRace( this ModelRace race )
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
            _                    => throw new ArgumentOutOfRangeException( nameof( race ), race, null ),
        };
    }

    public static Race ToRace( this SubRace subRace )
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
            _                       => throw new ArgumentOutOfRangeException( nameof( subRace ), subRace, null ),
        };
    }

    public static string ToName( this ModelRace modelRace )
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

    public static string ToName( this Race race )
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

    public static string ToName( this Gender gender )
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

    public static string ToName( this SubRace subRace )
    {
        return subRace switch
        {
            SubRace.Midlander       => "Midlander",
            SubRace.Highlander      => "Highlander",
            SubRace.Wildwood        => "Wildwood",
            SubRace.Duskwight       => "Duskwright",
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

    public static bool FitsRace( this SubRace subRace, Race race )
        => subRace.ToRace() == race;

    public static byte ToByte( this Gender gender, ModelRace modelRace )
        => ( byte )( ( int )gender | ( ( int )modelRace << 3 ) );

    public static byte ToByte( this ModelRace modelRace, Gender gender )
        => gender.ToByte( modelRace );

    public static byte ToByte( this GenderRace value )
    {
        var (gender, race) = value.Split();
        return gender.ToByte( race );
    }

    public static (Gender, ModelRace) Split( this GenderRace value )
    {
        return value switch
        {
            GenderRace.Unknown             => ( Gender.Unknown, ModelRace.Unknown ),
            GenderRace.MidlanderMale       => ( Gender.Male, ModelRace.Midlander ),
            GenderRace.MidlanderMaleNpc    => ( Gender.MaleNpc, ModelRace.Midlander ),
            GenderRace.MidlanderFemale     => ( Gender.Female, ModelRace.Midlander ),
            GenderRace.MidlanderFemaleNpc  => ( Gender.FemaleNpc, ModelRace.Midlander ),
            GenderRace.HighlanderMale      => ( Gender.Male, ModelRace.Highlander ),
            GenderRace.HighlanderMaleNpc   => ( Gender.MaleNpc, ModelRace.Highlander ),
            GenderRace.HighlanderFemale    => ( Gender.Female, ModelRace.Highlander ),
            GenderRace.HighlanderFemaleNpc => ( Gender.FemaleNpc, ModelRace.Highlander ),
            GenderRace.ElezenMale          => ( Gender.Male, ModelRace.Elezen ),
            GenderRace.ElezenMaleNpc       => ( Gender.MaleNpc, ModelRace.Elezen ),
            GenderRace.ElezenFemale        => ( Gender.Female, ModelRace.Elezen ),
            GenderRace.ElezenFemaleNpc     => ( Gender.FemaleNpc, ModelRace.Elezen ),
            GenderRace.LalafellMale        => ( Gender.Male, ModelRace.Lalafell ),
            GenderRace.LalafellMaleNpc     => ( Gender.MaleNpc, ModelRace.Lalafell ),
            GenderRace.LalafellFemale      => ( Gender.Female, ModelRace.Lalafell ),
            GenderRace.LalafellFemaleNpc   => ( Gender.FemaleNpc, ModelRace.Lalafell ),
            GenderRace.MiqoteMale          => ( Gender.Male, ModelRace.Miqote ),
            GenderRace.MiqoteMaleNpc       => ( Gender.MaleNpc, ModelRace.Miqote ),
            GenderRace.MiqoteFemale        => ( Gender.Female, ModelRace.Miqote ),
            GenderRace.MiqoteFemaleNpc     => ( Gender.FemaleNpc, ModelRace.Miqote ),
            GenderRace.RoegadynMale        => ( Gender.Male, ModelRace.Roegadyn ),
            GenderRace.RoegadynMaleNpc     => ( Gender.MaleNpc, ModelRace.Roegadyn ),
            GenderRace.RoegadynFemale      => ( Gender.Female, ModelRace.Roegadyn ),
            GenderRace.RoegadynFemaleNpc   => ( Gender.FemaleNpc, ModelRace.Roegadyn ),
            GenderRace.AuRaMale            => ( Gender.Male, ModelRace.AuRa ),
            GenderRace.AuRaMaleNpc         => ( Gender.MaleNpc, ModelRace.AuRa ),
            GenderRace.AuRaFemale          => ( Gender.Female, ModelRace.AuRa ),
            GenderRace.AuRaFemaleNpc       => ( Gender.FemaleNpc, ModelRace.AuRa ),
            GenderRace.HrothgarMale        => ( Gender.Male, ModelRace.Hrothgar ),
            GenderRace.HrothgarMaleNpc     => ( Gender.MaleNpc, ModelRace.Hrothgar ),
            GenderRace.HrothgarFemale      => ( Gender.Female, ModelRace.Hrothgar ),
            GenderRace.HrothgarFemaleNpc   => ( Gender.FemaleNpc, ModelRace.Hrothgar ),
            GenderRace.VieraMale           => ( Gender.Male, ModelRace.Viera ),
            GenderRace.VieraMaleNpc        => ( Gender.Male, ModelRace.Viera ),
            GenderRace.VieraFemale         => ( Gender.Female, ModelRace.Viera ),
            GenderRace.VieraFemaleNpc      => ( Gender.FemaleNpc, ModelRace.Viera ),
            GenderRace.UnknownMaleNpc      => ( Gender.MaleNpc, ModelRace.Unknown ),
            GenderRace.UnknownFemaleNpc    => ( Gender.FemaleNpc, ModelRace.Unknown ),
            _                              => throw new InvalidEnumArgumentException(),
        };
    }

    public static bool IsValid( this GenderRace value )
        => value != GenderRace.Unknown && Enum.IsDefined( typeof( GenderRace ), value );

    public static string ToRaceCode( this GenderRace value )
    {
        return value switch
        {
            GenderRace.MidlanderMale       => "0101",
            GenderRace.MidlanderMaleNpc    => "0104",
            GenderRace.MidlanderFemale     => "0201",
            GenderRace.MidlanderFemaleNpc  => "0204",
            GenderRace.HighlanderMale      => "0301",
            GenderRace.HighlanderMaleNpc   => "0304",
            GenderRace.HighlanderFemale    => "0401",
            GenderRace.HighlanderFemaleNpc => "0404",
            GenderRace.ElezenMale          => "0501",
            GenderRace.ElezenMaleNpc       => "0504",
            GenderRace.ElezenFemale        => "0601",
            GenderRace.ElezenFemaleNpc     => "0604",
            GenderRace.MiqoteMale          => "0701",
            GenderRace.MiqoteMaleNpc       => "0704",
            GenderRace.MiqoteFemale        => "0801",
            GenderRace.MiqoteFemaleNpc     => "0804",
            GenderRace.RoegadynMale        => "0901",
            GenderRace.RoegadynMaleNpc     => "0904",
            GenderRace.RoegadynFemale      => "1001",
            GenderRace.RoegadynFemaleNpc   => "1004",
            GenderRace.LalafellMale        => "1101",
            GenderRace.LalafellMaleNpc     => "1104",
            GenderRace.LalafellFemale      => "1201",
            GenderRace.LalafellFemaleNpc   => "1204",
            GenderRace.AuRaMale            => "1301",
            GenderRace.AuRaMaleNpc         => "1304",
            GenderRace.AuRaFemale          => "1401",
            GenderRace.AuRaFemaleNpc       => "1404",
            GenderRace.HrothgarMale        => "1501",
            GenderRace.HrothgarMaleNpc     => "1504",
            GenderRace.HrothgarFemale      => "1601",
            GenderRace.HrothgarFemaleNpc   => "1604",
            GenderRace.VieraMale           => "1701",
            GenderRace.VieraMaleNpc        => "1704",
            GenderRace.VieraFemale         => "1801",
            GenderRace.VieraFemaleNpc      => "1804",
            GenderRace.UnknownMaleNpc      => "9104",
            GenderRace.UnknownFemaleNpc    => "9204",
            _                              => throw new InvalidEnumArgumentException(),
        };
    }
}

public static partial class Names
{
    public static GenderRace GenderRaceFromCode( string code )
    {
        return code switch
        {
            "0101" => GenderRace.MidlanderMale,
            "0104" => GenderRace.MidlanderMaleNpc,
            "0201" => GenderRace.MidlanderFemale,
            "0204" => GenderRace.MidlanderFemaleNpc,
            "0301" => GenderRace.HighlanderMale,
            "0304" => GenderRace.HighlanderMaleNpc,
            "0401" => GenderRace.HighlanderFemale,
            "0404" => GenderRace.HighlanderFemaleNpc,
            "0501" => GenderRace.ElezenMale,
            "0504" => GenderRace.ElezenMaleNpc,
            "0601" => GenderRace.ElezenFemale,
            "0604" => GenderRace.ElezenFemaleNpc,
            "0701" => GenderRace.MiqoteMale,
            "0704" => GenderRace.MiqoteMaleNpc,
            "0801" => GenderRace.MiqoteFemale,
            "0804" => GenderRace.MiqoteFemaleNpc,
            "0901" => GenderRace.RoegadynMale,
            "0904" => GenderRace.RoegadynMaleNpc,
            "1001" => GenderRace.RoegadynFemale,
            "1004" => GenderRace.RoegadynFemaleNpc,
            "1101" => GenderRace.LalafellMale,
            "1104" => GenderRace.LalafellMaleNpc,
            "1201" => GenderRace.LalafellFemale,
            "1204" => GenderRace.LalafellFemaleNpc,
            "1301" => GenderRace.AuRaMale,
            "1304" => GenderRace.AuRaMaleNpc,
            "1401" => GenderRace.AuRaFemale,
            "1404" => GenderRace.AuRaFemaleNpc,
            "1501" => GenderRace.HrothgarMale,
            "1504" => GenderRace.HrothgarMaleNpc,
            "1601" => GenderRace.HrothgarFemale,
            "1604" => GenderRace.HrothgarFemaleNpc,
            "1701" => GenderRace.VieraMale,
            "1704" => GenderRace.VieraMaleNpc,
            "1801" => GenderRace.VieraFemale,
            "1804" => GenderRace.VieraFemaleNpc,
            "9104" => GenderRace.UnknownMaleNpc,
            "9204" => GenderRace.UnknownFemaleNpc,
            _      => throw new KeyNotFoundException(),
        };
    }

    public static GenderRace GenderRaceFromByte( byte value )
    {
        var gender = ( Gender )( value & 0b111 );
        var race   = ( ModelRace )( value >> 3 );
        return CombinedRace( gender, race );
    }

    public static GenderRace CombinedRace( Gender gender, ModelRace modelRace )
    {
        return gender switch
        {
            Gender.Male => modelRace switch
            {
                ModelRace.Midlander  => GenderRace.MidlanderMale,
                ModelRace.Highlander => GenderRace.HighlanderMale,
                ModelRace.Elezen     => GenderRace.ElezenMale,
                ModelRace.Lalafell   => GenderRace.LalafellMale,
                ModelRace.Miqote     => GenderRace.MiqoteMale,
                ModelRace.Roegadyn   => GenderRace.RoegadynMale,
                ModelRace.AuRa       => GenderRace.AuRaMale,
                ModelRace.Hrothgar   => GenderRace.HrothgarMale,
                ModelRace.Viera      => GenderRace.VieraMale,
                _                    => GenderRace.Unknown,
            },
            Gender.MaleNpc => modelRace switch
            {
                ModelRace.Midlander  => GenderRace.MidlanderMaleNpc,
                ModelRace.Highlander => GenderRace.HighlanderMaleNpc,
                ModelRace.Elezen     => GenderRace.ElezenMaleNpc,
                ModelRace.Lalafell   => GenderRace.LalafellMaleNpc,
                ModelRace.Miqote     => GenderRace.MiqoteMaleNpc,
                ModelRace.Roegadyn   => GenderRace.RoegadynMaleNpc,
                ModelRace.AuRa       => GenderRace.AuRaMaleNpc,
                ModelRace.Hrothgar   => GenderRace.HrothgarMaleNpc,
                ModelRace.Viera      => GenderRace.VieraMaleNpc,
                _                    => GenderRace.Unknown,
            },
            Gender.Female => modelRace switch
            {
                ModelRace.Midlander  => GenderRace.MidlanderFemale,
                ModelRace.Highlander => GenderRace.HighlanderFemale,
                ModelRace.Elezen     => GenderRace.ElezenFemale,
                ModelRace.Lalafell   => GenderRace.LalafellFemale,
                ModelRace.Miqote     => GenderRace.MiqoteFemale,
                ModelRace.Roegadyn   => GenderRace.RoegadynFemale,
                ModelRace.AuRa       => GenderRace.AuRaFemale,
                ModelRace.Hrothgar   => GenderRace.HrothgarFemale,
                ModelRace.Viera      => GenderRace.VieraFemale,
                _                    => GenderRace.Unknown,
            },
            Gender.FemaleNpc => modelRace switch
            {
                ModelRace.Midlander  => GenderRace.MidlanderFemaleNpc,
                ModelRace.Highlander => GenderRace.HighlanderFemaleNpc,
                ModelRace.Elezen     => GenderRace.ElezenFemaleNpc,
                ModelRace.Lalafell   => GenderRace.LalafellFemaleNpc,
                ModelRace.Miqote     => GenderRace.MiqoteFemaleNpc,
                ModelRace.Roegadyn   => GenderRace.RoegadynFemaleNpc,
                ModelRace.AuRa       => GenderRace.AuRaFemaleNpc,
                ModelRace.Hrothgar   => GenderRace.HrothgarFemaleNpc,
                ModelRace.Viera      => GenderRace.VieraFemaleNpc,
                _                    => GenderRace.Unknown,
            },
            _ => GenderRace.Unknown,
        };
    }
}