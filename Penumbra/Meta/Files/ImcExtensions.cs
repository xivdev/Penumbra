using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Lumina.Data.Files;
using Penumbra.GameData.Enums;

namespace Penumbra.Meta.Files
{
    public class InvalidImcVariantException : ArgumentOutOfRangeException
    {
        public InvalidImcVariantException()
            : base( "Trying to manipulate invalid variant." )
        { }
    }

    // Imc files are already supported in Lumina, but changing the provided data is not supported.
    // We use reflection and extension methods to support changing the data of a given Imc file.
    public static class ImcExtensions
    {
        public static ulong ToInteger( this ImcFile.ImageChangeData imc )
        {
            ulong ret = imc.MaterialId;
            ret |= ( ulong )imc.DecalId                     << 8;
            ret |= ( ulong )imc.AttributeMask               << 16;
            ret |= ( ulong )imc.SoundId                     << 16;
            ret |= ( ulong )imc.VfxId                       << 32;
            ret |= ( ulong )imc.ActualMaterialAnimationId() << 40;
            return ret;
        }

        public static byte ActualMaterialAnimationId( this ImcFile.ImageChangeData imc )
        {
            var tmp = imc.GetType().GetField( "_MaterialAnimationIdMask",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance );
            return ( byte )( tmp?.GetValue( imc ) ?? 0 );
        }

        public static ImcFile.ImageChangeData FromValues( byte materialId, byte decalId, ushort attributeMask, byte soundId, byte vfxId,
            byte materialAnimationId )
        {
            var ret = new ImcFile.ImageChangeData()
            {
                DecalId    = decalId,
                MaterialId = materialId,
                VfxId      = vfxId,
            };
            ret.GetType().GetField( "_AttributeAndSound",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance )!
               .SetValue( ret, ( ushort )( ( attributeMask & 0x3FF ) | ( soundId << 10 ) ) );
            ret.GetType().GetField( "_AttributeAndSound",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance )!.SetValue( ret, materialAnimationId );
            return ret;
        }

        public static bool Equal( this ImcFile.ImageChangeData lhs, ImcFile.ImageChangeData rhs )
            => lhs.MaterialId           == rhs.MaterialId
             && lhs.DecalId             == rhs.DecalId
             && lhs.AttributeMask       == rhs.AttributeMask
             && lhs.SoundId             == rhs.SoundId
             && lhs.VfxId               == rhs.VfxId
             && lhs.MaterialAnimationId == rhs.MaterialAnimationId;

        private static void WriteBytes( this ImcFile.ImageChangeData variant, BinaryWriter bw )
        {
            bw.Write( variant.MaterialId );
            bw.Write( variant.DecalId );
            bw.Write( ( ushort )( variant.AttributeMask | variant.SoundId ) );
            bw.Write( variant.VfxId );
            bw.Write( variant.ActualMaterialAnimationId() );
        }

        public static byte[] WriteBytes( this ImcFile file )
        {
            var       parts    = file.PartMask == 31 ? 5 : 1;
            var       dataSize = 4 + 6 * parts * ( 1 + file.Count );
            using var mem      = new MemoryStream( dataSize );
            using var bw       = new BinaryWriter( mem );

            bw.Write( file.Count );
            bw.Write( file.PartMask );
            for( var i = 0; i < parts; ++i )
            {
                file.GetDefaultVariant( i ).WriteBytes( bw );
            }

            for( var i = 0; i < file.Count; ++i )
            {
                for( var j = 0; j < parts; ++j )
                {
                    file.GetVariant( j, i ).WriteBytes( bw );
                }
            }

            return mem.ToArray();
        }

        public static ref ImcFile.ImageChangeData GetValue( this ImcFile file, MetaManipulation manipulation )
        {
            var parts = file.GetParts();
            var imc   = manipulation.ImcIdentifier;
            var idx   = 0;
            if( imc.ObjectType == ObjectType.Equipment || imc.ObjectType == ObjectType.Accessory )
            {
                idx = imc.EquipSlot switch
                {
                    EquipSlot.Head   => 0,
                    EquipSlot.Ears   => 0,
                    EquipSlot.Body   => 1,
                    EquipSlot.Neck   => 1,
                    EquipSlot.Hands  => 2,
                    EquipSlot.Wrists => 2,
                    EquipSlot.Legs   => 3,
                    EquipSlot.RFinger  => 3,
                    EquipSlot.Feet   => 4,
                    EquipSlot.LFinger  => 4,
                    _                => throw new InvalidEnumArgumentException(),
                };
            }

            if( imc.Variant == 0 )
            {
                return ref parts[ idx ].DefaultVariant;
            }

            if( imc.Variant > parts[ idx ].Variants.Length )
            {
                throw new InvalidImcVariantException();
            }

            return ref parts[ idx ].Variants[ imc.Variant - 1 ];
        }

        public static ImcFile Clone( this ImcFile file )
        {
            var ret = new ImcFile
            {
                Count    = file.Count,
                PartMask = file.PartMask,
            };
            var parts = file.GetParts().Select( p => new ImcFile.ImageChangeParts()
            {
                DefaultVariant = p.DefaultVariant,
                Variants       = ( ImcFile.ImageChangeData[] )p.Variants.Clone(),
            } ).ToArray();
            var prop = ret.GetType().GetField( "Parts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance );
            prop!.SetValue( ret, parts );
            return ret;
        }
    }
}