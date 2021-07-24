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
        private const int MainWeaponOffset = 0x0F08;
        private const int OffWeaponOffset  = 0x0F70;
        private const int EquipmentOffset  = 0x1040;
        private const int EquipmentSlots   = 10;
        private const int WeaponSlots      = 2;

        public readonly ActorWeapon Mainhand;
        public readonly ActorWeapon Offhand;
        public readonly ActorEquip  Head;
        public readonly ActorEquip  Body;
        public readonly ActorEquip  Hands;
        public readonly ActorEquip  Legs;
        public readonly ActorEquip  Feet;
        public readonly ActorEquip  Ear;
        public readonly ActorEquip  Neck;
        public readonly ActorEquip  Wrist;
        public readonly ActorEquip  RFinger;
        public readonly ActorEquip  LFinger;
        public readonly ushort      IsSet; // Also fills struct size to 56, a multiple of 8.

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
            fixed( ActorWeapon* main = &Mainhand, off = &Offhand )
            {
                Buffer.MemoryCopy( actorPtr + MainWeaponOffset, main, sizeof( ActorWeapon ), sizeof( ActorWeapon ) );
                Buffer.MemoryCopy( actorPtr + OffWeaponOffset, off, sizeof( ActorWeapon ), sizeof( ActorWeapon ) );
            }

            fixed( ActorEquip* equipment = &Head )
            {
                Buffer.MemoryCopy( actorPtr + EquipmentOffset, equipment, EquipmentSlots * sizeof( ActorEquip ),
                    EquipmentSlots                                                       * sizeof( ActorEquip ) );
            }
        }

        public unsafe void Clear()
        {
            fixed( ActorWeapon* main = &Mainhand )
            {
                var structSizeEights = ( 2 + EquipmentSlots * sizeof( ActorEquip ) + WeaponSlots * sizeof( ActorWeapon ) ) / 8;
                for( ulong* ptr = ( ulong* )main, end = ptr + structSizeEights; ptr != end; ++ptr )
                {
                    *ptr = 0;
                }
            }
        }

        private unsafe bool CompareAndOverwrite( CharEquipment rhs )
        {
            var structSizeEights = ( 2 + EquipmentSlots * sizeof( ActorEquip ) + WeaponSlots * sizeof( ActorWeapon ) ) / 8;
            var ret              = true;
            fixed( ActorWeapon* data1 = &Mainhand, data2 = &rhs.Mainhand )
            {
                var ptr1 = ( ulong* )data1;
                var ptr2 = ( ulong* )data2;
                for( var end = ptr1 + structSizeEights; ptr1 != end; ++ptr1, ++ptr2 )
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
            var structSizeEights = ( 2 + EquipmentSlots * sizeof( ActorEquip ) + WeaponSlots * sizeof( ActorWeapon ) ) / 8;
            fixed( ActorWeapon* data1 = &Mainhand, data2 = &rhs.Mainhand )
            {
                var ptr1 = ( ulong* )data1;
                var ptr2 = ( ulong* )data2;
                for( var end = ptr1 + structSizeEights; ptr1 != end; ++ptr1, ++ptr2 )
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