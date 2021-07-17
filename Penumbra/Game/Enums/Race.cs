using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Penumbra.Game.Enums
{
    public enum Gender : byte
    {
        Unknown,
        Male,
        Female,
        MaleNpc,
        FemaleNpc,
    }

    public enum Race : byte
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
        Duskwright,
        Plainsfolk,
        Dunesfolk,
        SeekerOfTheSun,
        KeeperOfTheMoon,
        Seawolf,
        Hellsguard,
        Raen,
        Xaela,
        Hellion,
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
        VieraFemale         = 1801,
        VieraFemaleNpc      = 1804,
        UnknownMaleNpc      = 9104,
        UnknownFemaleNpc    = 9204,
    }

    public static class RaceEnumExtensions
    {
        public static int ToRspIndex( this SubRace subRace )
        {
            return subRace switch
            {
                SubRace.Midlander       => 0,
                SubRace.Highlander      => 1,
                SubRace.Wildwood        => 10,
                SubRace.Duskwright      => 11,
                SubRace.Plainsfolk      => 20,
                SubRace.Dunesfolk       => 21,
                SubRace.SeekerOfTheSun  => 30,
                SubRace.KeeperOfTheMoon => 31,
                SubRace.Seawolf         => 40,
                SubRace.Hellsguard      => 41,
                SubRace.Raen            => 50,
                SubRace.Xaela           => 51,
                SubRace.Hellion         => 60,
                SubRace.Lost            => 61,
                SubRace.Rava            => 70,
                SubRace.Veena           => 71,
                _                       => throw new InvalidEnumArgumentException(),
            };
        }

        public static Race ToRace( this SubRace subRace )
        {
            return subRace switch
            {
                SubRace.Unknown         => Race.Unknown,
                SubRace.Midlander       => Race.Midlander,
                SubRace.Highlander      => Race.Highlander,
                SubRace.Wildwood        => Race.Elezen,
                SubRace.Duskwright      => Race.Elezen,
                SubRace.Plainsfolk      => Race.Lalafell,
                SubRace.Dunesfolk       => Race.Lalafell,
                SubRace.SeekerOfTheSun  => Race.Miqote,
                SubRace.KeeperOfTheMoon => Race.Miqote,
                SubRace.Seawolf         => Race.Roegadyn,
                SubRace.Hellsguard      => Race.Roegadyn,
                SubRace.Raen            => Race.AuRa,
                SubRace.Xaela           => Race.AuRa,
                SubRace.Hellion         => Race.Hrothgar,
                SubRace.Lost            => Race.Hrothgar,
                SubRace.Rava            => Race.Viera,
                SubRace.Veena           => Race.Viera,
                _                       => throw new InvalidEnumArgumentException(),
            };
        }

        public static bool FitsRace( this SubRace subRace, Race race )
            => subRace.ToRace() == race;

        public static byte ToByte( this Gender gender, Race race )
            => ( byte )( ( int )gender | ( ( int )race << 3 ) );

        public static byte ToByte( this Race race, Gender gender )
            => gender.ToByte( race );

        public static byte ToByte( this GenderRace value )
        {
            var (gender, race) = value.Split();
            return gender.ToByte( race );
        }

        public static (Gender, Race) Split( this GenderRace value )
        {
            return value switch
            {
                GenderRace.Unknown             => ( Gender.Unknown, Race.Unknown ),
                GenderRace.MidlanderMale       => ( Gender.Male, Race.Midlander ),
                GenderRace.MidlanderMaleNpc    => ( Gender.MaleNpc, Race.Midlander ),
                GenderRace.MidlanderFemale     => ( Gender.Female, Race.Midlander ),
                GenderRace.MidlanderFemaleNpc  => ( Gender.FemaleNpc, Race.Midlander ),
                GenderRace.HighlanderMale      => ( Gender.Male, Race.Highlander ),
                GenderRace.HighlanderMaleNpc   => ( Gender.MaleNpc, Race.Highlander ),
                GenderRace.HighlanderFemale    => ( Gender.Female, Race.Highlander ),
                GenderRace.HighlanderFemaleNpc => ( Gender.FemaleNpc, Race.Highlander ),
                GenderRace.ElezenMale          => ( Gender.Male, Race.Elezen ),
                GenderRace.ElezenMaleNpc       => ( Gender.MaleNpc, Race.Elezen ),
                GenderRace.ElezenFemale        => ( Gender.Female, Race.Elezen ),
                GenderRace.ElezenFemaleNpc     => ( Gender.FemaleNpc, Race.Elezen ),
                GenderRace.LalafellMale        => ( Gender.Male, Race.Lalafell ),
                GenderRace.LalafellMaleNpc     => ( Gender.MaleNpc, Race.Lalafell ),
                GenderRace.LalafellFemale      => ( Gender.Female, Race.Lalafell ),
                GenderRace.LalafellFemaleNpc   => ( Gender.FemaleNpc, Race.Lalafell ),
                GenderRace.MiqoteMale          => ( Gender.Male, Race.Miqote ),
                GenderRace.MiqoteMaleNpc       => ( Gender.MaleNpc, Race.Miqote ),
                GenderRace.MiqoteFemale        => ( Gender.Female, Race.Miqote ),
                GenderRace.MiqoteFemaleNpc     => ( Gender.FemaleNpc, Race.Miqote ),
                GenderRace.RoegadynMale        => ( Gender.Male, Race.Roegadyn ),
                GenderRace.RoegadynMaleNpc     => ( Gender.MaleNpc, Race.Roegadyn ),
                GenderRace.RoegadynFemale      => ( Gender.Female, Race.Roegadyn ),
                GenderRace.RoegadynFemaleNpc   => ( Gender.FemaleNpc, Race.Roegadyn ),
                GenderRace.AuRaMale            => ( Gender.Male, Race.AuRa ),
                GenderRace.AuRaMaleNpc         => ( Gender.MaleNpc, Race.AuRa ),
                GenderRace.AuRaFemale          => ( Gender.Female, Race.AuRa ),
                GenderRace.AuRaFemaleNpc       => ( Gender.FemaleNpc, Race.AuRa ),
                GenderRace.HrothgarMale        => ( Gender.Male, Race.Hrothgar ),
                GenderRace.HrothgarMaleNpc     => ( Gender.MaleNpc, Race.Hrothgar ),
                GenderRace.VieraFemale         => ( Gender.Female, Race.Viera ),
                GenderRace.VieraFemaleNpc      => ( Gender.FemaleNpc, Race.Viera ),
                GenderRace.UnknownMaleNpc      => ( Gender.MaleNpc, Race.Unknown ),
                GenderRace.UnknownFemaleNpc    => ( Gender.FemaleNpc, Race.Unknown ),
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
                GenderRace.VieraFemale         => "1801",
                GenderRace.VieraFemaleNpc      => "1804",
                GenderRace.UnknownMaleNpc      => "9104",
                GenderRace.UnknownFemaleNpc    => "9204",
                _                              => throw new InvalidEnumArgumentException(),
            };
        }
    }

    public static partial class GameData
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
            var race   = ( Race )( value >> 3 );
            return CombinedRace( gender, race );
        }

        public static GenderRace CombinedRace( Gender gender, Race race )
        {
            return gender switch
            {
                Gender.Male => race switch
                {
                    Race.Midlander  => GenderRace.MidlanderMale,
                    Race.Highlander => GenderRace.HighlanderMale,
                    Race.Elezen     => GenderRace.ElezenMale,
                    Race.Lalafell   => GenderRace.LalafellMale,
                    Race.Miqote     => GenderRace.MiqoteMale,
                    Race.Roegadyn   => GenderRace.RoegadynMale,
                    Race.AuRa       => GenderRace.AuRaMale,
                    Race.Hrothgar   => GenderRace.HrothgarMale,
                    _               => GenderRace.Unknown,
                },
                Gender.MaleNpc => race switch
                {
                    Race.Midlander  => GenderRace.MidlanderMaleNpc,
                    Race.Highlander => GenderRace.HighlanderMaleNpc,
                    Race.Elezen     => GenderRace.ElezenMaleNpc,
                    Race.Lalafell   => GenderRace.LalafellMaleNpc,
                    Race.Miqote     => GenderRace.MiqoteMaleNpc,
                    Race.Roegadyn   => GenderRace.RoegadynMaleNpc,
                    Race.AuRa       => GenderRace.AuRaMaleNpc,
                    Race.Hrothgar   => GenderRace.HrothgarMaleNpc,
                    _               => GenderRace.Unknown,
                },
                Gender.Female => race switch
                {
                    Race.Midlander  => GenderRace.MidlanderFemale,
                    Race.Highlander => GenderRace.HighlanderFemale,
                    Race.Elezen     => GenderRace.ElezenFemale,
                    Race.Lalafell   => GenderRace.LalafellFemale,
                    Race.Miqote     => GenderRace.MiqoteFemale,
                    Race.Roegadyn   => GenderRace.RoegadynFemale,
                    Race.AuRa       => GenderRace.AuRaFemale,
                    Race.Viera      => GenderRace.VieraFemale,
                    _               => GenderRace.Unknown,
                },
                Gender.FemaleNpc => race switch
                {
                    Race.Midlander  => GenderRace.MidlanderFemaleNpc,
                    Race.Highlander => GenderRace.HighlanderFemaleNpc,
                    Race.Elezen     => GenderRace.ElezenFemaleNpc,
                    Race.Lalafell   => GenderRace.LalafellFemaleNpc,
                    Race.Miqote     => GenderRace.MiqoteFemaleNpc,
                    Race.Roegadyn   => GenderRace.RoegadynFemaleNpc,
                    Race.AuRa       => GenderRace.AuRaFemaleNpc,
                    Race.Viera      => GenderRace.VieraFemaleNpc,
                    _               => GenderRace.Unknown,
                },
                _ => GenderRace.Unknown,
            };
        }
    }
}