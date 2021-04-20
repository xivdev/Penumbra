using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Lumina.Data.Files;
using Penumbra.Game;
using Penumbra.Mods;

namespace Penumbra.MetaData
{
    public class InvalidImcVariantException : ArgumentOutOfRangeException
    {
        public InvalidImcVariantException()
            : base("Trying to manipulate invalid variant.")
        { }
    }

    public static class ImcExtensions
    {
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
            bw.Write( variant.MaterialAnimationId );
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
                    EquipSlot.RingR  => 3,
                    EquipSlot.Feet   => 4,
                    EquipSlot.RingL  => 4,
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
            var parts = file.GetParts().Select( P => new ImcFile.ImageChangeParts()
            {
                DefaultVariant = P.DefaultVariant,
                Variants       = ( ImcFile.ImageChangeData[] )P.Variants.Clone(),
            } ).ToArray();
            var prop = ret.GetType().GetField( "Parts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance );
            prop!.SetValue( ret, parts );
            return ret;
        }
    }
}