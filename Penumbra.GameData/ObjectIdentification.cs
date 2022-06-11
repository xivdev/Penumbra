using Dalamud;
using Dalamud.Data;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.GameData.Util;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace Penumbra.GameData;

internal class ObjectIdentification : IObjectIdentifier
{
    public static    DataManager?                            DataManager = null!;
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

    public ObjectIdentification( DataManager dataManager, ClientLanguage clientLanguage )
    {
        DataManager = dataManager;
        var                                  items     = dataManager.GetExcelSheet< Item >( clientLanguage )!;
        SortedList< ulong, HashSet< Item > > weapons   = new();
        SortedList< ulong, HashSet< Item > > equipment = new();
        foreach( var item in items )
        {
            switch( ( EquipSlot )item.EquipSlotCategory.Row )
            {
                case EquipSlot.MainHand:
                case EquipSlot.OffHand:
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
                case EquipSlot.RFinger:
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
        foreach( var action in dataManager.GetExcelSheet< Action >( clientLanguage )!
                   .Where( a => a.Name.ToString().Any() ) )
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
        while( endIdx < list.Count && maskedKey == ( list[ endIdx ].Item1 & mask ) )
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

    private static void AddCounterString( IDictionary< string, object? > set, string data )
    {
        if( set.TryGetValue( data, out var obj ) && obj is int counter )
        {
            set[ data ] = counter + 1;
        }
        else
        {
            set[ data ] = 1;
        }
    }

    private void IdentifyParsed( IDictionary< string, object? > set, GameObjectInfo info )
    {
        switch( info.ObjectType )
        {
            case ObjectType.Unknown:
                switch( info.FileType )
                {
                    case FileType.Sound:
                        AddCounterString( set, FileType.Sound.ToString() );
                        break;
                    case FileType.Animation:
                    case FileType.Pap:
                        AddCounterString( set, FileType.Animation.ToString() );
                        break;
                    case FileType.Shader:
                        AddCounterString( set, FileType.Shader.ToString() );
                        break;
                }

                break;
            case ObjectType.LoadingScreen:
            case ObjectType.Map:
            case ObjectType.Interface:
            case ObjectType.Vfx:
            case ObjectType.World:
            case ObjectType.Housing:
            case ObjectType.Font:
                AddCounterString( set, info.ObjectType.ToString() );
                break;
            case ObjectType.DemiHuman:
                set[ $"Demi Human: {info.PrimaryId}" ] = null;
                break;
            case ObjectType.Monster:
                set[ $"Monster: {info.PrimaryId}" ] = null;
                break;
            case ObjectType.Icon:
                set[ $"Icon: {info.IconId}" ] = null;
                break;
            case ObjectType.Accessory:
            case ObjectType.Equipment:
                FindEquipment( set, info );
                break;
            case ObjectType.Weapon:
                FindWeapon( set, info );
                break;
            case ObjectType.Character:
                var (gender, race) = info.GenderRace.Split();
                var raceString   = race   != ModelRace.Unknown ? race.ToName() + " " : "";
                var genderString = gender != Gender.Unknown ? gender.ToName()  + " " : "Player ";
                switch( info.CustomizationType )
                {
                    case CustomizationType.Skin:
                        set[ $"Customization: {raceString}{genderString}Skin Textures" ] = null;
                        break;
                    case CustomizationType.DecalFace:
                        set[ $"Customization: Face Decal {info.PrimaryId}" ] = null;
                        break;
                    case CustomizationType.Iris when race == ModelRace.Unknown:
                        set[ $"Customization: All Eyes (Catchlight)" ] = null;
                        break;
                    default:
                    {
                        var customizationString = race == ModelRace.Unknown
                         || info.BodySlot              == BodySlot.Unknown
                         || info.CustomizationType     == CustomizationType.Unknown
                                ? "Customization: Unknown"
                                : $"Customization: {race} {gender} {info.BodySlot} ({info.CustomizationType}) {info.PrimaryId}";
                        set[ customizationString ] = null;
                        break;
                    }
                }

                break;

            default: throw new InvalidEnumArgumentException();
        }
    }

    private void IdentifyVfx( IDictionary< string, object? > set, GamePath path )
    {
        var key = GameData.GamePathParser.VfxToKey( path );
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
            var info = GameData.GamePathParser.GetFileInfo( path );
            IdentifyParsed( set, info );
        }
    }

    public Dictionary< string, object? > Identify( GamePath path )
    {
        Dictionary< string, object? > ret = new();
        Identify( ret, path );
        return ret;
    }

    public Item? Identify( SetId setId, WeaponType weaponType, ushort variant, EquipSlot slot )
    {
        switch( slot )
        {
            case EquipSlot.MainHand:
            case EquipSlot.OffHand:
            {
                var (begin, _) = FindIndexRange( _weapons, ( ( ulong )setId << 32 ) | ( ( ulong )weaponType << 16 ) | variant,
                    0xFFFFFFFFFFFF );
                return begin >= 0 ? _weapons[ begin ].Item2.FirstOrDefault() : null;
            }
            default:
            {
                var (begin, _) = FindIndexRange( _equipment,
                    ( ( ulong )setId << 32 ) | ( ( ulong )slot.ToSlot() << 16 ) | variant,
                    0xFFFFFFFFFFFF );
                return begin >= 0 ? _equipment[ begin ].Item2.FirstOrDefault() : null;
            }
        }
    }
}