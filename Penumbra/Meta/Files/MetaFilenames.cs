using System;
using Penumbra.Game.Enums;
using Penumbra.Util;

namespace Penumbra.Meta.Files
{
    // Contains all filenames for meta changes depending on their parameters.
    public static class MetaFileNames
    {
        public static GamePath Eqp()
            => GamePath.GenerateUnchecked( "chara/xls/equipmentparameter/equipmentparameter.eqp" );

        public static GamePath Gmp()
            => GamePath.GenerateUnchecked( "chara/xls/equipmentparameter/gimmickparameter.gmp" );

        public static GamePath Est( ObjectType type, EquipSlot equip, BodySlot slot )
        {
            return type switch
            {
                ObjectType.Equipment => equip switch
                {
                    EquipSlot.Body => GamePath.GenerateUnchecked( "chara/xls/charadb/extra_top.est" ),
                    EquipSlot.Head => GamePath.GenerateUnchecked( "chara/xls/charadb/extra_met.est" ),
                    _              => throw new NotImplementedException(),
                },
                ObjectType.Character => slot switch
                {
                    BodySlot.Hair => GamePath.GenerateUnchecked( "chara/xls/charadb/hairskeletontemplate.est" ),
                    BodySlot.Face => GamePath.GenerateUnchecked( "chara/xls/charadb/faceskeletontemplate.est" ),
                    _             => throw new NotImplementedException(),
                },
                _ => throw new NotImplementedException(),
            };
        }

        public static GamePath Imc( ObjectType type, ushort primaryId, ushort secondaryId )
        {
            return type switch
            {
                ObjectType.Accessory => GamePath.GenerateUnchecked( $"chara/accessory/a{primaryId:D4}/a{primaryId:D4}.imc" ),
                ObjectType.Equipment => GamePath.GenerateUnchecked( $"chara/equipment/e{primaryId:D4}/e{primaryId:D4}.imc" ),
                ObjectType.DemiHuman => GamePath.GenerateUnchecked(
                    $"chara/demihuman/d{primaryId:D4}/obj/equipment/e{secondaryId:D4}/e{secondaryId:D4}.imc" ),
                ObjectType.Monster => GamePath.GenerateUnchecked(
                    $"chara/monster/m{primaryId:D4}/obj/body/b{secondaryId:D4}/b{secondaryId:D4}.imc" ),
                ObjectType.Weapon => GamePath.GenerateUnchecked(
                    $"chara/weapon/w{primaryId:D4}/obj/body/b{secondaryId:D4}/b{secondaryId:D4}.imc" ),
                _ => throw new NotImplementedException(),
            };
        }

        public static GamePath Eqdp( ObjectType type, GenderRace gr )
        {
            return type switch
            {
                ObjectType.Accessory => GamePath.GenerateUnchecked( $"chara/xls/charadb/accessorydeformerparameter/c{gr.ToRaceCode()}.eqdp" ),
                ObjectType.Equipment => GamePath.GenerateUnchecked( $"chara/xls/charadb/equipmentdeformerparameter/c{gr.ToRaceCode()}.eqdp" ),
                _                    => throw new NotImplementedException(),
            };
        }

        public static GamePath Eqdp( EquipSlot slot, GenderRace gr )
        {
            return slot switch
            {
                EquipSlot.Head   => Eqdp( ObjectType.Equipment, gr ),
                EquipSlot.Body   => Eqdp( ObjectType.Equipment, gr ),
                EquipSlot.Feet   => Eqdp( ObjectType.Equipment, gr ),
                EquipSlot.Hands  => Eqdp( ObjectType.Equipment, gr ),
                EquipSlot.Legs   => Eqdp( ObjectType.Equipment, gr ),
                EquipSlot.Neck   => Eqdp( ObjectType.Accessory, gr ),
                EquipSlot.Ears   => Eqdp( ObjectType.Accessory, gr ),
                EquipSlot.Wrists => Eqdp( ObjectType.Accessory, gr ),
                EquipSlot.RingL  => Eqdp( ObjectType.Accessory, gr ),
                EquipSlot.RingR  => Eqdp( ObjectType.Accessory, gr ),
                _                => throw new NotImplementedException(),
            };
        }
    }
}