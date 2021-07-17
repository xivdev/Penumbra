using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Penumbra.Game;
using Penumbra.Game.Enums;
using Penumbra.Meta.Files;
using Penumbra.Util;
using Swan;
using ImcFile = Lumina.Data.Files.ImcFile;

namespace Penumbra.Meta
{
    // Write a single meta manipulation as a Base64string of the 16 bytes defining it.
    public class MetaManipulationConverter : JsonConverter< MetaManipulation >
    {
        public override void WriteJson( JsonWriter writer, MetaManipulation manip, JsonSerializer serializer )
        {
            var s = Convert.ToBase64String( manip.ToBytes() );
            writer.WriteValue( s );
        }

        public override MetaManipulation ReadJson( JsonReader reader, Type objectType, MetaManipulation existingValue, bool hasExistingValue,
            JsonSerializer serializer )

        {
            if( reader.TokenType != JsonToken.String )
            {
                throw new JsonReaderException();
            }

            var                bytes = Convert.FromBase64String( ( string )reader.Value! );
            using MemoryStream m     = new( bytes );
            using BinaryReader br    = new( m );
            var                i     = br.ReadUInt64();
            var                v     = br.ReadUInt64();
            return new MetaManipulation( i, v );
        }
    }

    // A MetaManipulation is a union of a type of Identifier (first 8 bytes, cf. Identifier.cs)
    // and the appropriate Value to change the meta entry to (the other 8 bytes).
    // Its comparison for sorting and hashes depends only on the identifier.
    // The first byte is guaranteed to be a MetaType enum value in any case, so Type can always be read.
    [StructLayout( LayoutKind.Explicit )]
    [JsonConverter( typeof( MetaManipulationConverter ) )]
    public struct MetaManipulation : IComparable
    {
        public static MetaManipulation Eqp( EquipSlot equipSlot, ushort setId, EqpEntry value )
            => new()
            {
                EqpIdentifier = new EqpIdentifier()
                {
                    Type  = MetaType.Eqp,
                    Slot  = equipSlot,
                    SetId = setId,
                },
                EqpValue = value,
            };

        public static MetaManipulation Eqdp( EquipSlot equipSlot, GenderRace gr, ushort setId, EqdpEntry value )
            => new()
            {
                EqdpIdentifier = new EqdpIdentifier()
                {
                    Type       = MetaType.Eqdp,
                    Slot       = equipSlot,
                    GenderRace = gr,
                    SetId      = setId,
                },
                EqdpValue = value,
            };

        public static MetaManipulation Gmp( ushort setId, GmpEntry value )
            => new()
            {
                GmpIdentifier = new GmpIdentifier()
                {
                    Type  = MetaType.Gmp,
                    SetId = setId,
                },
                GmpValue = value,
            };

        public static MetaManipulation Est( ObjectType type, EquipSlot equipSlot, GenderRace gr, BodySlot bodySlot, ushort setId,
            ushort value )
            => new()
            {
                EstIdentifier = new EstIdentifier()
                {
                    Type       = MetaType.Est,
                    ObjectType = type,
                    GenderRace = gr,
                    EquipSlot  = equipSlot,
                    BodySlot   = bodySlot,
                    PrimaryId  = setId,
                },
                EstValue = value,
            };

        public static MetaManipulation Imc( ObjectType type, BodySlot secondaryType, ushort primaryId, ushort secondaryId
            , ushort idx, ImcFile.ImageChangeData value )
            => new()
            {
                ImcIdentifier = new ImcIdentifier()
                {
                    Type        = MetaType.Imc,
                    ObjectType  = type,
                    BodySlot    = secondaryType,
                    PrimaryId   = primaryId,
                    SecondaryId = secondaryId,
                    Variant     = idx,
                },
                ImcValue = value,
            };

        public static MetaManipulation Imc( EquipSlot slot, ushort primaryId, ushort idx, ImcFile.ImageChangeData value )
            => new()
            {
                ImcIdentifier = new ImcIdentifier()
                {
                    Type       = MetaType.Imc,
                    ObjectType = slot.IsAccessory() ? ObjectType.Accessory : ObjectType.Equipment,
                    EquipSlot  = slot,
                    PrimaryId  = primaryId,
                    Variant    = idx,
                },
                ImcValue = value,
            };

