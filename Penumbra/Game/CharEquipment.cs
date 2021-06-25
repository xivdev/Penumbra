using System;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Actors.Types;

// Read the customization data regarding weapons and displayable equipment from an actor struct.
// Stores the data in a 56 bytes, i.e. 7 longs for easier comparison.
namespace Penumbra.Game
{
    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public class CharEquipment
    {
        [StructLayout( LayoutKind.Sequential, Pack = 1 )]
        private readonly struct Weapon
        {
            public readonly ushort _1;
            public readonly ushort _2;
            public readonly ushort _3;
            public readonly byte   _4;

            public override string ToString()
                => $"{_1},{_2},{_3},{_4}";
        }

        [StructLayout( LayoutKind.Sequential, Pack = 1 )]
        private readonly struct Equip
        {
            public readonly ushort _1;
            public readonly byte   _2;
            public readonly byte   _3;

            public override string ToString()
                => $"{_1},{_2},{_3}";
        }

        private const int MainWeaponOffset = 0x0F08;
        private const int OffWeaponOffset  = 0x0F70;
        private const int EquipmentOffset  = 0x1040;
        private const int EquipmentSlots   = 10;
        private const int WeaponSlots      = 2;

        private readonly ushort IsSet; // Also fills struct size to 56, a multiple of 8.
        private readonly Weapon Mainhand;
        private readonly Weapon Offhand;
        private readonly Equip  Head;
        private readonly Equip  Body;
        private readonly Equip  Hands;
        private readonly Equip  Legs;
        private readonly Equip  Feet;
        private readonly Equip  Ear;
        private readonly Equip  Neck;
        private readonly Equip  Wrist;
        private readonly Equip  LFinger;
        private readonly Equip  RFinger;

        public CharEquipment()
            => Clear();

        public CharEquipment( Actor actor )
            : this( actor.Address )
        { }

        public override string ToString()
            => IsSet == 0
                ? "(Not Set)"
                : $"({Mainhand}) | ({Offhand}) | ({Head}) | ({Body}) | ({Hands}) | ({Legs}) | "
              + $"({Feet}) | ({Ear}) | ({Neck}) | ({Wrist}) | ({LFinger}) | ({RFinger})";

        public bool Equal( Actor rhs )
            => CompareData( new CharEquipment( rhs ) );

        public bool Equal( CharEquipment rhs )
            => CompareData( rhs );

        public bool CompareAndUpdate( Actor rhs )
            => CompareAndOverwrite( new CharEquipment( rhs ) );

        public bool CompareAndUpdate( CharEquipment rhs )
            => CompareAndOverwrite( rhs );

        private unsafe CharEquipment( IntPtr actorAddress )
        {
            IsSet = 1;
            var actorPtr = ( byte* )actorAddress.ToPointer();
            fixed( Weapon* main = &Mainhand, off = &Offhand )
            {
                Buffer.MemoryCopy( actorPtr + MainWeaponOffset, main, sizeof( Weapon ), sizeof( Weapon ) );
                Buffer.MemoryCopy( actorPtr + OffWeaponOffset, off, sizeof( Weapon ), sizeof( Weapon ) );
            }

            fixed( Equip* equipment = &Head )
            {
                Buffer.MemoryCopy( actorPtr + EquipmentOffset, equipment, EquipmentSlots * sizeof( Equip ), EquipmentSlots * sizeof( Equip ) );
            }
        }

        public unsafe void Clear()
        {
            fixed( Weapon* main = &Mainhand )
            {
                var structSizeEights = ( EquipmentSlots * sizeof( Equip ) + WeaponSlots * sizeof( Weapon ) ) / 8;
                for( ulong* ptr = ( ulong* )main, end = ptr + structSizeEights; ptr != end; ++ptr )
                {
                    *ptr = 0;
                }
            }
        }

        private unsafe bool CompareAndOverwrite( CharEquipment rhs )
        {
            var structSizeHalf = ( EquipmentSlots * sizeof( Equip ) + WeaponSlots * sizeof( Weapon ) ) / 8;
            var ret            = true;
            fixed( Weapon* data1 = &Mainhand, data2 = &rhs.Mainhand )
            {
                var ptr1 = ( ulong* )data1;
                var ptr2 = ( ulong* )data2;
                for( var end = ptr1 + structSizeHalf; ptr1 != end; ++ptr1, ++ptr2 )
                {
                    if( *ptr1 != *ptr2 )
                    {
                        *ptr1 = *ptr2;
                        ret   = false;
                    }
                }
            }

            return ret;
        }

        private unsafe bool CompareData( CharEquipment rhs )
        {
            var structSizeHalf = ( EquipmentSlots * sizeof( Equip ) + WeaponSlots * sizeof( Weapon ) ) / 8;
            fixed( Weapon* data1 = &Mainhand, data2 = &rhs.Mainhand )
            {
                var ptr1 = ( ulong* )data1;
                var ptr2 = ( ulong* )data2;
                for( var end = ptr1 + structSizeHalf; ptr1 != end; ++ptr1, ++ptr2 )
                {
                    if( *ptr1 != *ptr2 )
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}