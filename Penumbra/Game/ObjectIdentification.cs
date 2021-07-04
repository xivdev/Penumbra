using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using Penumbra.Game.Enums;
using Penumbra.Util;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace Penumbra.Game
{
    public class ObjectIdentification
    {
        private readonly List< (ulong, HashSet< Item >) >        _weapons;
        private readonly List< (ulong, HashSet< Item >) >        _equipment;
        private readonly Dictionary< string, HashSet< Action > > _actions;

        private static bool Add( IDictionary< ulong, HashSet< Item > > dict, ulong key, Item item )
        {
            if( dict.TryGetValue( key, out var list ) )
            {
                return list.Add( item );
            }

            dict[ key ] = new HashSet< Item > { item };
            return true;
        }

        private static ulong EquipmentKey( Item i )
        {
            var model   = ( ulong )( ( Lumina.Data.Parsing.Quad )i.ModelMain ).A;
            var variant = ( ulong )( ( Lumina.Data.Parsing.Quad )i.ModelMain ).B;
            var slot    = ( ulong )( ( EquipSlot )i.EquipSlotCategory.Row ).ToSlot();
            return ( model << 32 ) | ( slot << 16 ) | variant;
        }

        private static ulong WeaponKey( Item i, bool offhand )
        {
            var quad    = offhand ? ( Lumina.Data.Parsing.Quad )i.ModelSub : ( Lumina.Data.Parsing.Quad )i.ModelMain;
            var model   = ( ulong )quad.A;
            var type    = ( ulong )quad.B;
            var variant = ( ulong )quad.C;

            return ( model << 32 ) | ( type << 16 ) | variant;
        }

        private void AddAction( string key, Action action )
        {
            if( key.Length == 0 )
            {
                return;
            }

            key = key.ToLowerInvariant();
            if( _actions.TryGetValue( key, out var actions ) )
            {
                actions.Add( action );
            }
            else
            {
                _actions[ key ] = new HashSet< Action > { action };
            }
        }

        public ObjectIdentification( DalamudPluginInterface plugin )
        {
            var                                  items     = plugin.Data.GetExcelSheet< Item >( plugin.ClientState.ClientLanguage );
            SortedList< ulong, HashSet< Item > > weapons   = new();
            SortedList< ulong, HashSet< Item > > equipment = new();
            foreach( var item in items )
            {
                switch( ( EquipSlot )item.EquipSlotCategory.Row )
                {
                    case EquipSlot.MainHand:
                    case EquipSlot.Offhand:
                    case EquipSlot.BothHand:
                        if( item.ModelMain != 0 )
                        {
                            Add( weapons, WeaponKey( item, false ), item );
                        }

                        if( item.ModelSub != 0 )
                        {
                            Add( weapons, WeaponKey( item, true ), item );
                        }

                        break;
                    // Accessories
                    case EquipSlot.RingR:
                    case EquipSlot.Wrists:
                    case EquipSlot.Ears:
                    case EquipSlot.Neck:
                        Add( equipment, EquipmentKey( item ), item );
                        break;
                    // Equipment
                    case EquipSlot.Head:
                    case EquipSlot.Body:
                    case EquipSlot.Hands:
                    case EquipSlot.Legs:
                    case EquipSlot.Feet:
                    case EquipSlot.BodyHands:
                    case EquipSlot.BodyHandsLegsFeet:
                    case EquipSlot.BodyLegsFeet:
                    case EquipSlot.FullBody:
                    case EquipSlot.HeadBody:
                    case EquipSlot.LegsFeet:
                        Add( equipment, EquipmentKey( item ), item );
                        break;
                    default: continue;
                }
            }

            _actions = new Dictionary< string, HashSet< Action > >();
            foreach( var action in plugin.Data.GetExcelSheet< Action >( plugin.ClientState.ClientLanguage ) )
            {
                var startKey = action.AnimationStart?.Value?.Name?.Value?.Key.ToString() ?? string.Empty;
                var endKey   = action.AnimationEnd?.Value?.Key.ToString()                ?? string.Empty;
                var hitKey   = action.ActionTimelineHit?.Value?.Key.ToString()           ?? string.Empty;
                AddAction( startKey, action );
                AddAction( endKey, action );
                AddAction( hitKey, action );
            }

            _weapons   = weapons.Select( kvp => ( kvp.Key, kvp.Value ) ).ToList();
            _equipment = equipment.Select( kvp => ( kvp.Key, kvp.Value ) ).ToList();
        }

        private class Comparer : IComparer< (ulong, HashSet< Item >) >
        {
            public int Compare( (ulong, HashSet< Item >) x, (ulong, HashSet< Item >) y )
                => x.Item1.CompareTo( y.Item1 );
        }

        private static (int, int) FindIndexRange( List< (ulong, HashSet< Item >) > list, ulong key, ulong mask )
        {
            var maskedKey = key & mask;
            var idx       = list.BinarySearch( 0, list.Count, ( key, null! ), new Comparer() );
            if( idx < 0 )
            {
                if( ~idx == list.Count || maskedKey != ( list[ ~idx ].Item1 & mask ) )
                {
                    return ( -1, -1 );
                }

                idx = ~idx;
            }

            var endIdx = idx + 1;
            while( maskedKey == ( list[ endIdx ].Item1 & mask ) )
            {
                ++endIdx;
            }

            return ( idx, endIdx );
        }

        private void FindEquipment( IDictionary< string, object? > set, GameObjectInfo info )
        {
            var key  = ( ulong )info.PrimaryId << 32;
            var mask = 0xFFFF00000000ul;
            if( info.EquipSlot != EquipSlot.Unknown )
            {
                key  |= ( ulong )info.EquipSlot.ToSlot() << 16;
                mask |= 0xFFFF0000;
            }

            if( info.Variant != 0 )
            {
                key  |= info.Variant;
                mask |= 0xFFFF;
            }

            var (start, end) = FindIndexRange( _equipment, key, mask );
            if( start == -1 )
            {
                return;
            }

            for( ; start < end; ++start )
            {
                foreach( var item in _equipment[ start ].Item2 )
                {
                    set[ item.Name.ToString() ] = item;
                }
            }
        }

        private void FindWeapon( IDictionary< string, object? > set, GameObjectInfo info )
        {
            var key  = ( ulong )info.PrimaryId << 32;
            var mask = 0xFFFF00000000ul;
            if( info.SecondaryId != 0 )
            {
                key  |= ( ulong )info.SecondaryId << 16;
                mask |= 0xFFFF0000;
            }

            if( info.Variant != 0 )
            {
                key  |= info.Variant;
                mask |= 0xFFFF;
            }

            var (start, end) = FindIndexRange( _weapons, key, mask );
            if( start == -1 )
            {
                return;
            }

            for( ; start < end; ++start )
            {
                foreach( var item in _weapons[ start ].Item2 )
                {
                    set[ item.Name.ToString() ] = item;
                }
            }
        }


        private void IdentifyParsed( IDictionary< string, object? > set, GameObjectInfo info )
        {
            switch( info.ObjectType )
            {
                case ObjectType.Unknown:
                case ObjectType.LoadingScreen:
                case ObjectType.Map:
                case ObjectType.Interface:
                case ObjectType.Vfx:
                case ObjectType.World:
                case ObjectType.Housing:
                case ObjectType.DemiHuman:
                case ObjectType.Monster:
                case ObjectType.Icon:
                case ObjectType.Font:
                    // Don't do anything for these cases.
                    break;
                case ObjectType.Accessory:
                case ObjectType.Equipment:
                    FindEquipment( set, info );
                    break;
                case ObjectType.Weapon:
                    FindWeapon( set, info );
                    break;
                case ObjectType.Character:
                    if( info.CustomizationType == CustomizationType.Skin )
                    {
                        set[ "Customization: Player Skin" ] = null;
                    }
                    else
                    {
                        var (gender, race) = info.GenderRace.Split();
                        var customizationString =
                            $"Customization: {race} {gender}s {info.BodySlot} ({info.CustomizationType}) {info.PrimaryId}";
                        set[ customizationString ] = null;
                    }

                    break;

                default: throw new InvalidEnumArgumentException();
            }
        }

        private void IdentifyVfx( IDictionary< string, object? > set, GamePath path )
        {
            var key = GamePathParser.VfxToKey( path );
            if( key.Length == 0 || !_actions.TryGetValue( key, out var actions ) )
            {
                return;
            }

            foreach( var action in actions )
            {
                set[ $"Action: {action.Name}" ] = action;
            }
        }

        public void Identify( IDictionary< string, object? > set, GamePath path )
        {
            if( ( ( string )path ).EndsWith( ".pap" ) || ( ( string )path ).EndsWith( ".tmb" ) )
            {
                IdentifyVfx( set, path );
            }
            else
            {
                var info = GamePathParser.GetFileInfo( path );
                IdentifyParsed( set, info );
            }
        }
    }
}