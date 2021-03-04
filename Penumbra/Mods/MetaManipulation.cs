using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Penumbra.Game;
using Penumbra.MetaData;
using Penumbra.Util;
using ImcFile = Lumina.Data.Files.ImcFile;

namespace Penumbra.Mods
{
    public enum MetaType : byte
    {
        Unknown = 0,
        Imc     = 1,
        Eqdp    = 2,
        Eqp     = 3,
        Est     = 4,
        Gmp     = 5
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
            get => ( BodySlot )( _objectAndBody & 0b11100000 );
            set => _objectAndBody = ( byte )( ( _objectAndBody & 0b00011111 ) | ( byte )value );
        }

        [FieldOffset( 2 )]
        public ushort PrimaryId;

        [FieldOffset( 4 )]
        public ushort Variant;

        [FieldOffset( 6 )]
        public ushort SecondaryId;

        [FieldOffset( 6 )]
        public EquipSlot EquipSlot;
    }

    [StructLayout( LayoutKind.Explicit )]
    public struct MetaManipulation : IComparable
    {
        public static MetaManipulation Eqp( EquipSlot equipSlot, ushort setId, EqpEntry value )
            => new()
            {
                EqpIdentifier = new EqpIdentifier()
                {
                    Type  = MetaType.Eqp,
                    Slot  = equipSlot,
                    SetId = setId
                },
                EqpValue = value
            };

        public static MetaManipulation Eqdp( EquipSlot equipSlot, GenderRace gr, ushort setId, EqdpEntry value )
            => new()
            {
                EqdpIdentifier = new EqdpIdentifier()
                {
                    Type       = MetaType.Eqdp,
                    Slot       = equipSlot,
                    GenderRace = gr,
                    SetId      = setId
                },
                EqdpValue = value
            };

        public static MetaManipulation Gmp( ushort setId, GmpEntry value )
            => new()
            {
                GmpIdentifier = new GmpIdentifier()
                {
                    Type  = MetaType.Gmp,
                    SetId = setId
                },
                GmpValue = value
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
                    PrimaryId  = setId
                },
                EstValue = value
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
                    Variant     = idx
                },
                ImcValue = value
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
                    Variant    = idx
                },
                ImcValue = value
            };

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

        public override int GetHashCode()
            => Identifier.GetHashCode();

        public int CompareTo( object? rhs )
            => Identifier.CompareTo( rhs );

        public GamePath CorrespondingFilename()
        {
            return Type switch
            {
                MetaType.Eqp  => MetaFileNames.Eqp(),
                MetaType.Eqdp => MetaFileNames.Eqdp( EqdpIdentifier.Slot, EqdpIdentifier.GenderRace ),
                MetaType.Est  => MetaFileNames.Est( EstIdentifier.ObjectType, EstIdentifier.EquipSlot, EstIdentifier.BodySlot ),
                MetaType.Gmp  => MetaFileNames.Gmp(),
                MetaType.Imc  => MetaFileNames.Imc( ImcIdentifier.ObjectType, ImcIdentifier.PrimaryId, ImcIdentifier.SecondaryId ),
                _             => throw new InvalidEnumArgumentException()
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
    }
}