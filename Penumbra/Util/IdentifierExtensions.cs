using OtterGui.Classes;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Editor;
using Penumbra.Mods.SubMods;

namespace Penumbra.Util;

public static class IdentifierExtensions
{
    /// <summary> Compute the items changed by a given meta manipulation and put them into the changedItems dictionary. </summary>
    public static void MetaChangedItems(this ObjectIdentification identifier, IDictionary<string, object?> changedItems,
        MetaManipulation manip)
    {
        switch (manip.ManipulationType)
        {
            case MetaManipulation.Type.Imc:
                switch (manip.Imc.ObjectType)
                {
                    case ObjectType.Equipment:
                    case ObjectType.Accessory:
                        identifier.Identify(changedItems,
                            GamePaths.Equipment.Mtrl.Path(manip.Imc.PrimaryId, GenderRace.MidlanderMale, manip.Imc.EquipSlot, manip.Imc.Variant,
                                "a"));
                        break;
                    case ObjectType.Weapon:
                        identifier.Identify(changedItems,
                            GamePaths.Weapon.Mtrl.Path(manip.Imc.PrimaryId, manip.Imc.SecondaryId, manip.Imc.Variant, "a"));
                        break;
                    case ObjectType.DemiHuman:
                        identifier.Identify(changedItems,
                            GamePaths.DemiHuman.Mtrl.Path(manip.Imc.PrimaryId, manip.Imc.SecondaryId, manip.Imc.EquipSlot, manip.Imc.Variant,
                                "a"));
                        break;
                    case ObjectType.Monster:
                        identifier.Identify(changedItems,
                            GamePaths.Monster.Mtrl.Path(manip.Imc.PrimaryId, manip.Imc.SecondaryId, manip.Imc.Variant, "a"));
                        break;
                }

                break;
            case MetaManipulation.Type.Eqdp:
                identifier.Identify(changedItems,
                    GamePaths.Equipment.Mdl.Path(manip.Eqdp.SetId, Names.CombinedRace(manip.Eqdp.Gender, manip.Eqdp.Race), manip.Eqdp.Slot));
                break;
            case MetaManipulation.Type.Eqp:
                identifier.Identify(changedItems, GamePaths.Equipment.Mdl.Path(manip.Eqp.SetId, GenderRace.MidlanderMale, manip.Eqp.Slot));
                break;
            case MetaManipulation.Type.Est:
                switch (manip.Est.Slot)
                {
                    case EstManipulation.EstType.Hair:
                        changedItems.TryAdd($"Customization: {manip.Est.Race} {manip.Est.Gender} Hair (Hair) {manip.Est.SetId}", null);
                        break;
                    case EstManipulation.EstType.Face:
                        changedItems.TryAdd($"Customization: {manip.Est.Race} {manip.Est.Gender} Face (Face) {manip.Est.SetId}", null);
                        break;
                    case EstManipulation.EstType.Body:
                        identifier.Identify(changedItems,
                            GamePaths.Equipment.Mdl.Path(manip.Est.SetId, Names.CombinedRace(manip.Est.Gender, manip.Est.Race),
                                EquipSlot.Body));
                        break;
                    case EstManipulation.EstType.Head:
                        identifier.Identify(changedItems,
                            GamePaths.Equipment.Mdl.Path(manip.Est.SetId, Names.CombinedRace(manip.Est.Gender, manip.Est.Race),
                                EquipSlot.Head));
                        break;
                }

                break;
            case MetaManipulation.Type.Gmp:
                identifier.Identify(changedItems, GamePaths.Equipment.Mdl.Path(manip.Gmp.SetId, GenderRace.MidlanderMale, EquipSlot.Head));
                break;
            case MetaManipulation.Type.Rsp:
                changedItems.TryAdd($"{manip.Rsp.SubRace.ToName()} {manip.Rsp.Attribute.ToFullString()}", null);
                break;
            case MetaManipulation.Type.GlobalEqp:
                var path = manip.GlobalEqp.Type switch
                {
                    GlobalEqpType.DoNotHideEarrings => GamePaths.Accessory.Mdl.Path(manip.GlobalEqp.Condition, GenderRace.MidlanderMale,
                        EquipSlot.Ears),
                    GlobalEqpType.DoNotHideNecklace => GamePaths.Accessory.Mdl.Path(manip.GlobalEqp.Condition, GenderRace.MidlanderMale,
                        EquipSlot.Neck),
                    GlobalEqpType.DoNotHideBracelets => GamePaths.Accessory.Mdl.Path(manip.GlobalEqp.Condition, GenderRace.MidlanderMale,
                        EquipSlot.Wrists),
                    GlobalEqpType.DoNotHideRingR => GamePaths.Accessory.Mdl.Path(manip.GlobalEqp.Condition, GenderRace.MidlanderMale,
                        EquipSlot.RFinger),
                    GlobalEqpType.DoNotHideRingL => GamePaths.Accessory.Mdl.Path(manip.GlobalEqp.Condition, GenderRace.MidlanderMale,
                        EquipSlot.LFinger),
                    GlobalEqpType.DoNotHideHrothgarHats => string.Empty,
                    GlobalEqpType.DoNotHideVieraHats    => string.Empty,
                    _                                         => string.Empty,
                };
                if (path.Length > 0)
                    identifier.Identify(changedItems, path);
                break;
        }
    }

    public static void AddChangedItems(this ObjectIdentification identifier, IModDataContainer container,
        IDictionary<string, object?> changedItems)
    {
        foreach (var gamePath in container.Files.Keys.Concat(container.FileSwaps.Keys))
            identifier.Identify(changedItems, gamePath.ToString());

        foreach (var manip in container.Manipulations)
            MetaChangedItems(identifier, changedItems, manip);
    }

    public static void RemoveMachinistOffhands(this SortedList<string, object?> changedItems)
    {
        for (var i = 0; i < changedItems.Count; i++)
        {
            {
                var value = changedItems.Values[i];
                if (value is EquipItem { Type: FullEquipType.GunOff })
                    changedItems.RemoveAt(i--);
            }
        }
    }

    public static void RemoveMachinistOffhands(this SortedList<string, (SingleArray<IMod>, object?)> changedItems)
    {
        for (var i = 0; i < changedItems.Count; i++)
        {
            {
                var value = changedItems.Values[i].Item2;
                if (value is EquipItem { Type: FullEquipType.GunOff })
                    changedItems.RemoveAt(i--);
            }
        }
    }
}
