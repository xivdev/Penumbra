using System;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Types;

// Read the customization data regarding weapons and displayable equipment from an actor struct.
// Stores the data in a 56 bytes, i.e. 7 longs for easier comparison.
namespace Penumbra.GameData.Structs
{
    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public class CharacterEquipment
    {
        public const int MainWeaponOffset = 0x0F08;
        public const int OffWeaponOffset  = 0x0F70;
        public const int EquipmentOffset  = 0x1040;
        public const int EquipmentSlots   = 10;
        public const int WeaponSlots      = 2;

        public CharacterWeapon MainHand;
        public CharacterWeapon OffHand;
        public CharacterArmor  Head;
        public CharacterArmor  Body;
        public CharacterArmor  Hands;
        public CharacterArmor  Legs;
        public CharacterArmor  Feet;
        public CharacterArmor  Ears;
        public CharacterArmor  Neck;
        public CharacterArmor  Wrists;
        public CharacterArmor  RFinger;
        public CharacterArmor  LFinger;
        public ushort      IsSet; // Also fills struct size to 56, a multiple of 8.

        public CharacterEquipment()
            => Clear();

        public CharacterEquipment( Character actor )
            : this( actor.Address )
        { }

        public override string ToString()
            => IsSet == 0
                ? "(Not Set)"
                : $"({MainHand}) | ({OffHand}) | ({Head}) | ({Body}) | ({Hands}) | ({Legs}) | "
              + $"({Feet}) | ({Ears}) | ({Neck}) | ({Wrists}) | ({LFinger}) | ({RFinger})";

        public bool Equal( Character rhs )
            => CompareData( new CharacterEquipment( rhs ) );

        public bool Equal( CharacterEquipment rhs )
            => CompareData( rhs );

        public bool CompareAndUpdate( Character rhs )
            => CompareAndOverwrite( new CharacterEquipment( rhs ) );

        public bool CompareAndUpdate( CharacterEquipment rhs )
            => CompareAndOverwrite( rhs );

        private unsafe CharacterEquipment( IntPtr actorAddress )
        {
            IsSet = 1;
            var actorPtr = ( byte* )actorAddress.ToPointer();
            fixed( CharacterWeapon* main = &MainHand, off = &OffHand )
            {
                Buffer.MemoryCopy( actorPtr + MainWeaponOffset, main, sizeof( CharacterWeapon ), sizeof( CharacterWeapon ) );
                Buffer.MemoryCopy( actorPtr + OffWeaponOffset, off, sizeof( CharacterWeapon ), sizeof( CharacterWeapon ) );
            }

            fixed( CharacterArmor* equipment = &Head )
            {
                Buffer.MemoryCopy( actorPtr + EquipmentOffset, equipment, EquipmentSlots * sizeof( CharacterArmor ),
                    EquipmentSlots                                                       * sizeof( CharacterArmor ) );
            }
        }

        public unsafe void Clear()
        {
            fixed( CharacterWeapon* main = &MainHand )
            {
                var structSizeEights = ( 2 + EquipmentSlots * sizeof( CharacterArmor ) + WeaponSlots * sizeof( CharacterWeapon ) ) / 8;
                for( ulong* ptr = ( ulong* )main, end = ptr + structSizeEights; ptr != end; ++ptr )
                {
                    *ptr = 0;
                }
            }
        }

        private unsafe bool CompareAndOverwrite( CharacterEquipment rhs )
        {
            var structSizeEights = ( 2 + EquipmentSlots * sizeof( CharacterArmor ) + WeaponSlots * sizeof( CharacterWeapon ) ) / 8;
            var ret              = true;
            fixed( CharacterWeapon* data1 = &MainHand, data2 = &rhs.MainHand )
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

        private unsafe bool CompareData( CharacterEquipment rhs )
        {
            var structSizeEights = ( 2 + EquipmentSlots * sizeof( CharacterArmor ) + WeaponSlots * sizeof( CharacterWeapon ) ) / 8;
            fixed( CharacterWeapon* data1 = &MainHand, data2 = &rhs.MainHand )
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

        public unsafe void WriteBytes( byte[] array, int offset = 0 )
        {
            fixed( CharacterWeapon* data = &MainHand )
            {
                Marshal.Copy( new IntPtr( data ), array, offset, 56 );
            }
        }

        public byte[] ToBytes()
        {
            var ret = new byte[56];
            WriteBytes( ret );
            return ret;
        }

        public unsafe void FromBytes( byte[] array, int offset = 0 )
        {
            fixed( CharacterWeapon* data = &MainHand )
            {
                Marshal.Copy( array, offset, new IntPtr( data ), 56 );
            }
        }
    }
}