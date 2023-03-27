using System;
using System.Collections.Generic;
using System.Linq;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Mods;

public sealed partial class Mod
{
    public SortedList< string, object? > ChangedItems { get; } = new();
    public string LowerChangedItemsString { get; private set; } = string.Empty;

    internal void ComputeChangedItems()
    {
        ChangedItems.Clear();
        foreach( var gamePath in AllRedirects )
        {
            Penumbra.Identifier.Identify( ChangedItems, gamePath.ToString() );
        }

        foreach( var manip in AllManipulations )
        {
            ComputeChangedItems( ChangedItems, manip );
        }

        LowerChangedItemsString = string.Join( "\0", ChangedItems.Keys.Select( k => k.ToLowerInvariant() ) );
    }

    public static void ComputeChangedItems( SortedList< string, object? > changedItems, MetaManipulation manip )
    {
        switch( manip.ManipulationType )
        {
            case MetaManipulation.Type.Imc:
                switch( manip.Imc.ObjectType )
                {
                    case ObjectType.Equipment:
                    case ObjectType.Accessory:
                        Penumbra.Identifier.Identify( changedItems,
                            GamePaths.Equipment.Mtrl.Path( manip.Imc.PrimaryId, GenderRace.MidlanderMale, manip.Imc.EquipSlot, manip.Imc.Variant, "a" ) );
                        break;
                    case ObjectType.Weapon:
                        Penumbra.Identifier.Identify( changedItems, GamePaths.Weapon.Mtrl.Path( manip.Imc.PrimaryId, manip.Imc.SecondaryId, manip.Imc.Variant, "a" ) );
                        break;
                    case ObjectType.DemiHuman:
                        Penumbra.Identifier.Identify( changedItems,
                            GamePaths.DemiHuman.Mtrl.Path( manip.Imc.PrimaryId, manip.Imc.SecondaryId, manip.Imc.EquipSlot, manip.Imc.Variant, "a" ) );
                        break;
                    case ObjectType.Monster:
                        Penumbra.Identifier.Identify( changedItems, GamePaths.Monster.Mtrl.Path( manip.Imc.PrimaryId, manip.Imc.SecondaryId, manip.Imc.Variant, "a" ) );
                        break;
                }

                break;
            case MetaManipulation.Type.Eqdp:
                Penumbra.Identifier.Identify( changedItems,
                    GamePaths.Equipment.Mdl.Path( manip.Eqdp.SetId, Names.CombinedRace( manip.Eqdp.Gender, manip.Eqdp.Race ), manip.Eqdp.Slot ) );
                break;
            case MetaManipulation.Type.Eqp:
                Penumbra.Identifier.Identify( changedItems, GamePaths.Equipment.Mdl.Path( manip.Eqp.SetId, GenderRace.MidlanderMale, manip.Eqp.Slot ) );
                break;
            case MetaManipulation.Type.Est:
                switch( manip.Est.Slot )
                {
                    case EstManipulation.EstType.Hair:
                        changedItems.TryAdd( $"Customization: {manip.Est.Race} {manip.Est.Gender} Hair (Hair) {manip.Est.SetId}", null );
                        break;
                    case EstManipulation.EstType.Face:
                        changedItems.TryAdd( $"Customization: {manip.Est.Race} {manip.Est.Gender} Face (Face) {manip.Est.SetId}", null );
                        break;
                    case EstManipulation.EstType.Body:
                        Penumbra.Identifier.Identify( changedItems,
                            GamePaths.Equipment.Mdl.Path( manip.Est.SetId, Names.CombinedRace( manip.Est.Gender, manip.Est.Race ), EquipSlot.Body ) );
                        break;
                    case EstManipulation.EstType.Head:
                        Penumbra.Identifier.Identify( changedItems,
                            GamePaths.Equipment.Mdl.Path( manip.Est.SetId, Names.CombinedRace( manip.Est.Gender, manip.Est.Race ), EquipSlot.Head ) );
                        break;
                }

                break;
            case MetaManipulation.Type.Gmp:
                Penumbra.Identifier.Identify( changedItems, GamePaths.Equipment.Mdl.Path( manip.Gmp.SetId, GenderRace.MidlanderMale, EquipSlot.Head ) );
                break;
            case MetaManipulation.Type.Rsp:
                changedItems.TryAdd( $"{manip.Rsp.SubRace.ToName()} {manip.Rsp.Attribute.ToFullString()}", null );
                break;
        }
    }
}