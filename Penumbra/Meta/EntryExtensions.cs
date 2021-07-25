using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Penumbra.Meta
{
    public static class EqdpEntryExtensions
    {
        public static bool Apply( this ref EqdpEntry entry, MetaManipulation manipulation )
        {
            if( manipulation.Type != MetaType.Eqdp )
            {
                return false;
            }

            var mask   = Eqdp.Mask( manipulation.EqdpIdentifier.Slot );
            var result = ( entry & ~mask ) | manipulation.EqdpValue;
            var ret    = result == entry;
            entry = result;
            return ret;
        }

        public static EqdpEntry Reduce( this EqdpEntry entry, EquipSlot slot )
            => entry & Eqdp.Mask( slot );
    }


    public static class EqpEntryExtensions
    {
        public static bool Apply( this ref EqpEntry entry, MetaManipulation manipulation )
        {
            if( manipulation.Type != MetaType.Eqp )
            {
                return false;
            }

            var mask   = Eqp.Mask( manipulation.EqpIdentifier.Slot );
            var result = ( entry & ~mask ) | manipulation.EqpValue;
            var ret    = result != entry;
            entry = result;
            return ret;
        }

        public static EqpEntry Reduce( this EqpEntry entry, EquipSlot slot )
            => entry & Eqp.Mask( slot );
    }

    public static class GmpEntryExtension
    {
        public static GmpEntry Apply( this GmpEntry entry, MetaManipulation manipulation )
        {
            if( manipulation.Type != MetaType.Gmp )
            {
                return entry;
            }

            entry.Value = manipulation.GmpValue.Value;
            return entry;
        }
    }
}