using System.Runtime.InteropServices;
using Penumbra.Game.Enums;

namespace Penumbra.Meta
{
    public enum MetaType : byte
    {
        Unknown = 0,
        Imc     = 1,
        Eqdp    = 2,
        Eqp     = 3,
        Est     = 4,
        Gmp     = 5,
    };

    [StructLayout( LayoutKind.Explicit )]
    public struct EqpIdentifier
    {
        [FieldOffset( 0 )]
        public ulong Value;

        [FieldOffset( 0 )]
        public MetaType Type;

        [FieldOffset( 1 )]
        public EquipSlot Slot;

        [FieldOffset( 2 )]
        public ushort SetId;

        public override string ToString()
            => $"Eqp - {SetId} - {Slot}";
    }

    [StructLayout( LayoutKind.Explicit )]
    public struct EqdpIdentifier
    {
        [FieldOffset( 0 )]
        public ulong Value;

        [FieldOffset( 0 )]
        public MetaType Type;

        [FieldOffset( 1 )]
        public EquipSlot Slot;

        [FieldOffset( 2 )]
        public GenderRace GenderRace;

        [FieldOffset( 4 )]
        public ushort SetId;

        public override string ToString()
            => $"Eqdp - {SetId} - {Slot} - {GenderRace.Split().Item2} {GenderRace.Split().Item1}";
    }

    [StructLayout( LayoutKind.Explicit )]
    public struct GmpIdentifier
    {
        [FieldOffset( 0 )]
        public ulong Value;

        [FieldOffset( 0 )]
        public MetaType Type;

        [FieldOffset( 1 )]
        public ushort SetId;

        public override string ToString()
            => $"Gmp - {SetId}";
    }

    [StructLayout( LayoutKind.Explicit )]
    public struct EstIdentifier
    {
        [FieldOffset( 0 )]
        public ulong Value;

        [FieldOffset( 0 )]
        public MetaType Type;

        [FieldOffset( 1 )]
        public ObjectType ObjectType;

        [FieldOffset( 2 )]
        public EquipSlot EquipSlot;

        [FieldOffset( 3 )]
        public BodySlot BodySlot;

        [FieldOffset( 4 )]
        public GenderRace GenderRace;

        [FieldOffset( 6 )]
        public ushort PrimaryId;

        public override string ToString()
            => ObjectType == ObjectType.Equipment
                ? $"Est - {PrimaryId} - {EquipSlot} - {GenderRace.Split().Item2} {GenderRace.Split().Item1}"
                : $"Est - {PrimaryId} - {BodySlot} - {GenderRace.Split().Item2} {GenderRace.Split().Item1}";
    }

    [StructLayout( LayoutKind.Explicit )]
    public struct ImcIdentifier
    {
        [FieldOffset( 0 )]
        public ulong Value;

        [FieldOffset( 0 )]
        public MetaType Type;

        [FieldOffset( 1 )]
        public byte _objectAndBody;

        public ObjectType ObjectType
        {
            get => ( ObjectType )( _objectAndBody & 0b00011111 );
            set => _objectAndBody = ( byte )( ( _objectAndBody & 0b11100000 ) | ( byte )value );
        }

        public BodySlot BodySlot
        {
            get => ( BodySlot )( _objectAndBody >> 5 );
            set => _objectAndBody = ( byte )( ( _objectAndBody & 0b00011111 ) | ( ( byte )value << 5 ) );
        }

        [FieldOffset( 2 )]
        public ushort PrimaryId;

        [FieldOffset( 4 )]
        public ushort Variant;

        [FieldOffset( 6 )]
        public ushort SecondaryId;

        [FieldOffset( 6 )]
        public EquipSlot EquipSlot;

        public override string ToString()
        {
            return ObjectType switch
            {
                ObjectType.Accessory => $"Imc - {PrimaryId} - {EquipSlot} - {Variant}",
                ObjectType.Equipment => $"Imc - {PrimaryId} - {EquipSlot} - {Variant}",
                _                    => $"Imc - {PrimaryId} - {ObjectType} - {SecondaryId} - {BodySlot} - {Variant}",
            };
        }
    }
}