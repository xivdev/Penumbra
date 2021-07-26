using System;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Actors.Types;
using Penumbra.GameData.Enums;

// Read the customization data regarding weapons and displayable equipment from an actor struct.
// Stores the data in a 56 bytes, i.e. 7 longs for easier comparison.
namespace Penumbra.GameData.Structs
{
    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public class ActorEquipment
    {
        public const int MainWeaponOffset = 0x0F08;
        public const int OffWeaponOffset  = 0x0F70;
        public const int EquipmentOffset  = 0x1040;
        public const int EquipmentSlots   = 10;
        public const int WeaponSlots      = 2;

        public ActorWeapon    MainHand;
        public ActorWeapon    OffHand;
        public ActorArmor     Head;
        public ActorArmor     Body;
        public ActorArmor     Hands;
        public ActorArmor     Legs;
        public ActorArmor     Feet;
        public ActorArmor     Ears;
        public ActorArmor     Neck;
        public ActorArmor     Wrists;
        public ActorArmor     RFinger;
        public ActorArmor     LFinger;
        public ushort         IsSet; // Also fills struct size to 56, a multiple of 8.

        public ActorEquipment()
            => Clear();

        public ActorEquipment( Actor actor )
            : this( actor.Address )
        { }

        public override string ToString()
            => IsSet == 0
                ? "(Not Set)"
                : $"({MainHand}) | ({OffHand}) | ({Head}) | ({Body}) | ({Hands}) | ({Legs}) | "
              + $"({Feet}) | ({Ears}) | ({Neck}) | ({Wrists}) | ({LFinger}) | ({RFinger})";

        public bool Equal( Actor rhs )
            => CompareData( new ActorEquipment( rhs ) );

        public bool Equal( ActorEquipment rhs )
            => CompareData( rhs );

        public bool CompareAndUpdate( Actor rhs )
            => CompareAndOverwrite( new ActorEquipment( rhs ) );

        public bool CompareAndUpdate( ActorEquipment rhs )
            => CompareAndOverwrite( rhs );

        private unsafe ActorEquipment( IntPtr actorAddress )
        {
            IsSet = 1;
            var actorPtr = ( byte* )actorAddress.ToPointer();
            fixed( ActorWeapon* main = &MainHand, off = &OffHand )
            {
                Buffer.MemoryCopy( actorPtr + MainWeaponOffset, main, sizeof( ActorWeapon ), sizeof( ActorWeapon ) );
                Buffer.MemoryCopy( actorPtr + OffWeaponOffset, off, sizeof( ActorWeapon ), sizeof( ActorWeapon ) );
            }

            fixed( ActorArmor* equipment = &Head )
            {
                Buffer.MemoryCopy( actorPtr + EquipmentOffset, equipment, EquipmentSlots * sizeof( ActorArmor ),
                    EquipmentSlots                                                       * sizeof( ActorArmor ) );
            }
        }

        public unsafe void Clear()
        {
            fixed( ActorWeapon* main = &MainHand )
            {
                var structSizeEights = ( 2 + EquipmentSlots * sizeof( ActorArmor ) + WeaponSlots * sizeof( ActorWeapon ) ) / 8;
                for( ulong* ptr = ( ulong* )main, end = ptr + structSizeEights; ptr != end; ++ptr )
                {
                    *ptr = 0;
                }
            }
        }

        private unsafe bool CompareAndOverwrite( ActorEquipment rhs )
        {
            var structSizeEights = ( 2 + EquipmentSlots * sizeof( ActorArmor ) + WeaponSlots * sizeof( ActorWeapon ) ) / 8;
            var ret              = true;
            fixed( ActorWeapon* data1 = &MainHand, data2 = &rhs.MainHand )
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

        private unsafe bool CompareData( ActorEquipment rhs )
        {
            var structSizeEights = ( 2 + EquipmentSlots * sizeof( ActorArmor ) + WeaponSlots * sizeof( ActorWeapon ) ) / 8;
            fixed( ActorWeapon* data1 = &MainHand, data2 = &rhs.MainHand )
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