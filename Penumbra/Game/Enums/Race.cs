using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Penumbra.Game
{
    public enum Gender : byte
    {
        Unknown,
        Male,
        Female,
        MaleNpc,
        FemaleNpc
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
        Viera
    }

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
        LalafellMale        = 0701,
        LalafellMaleNpc     = 0704,
        LalafellFemale      = 0801,
        LalafellFemaleNpc   = 0804,
        MiqoteMale          = 0901,
        MiqoteMaleNpc       = 0904,
        MiqoteFemale        = 1001,
        MiqoteFemaleNpc     = 1004,
        RoegadynMale        = 1101,
        RoegadynMaleNpc     = 1104,
        RoegadynFemale      = 1201,
        RoegadynFemaleNpc   = 1204,
        AuRaMale            = 1301,
        AuRaMaleNpc         = 1304,
        AuRaFemale          = 1401,
        AuRaFemaleNpc       = 1404,
        HrothgarMale        = 1501,
        HrothgarMaleNpc     = 1504,
        VieraFemale         = 1801,
        VieraFemaleNpc      = 1804,
        UnknownMaleNpc      = 9104,
        UnknownFemaleNpc    = 9204
    }

    public static class RaceEnumExtensions
    {
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
                _                              => throw new InvalidEnumArgumentException()
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
                GenderRace.LalafellMale        => "0701",
                GenderRace.LalafellMaleNpc     => "0704",
                GenderRace.LalafellFemale      => "0801",
                GenderRace.LalafellFemaleNpc   => "0804",
                GenderRace.MiqoteMale          => "0901",
                GenderRace.MiqoteMaleNpc       => "0904",
                GenderRace.MiqoteFemale        => "1001",
                GenderRace.MiqoteFemaleNpc     => "1004",
                GenderRace.RoegadynMale        => "1101",
                GenderRace.RoegadynMaleNpc     => "1104",
                GenderRace.RoegadynFemale      => "1201",
                GenderRace.RoegadynFemaleNpc   => "1204",
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
                _                              => throw new InvalidEnumArgumentException()
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
                "0701" => GenderRace.LalafellMale,
                "0704" => GenderRace.LalafellMaleNpc,
                "0801" => GenderRace.LalafellFemale,
                "0804" => GenderRace.LalafellFemaleNpc,
                "0901" => GenderRace.MiqoteMale,
                "0904" => GenderRace.MiqoteMaleNpc,
                "1001" => GenderRace.MiqoteFemale,
                "1004" => GenderRace.MiqoteFemaleNpc,
                "1101" => GenderRace.RoegadynMale,
                "1104" => GenderRace.RoegadynMaleNpc,
                "1201" => GenderRace.RoegadynFemale,
                "1204" => GenderRace.RoegadynFemaleNpc,
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
                _      => throw new KeyNotFoundException()
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
                    _               => GenderRace.Unknown
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
                    _               => GenderRace.Unknown
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
                    _               => GenderRace.Unknown
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
                    _               => GenderRace.Unknown
                },
                _ => GenderRace.Unknown
            };
        }
    }
}