        public static MetaManipulation Rsp( SubRace subRace, RspAttribute attribute, float value )
            => new()
            {
                RspIdentifier = new RspIdentifier()
                {
                    Type      = MetaType.Rsp,
                    SubRace   = subRace,
                    Attribute = attribute,
                },
                RspValue = value,
            };

        internal MetaManipulation( ulong identifier, ulong value )
            : this()
        {
            Identifier = identifier;
            Value      = value;
        }

        [FieldOffset( 0 )]
        public readonly ulong Identifier;

        [FieldOffset( 8 )]
        public readonly ulong Value;

        [FieldOffset( 0 )]
        public MetaType Type;

        [FieldOffset( 0 )]
        public EqpIdentifier EqpIdentifier;

        [FieldOffset( 0 )]
        public GmpIdentifier GmpIdentifier;

        [FieldOffset( 0 )]
        public EqdpIdentifier EqdpIdentifier;

        [FieldOffset( 0 )]
        public EstIdentifier EstIdentifier;

        [FieldOffset( 0 )]
        public ImcIdentifier ImcIdentifier;

        [FieldOffset( 0 )]
        public RspIdentifier RspIdentifier;


        [FieldOffset( 8 )]
        public EqpEntry EqpValue;

        [FieldOffset( 8 )]
        public GmpEntry GmpValue;

        [FieldOffset( 8 )]
        public EqdpEntry EqdpValue;

        [FieldOffset( 8 )]
        public ushort EstValue;

        [FieldOffset( 8 )]
        public ImcFile.ImageChangeData ImcValue; // 6 bytes.

        [FieldOffset( 8 )]
        public float RspValue;

        public override int GetHashCode()
            => Identifier.GetHashCode();

        public int CompareTo( object? rhs )
            => Identifier.CompareTo( rhs is MetaManipulation m ? m.Identifier : null );

        public GamePath CorrespondingFilename()
        {
            return Type switch
            {
                MetaType.Eqp  => MetaFileNames.Eqp(),
                MetaType.Eqdp => MetaFileNames.Eqdp( EqdpIdentifier.Slot, EqdpIdentifier.GenderRace ),
                MetaType.Est  => MetaFileNames.Est( EstIdentifier.ObjectType, EstIdentifier.EquipSlot, EstIdentifier.BodySlot ),
                MetaType.Gmp  => MetaFileNames.Gmp(),
                MetaType.Imc  => MetaFileNames.Imc( ImcIdentifier.ObjectType, ImcIdentifier.PrimaryId, ImcIdentifier.SecondaryId ),
                MetaType.Rsp  => MetaFileNames.Cmp(),
                _             => throw new InvalidEnumArgumentException(),
            };
        }

        // No error checking.
        public bool Apply( EqpFile file )
            => file[ EqpIdentifier.SetId ].Apply( this );

        public bool Apply( EqdpFile file )
            => file[ EqdpIdentifier.SetId ].Apply( this );

        public bool Apply( GmpFile file )
            => file.SetEntry( GmpIdentifier.SetId, GmpValue );

        public bool Apply( EstFile file )
            => file.SetEntry( EstIdentifier.GenderRace, EstIdentifier.PrimaryId, EstValue );

        public bool Apply( ImcFile file )
        {
            ref var value = ref file.GetValue( this );
            if( ImcValue.Equal( value ) )
            {
                return false;
            }

            value = ImcValue;
            return true;
        }

        public bool Apply( CmpFile file )
            => file.Set( RspIdentifier.SubRace, RspIdentifier.Attribute, RspValue );

        public string IdentifierString()
        {
            return Type switch
            {
                MetaType.Eqp  => EqpIdentifier.ToString(),
                MetaType.Eqdp => EqdpIdentifier.ToString(),
                MetaType.Est  => EstIdentifier.ToString(),
                MetaType.Gmp  => GmpIdentifier.ToString(),
                MetaType.Imc  => ImcIdentifier.ToString(),
                MetaType.Rsp  => RspIdentifier.ToString(),
                _             => throw new InvalidEnumArgumentException(),
            };
        }
    }
}