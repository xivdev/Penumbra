using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Logging;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.GameData.Util;

namespace Penumbra.GameData;

internal class GamePathParser : IGamePathParser
{
    private const string CharacterFolder = "chara";
    private const string EquipmentFolder = "equipment";
    private const string PlayerFolder    = "human";
    private const string WeaponFolder    = "weapon";
    private const string AccessoryFolder = "accessory";
    private const string DemiHumanFolder = "demihuman";
    private const string MonsterFolder   = "monster";
    private const string CommonFolder    = "common";
    private const string UiFolder        = "ui";
    private const string IconFolder      = "icon";
    private const string LoadingFolder   = "loadingimage";
    private const string MapFolder       = "map";
    private const string InterfaceFolder = "uld";
    private const string FontFolder      = "font";
    private const string HousingFolder   = "hou";
    private const string VfxFolder       = "vfx";
    private const string WorldFolder1    = "bgcommon";
    private const string WorldFolder2    = "bg";

        // @formatter:off
        private readonly Dictionary<FileType, Dictionary<ObjectType, Regex[]>> _regexes = new()
        { { FileType.Font, new Dictionary< ObjectType, Regex[] >(){ { ObjectType.Font, new Regex[]{ new(@"common/font/(?'fontname'.*)_(?'id'\d\d)(_lobby)?\.fdt") } } } }
        , { FileType.Texture, new Dictionary< ObjectType, Regex[] >()
            { { ObjectType.Icon,      new Regex[]{ new(@"ui/icon/(?'group'\d*)(/(?'lang'[a-z]{2}))?(/(?'hq'hq))?/(?'id'\d*)(?'hr'_hr1)?\.tex") } }
            , { ObjectType.Map,       new Regex[]{ new(@"ui/map/(?'id'[a-z0-9]{4})/(?'variant'\d{2})/\k'id'\k'variant'(?'suffix'[a-z])?(_[a-z])?\.tex")  } }
            , { ObjectType.Weapon,    new Regex[]{ new(@"chara/weapon/w(?'id'\d{4})/obj/body/b(?'weapon'\d{4})/texture/v(?'variant'\d{2})_w\k'id'b\k'weapon'(_[a-z])?_[a-z]\.tex") } }
            , { ObjectType.Monster,   new Regex[]{ new(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/texture/v(?'variant'\d{2})_m\k'monster'b\k'id'(_[a-z])?_[a-z]\.tex") } }
            , { ObjectType.Equipment, new Regex[]{ new(@"chara/equipment/e(?'id'\d{4})/texture/v(?'variant'\d{2})_c(?'race'\d{4})e\k'id'_(?'slot'[a-z]{3})(_[a-z])?_[a-z]\.tex") } }
            , { ObjectType.DemiHuman, new Regex[]{ new(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/texture/v(?'variant'\d{2})_d\k'id'e\k'equip'_(?'slot'[a-z]{3})(_[a-z])?_[a-z]\.tex") } }
            , { ObjectType.Accessory, new Regex[]{ new(@"chara/accessory/a(?'id'\d{4})/texture/v(?'variant'\d{2})_c(?'race'\d{4})a\k'id'_(?'slot'[a-z]{3})_[a-z]\.tex") } }
            , { ObjectType.Character, new Regex[]{ new(@"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/texture/(?'minus'(--)?)(v(?'variant'\d{2})_)?c\k'race'\k'typeabr'\k'id'(_(?'slot'[a-z]{3}))?(_[a-z])?_[a-z]\.tex")
                                                 , new(@"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/texture")
                                                 , new(@"chara/common/texture/skin(?'skin'.*)\.tex")
                                                 , new(@"chara/common/texture/(?'catchlight'catchlight)(.*)\.tex")
                                                 , new(@"chara/common/texture/decal_(?'location'[a-z]+)/[-_]?decal_(?'id'\d+).tex") } } } }
        , { FileType.Model, new Dictionary< ObjectType, Regex[] >()
            { { ObjectType.Weapon,    new Regex[]{ new(@"chara/weapon/w(?'id'\d{4})/obj/body/b(?'weapon'\d{4})/model/w\k'id'b\k'weapon'\.mdl") } }
            , { ObjectType.Monster,   new Regex[]{ new(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/model/m\k'monster'b\k'id'\.mdl") } }
            , { ObjectType.Equipment, new Regex[]{ new(@"chara/equipment/e(?'id'\d{4})/model/c(?'race'\d{4})e\k'id'_(?'slot'[a-z]{3})\.mdl") } }
            , { ObjectType.DemiHuman, new Regex[]{ new(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/model/d\k'id'e\k'equip'_(?'slot'[a-z]{3})\.mdl") } }
            , { ObjectType.Accessory, new Regex[]{ new(@"chara/accessory/a(?'id'\d{4})/model/c(?'race'\d{4})a\k'id'_(?'slot'[a-z]{3})\.mdl") } }
            , { ObjectType.Character, new Regex[]{ new(@"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/model/c\k'race'\k'typeabr'\k'id'_(?'slot'[a-z]{3})\.mdl") } } } }
        , { FileType.Material, new Dictionary< ObjectType, Regex[] >()
            { { ObjectType.Weapon,    new Regex[]{ new(@"chara/weapon/w(?'id'\d{4})/obj/body/b(?'weapon'\d{4})/material/v(?'variant'\d{4})/mt_w\k'id'b\k'weapon'_[a-z]\.mtrl") } }
            , { ObjectType.Monster,   new Regex[]{ new(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/material/v(?'variant'\d{4})/mt_m\k'monster'b\k'id'_[a-z]\.mtrl") } }
            , { ObjectType.Equipment, new Regex[]{ new(@"chara/equipment/e(?'id'\d{4})/material/v(?'variant'\d{4})/mt_c(?'race'\d{4})e\k'id'_(?'slot'[a-z]{3})_[a-z]\.mtrl") } }
            , { ObjectType.DemiHuman, new Regex[]{ new(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/material/v(?'variant'\d{4})/mt_d\k'id'e\k'equip'_(?'slot'[a-z]{3})_[a-z]\.mtrl") } }
            , { ObjectType.Accessory, new Regex[]{ new(@"chara/accessory/a(?'id'\d{4})/material/v(?'variant'\d{4})/mt_c(?'race'\d{4})a\k'id'_(?'slot'[a-z]{3})_[a-z]\.mtrl") } }
            , { ObjectType.Character, new Regex[]{ new( @"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/material(/v(?'variant'\d{4}))?/mt_c\k'race'\k'typeabr'\k'id'(_(?'slot'[a-z]{3}))?_[a-z]\.mtrl" ) } } } }
        , { FileType.Imc, new Dictionary< ObjectType, Regex[] >()
            { { ObjectType.Weapon,    new Regex[]{ new(@"chara/weapon/w(?'id'\d{4})/obj/body/b(?'weapon'\d{4})/b\k'weapon'\.imc") } }
            , { ObjectType.Monster,   new Regex[]{ new(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/b\k'id'\.imc") } }
            , { ObjectType.Equipment, new Regex[]{ new(@"chara/equipment/e(?'id'\d{4})/e\k'id'\.imc") } }
            , { ObjectType.DemiHuman, new Regex[]{ new(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/e\k'equip'\.imc") } }
            , { ObjectType.Accessory, new Regex[]{ new(@"chara/accessory/a(?'id'\d{4})/a\k'id'\.imc") } } } },
        };
    // @formatter:on

    public ObjectType PathToObjectType( GamePath path )
    {
        if( path.Empty )
        {
            return ObjectType.Unknown;
        }

        string p       = path;
        var    folders = p.Split( '/' );
        if( folders.Length < 2 )
        {
            return ObjectType.Unknown;
        }

        return folders[ 0 ] switch
        {
            CharacterFolder => folders[ 1 ] switch
            {
                EquipmentFolder => ObjectType.Equipment,
                AccessoryFolder => ObjectType.Accessory,
                WeaponFolder    => ObjectType.Weapon,
                PlayerFolder    => ObjectType.Character,
                DemiHumanFolder => ObjectType.DemiHuman,
                MonsterFolder   => ObjectType.Monster,
                CommonFolder    => ObjectType.Character,
                _               => ObjectType.Unknown,
            },
            UiFolder => folders[ 1 ] switch
            {
                IconFolder      => ObjectType.Icon,
                LoadingFolder   => ObjectType.LoadingScreen,
                MapFolder       => ObjectType.Map,
                InterfaceFolder => ObjectType.Interface,
                _               => ObjectType.Unknown,
            },
            CommonFolder => folders[ 1 ] switch
            {
                FontFolder => ObjectType.Font,
                _          => ObjectType.Unknown,
            },
            HousingFolder => ObjectType.Housing,
            WorldFolder1 => folders[ 1 ] switch
            {
                HousingFolder => ObjectType.Housing,
                _             => ObjectType.World,
            },
            WorldFolder2 => ObjectType.World,
            VfxFolder    => ObjectType.Vfx,
            _            => ObjectType.Unknown,
        };
    }

    private (FileType, ObjectType, Match?) ParseGamePath( GamePath path )
    {
        if( !Names.ExtensionToFileType.TryGetValue( Extension( path ), out var fileType ) )
        {
            fileType = FileType.Unknown;
        }

        var objectType = PathToObjectType( path );

        if( !_regexes.TryGetValue( fileType, out var objectDict ) )
        {
            return ( fileType, objectType, null );
        }

        if( !objectDict.TryGetValue( objectType, out var regexes ) )
        {
            return ( fileType, objectType, null );
        }

        foreach( var regex in regexes )
        {
            var match = regex.Match( path );
            if( match.Success )
            {
                return ( fileType, objectType, match );
            }
        }

        return ( fileType, objectType, null );
    }

    private static string Extension( string filename )
    {
        var extIdx = filename.LastIndexOf( '.' );
        return extIdx < 0 ? "" : filename.Substring( extIdx );
    }

    private static GameObjectInfo HandleEquipment( FileType fileType, GroupCollection groups )
    {
        var setId = ushort.Parse( groups[ "id" ].Value );
        if( fileType == FileType.Imc )
        {
            return GameObjectInfo.Equipment( fileType, setId );
        }

        var gr   = Names.GenderRaceFromCode( groups[ "race" ].Value );
        var slot = Names.SuffixToEquipSlot[ groups[ "slot" ].Value ];
        if( fileType == FileType.Model )
        {
            return GameObjectInfo.Equipment( fileType, setId, gr, slot );
        }

        var variant = byte.Parse( groups[ "variant" ].Value );
        return GameObjectInfo.Equipment( fileType, setId, gr, slot, variant );
    }

    private static GameObjectInfo HandleWeapon( FileType fileType, GroupCollection groups )
    {
        var weaponId = ushort.Parse( groups[ "weapon" ].Value );
        var setId    = ushort.Parse( groups[ "id" ].Value );
        if( fileType == FileType.Imc || fileType == FileType.Model )
        {
            return GameObjectInfo.Weapon( fileType, setId, weaponId );
        }

        var variant = byte.Parse( groups[ "variant" ].Value );
        return GameObjectInfo.Weapon( fileType, setId, weaponId, variant );
    }

    private static GameObjectInfo HandleMonster( FileType fileType, GroupCollection groups )
    {
        var monsterId = ushort.Parse( groups[ "monster" ].Value );
        var bodyId    = ushort.Parse( groups[ "id" ].Value );
        if( fileType == FileType.Imc || fileType == FileType.Model )
        {
            return GameObjectInfo.Monster( fileType, monsterId, bodyId );
        }

        var variant = byte.Parse( groups[ "variant" ].Value );
        return GameObjectInfo.Monster( fileType, monsterId, bodyId, variant );
    }

    private static GameObjectInfo HandleDemiHuman( FileType fileType, GroupCollection groups )
    {
        var demiHumanId = ushort.Parse( groups[ "id" ].Value );
        var equipId     = ushort.Parse( groups[ "equip" ].Value );
        if( fileType == FileType.Imc )
        {
            return GameObjectInfo.DemiHuman( fileType, demiHumanId, equipId );
        }

        var slot = Names.SuffixToEquipSlot[ groups[ "slot" ].Value ];
        if( fileType == FileType.Model )
        {
            return GameObjectInfo.DemiHuman( fileType, demiHumanId, equipId, slot );
        }

        var variant = byte.Parse( groups[ "variant" ].Value );
        return GameObjectInfo.DemiHuman( fileType, demiHumanId, equipId, slot, variant );
    }

    private static GameObjectInfo HandleCustomization( FileType fileType, GroupCollection groups )
    {
        if( groups[ "catchlight" ].Success )
        {
            return GameObjectInfo.Customization( fileType, CustomizationType.Iris );
        }

        if( groups[ "skin" ].Success )
        {
            return GameObjectInfo.Customization( fileType, CustomizationType.Skin );
        }

        var id = ushort.Parse( groups[ "id" ].Value );
        if( groups[ "location" ].Success )
        {
            var tmpType = groups[ "location" ].Value == "face"  ? CustomizationType.DecalFace
                : groups[ "location" ].Value         == "equip" ? CustomizationType.DecalEquip : CustomizationType.Unknown;
            return GameObjectInfo.Customization( fileType, tmpType, id );
        }

        var gr       = Names.GenderRaceFromCode( groups[ "race" ].Value );
        var bodySlot = Names.StringToBodySlot[ groups[ "type" ].Value ];
        var type = groups[ "slot" ].Success
            ? Names.SuffixToCustomizationType[ groups[ "slot" ].Value ]
            : CustomizationType.Skin;
        if( fileType == FileType.Material )
        {
            var variant = groups[ "variant" ].Success ? byte.Parse( groups[ "variant" ].Value ) : ( byte )0;
            return GameObjectInfo.Customization( fileType, type, id, gr, bodySlot, variant );
        }

        return GameObjectInfo.Customization( fileType, type, id, gr, bodySlot );
    }

    private static GameObjectInfo HandleIcon( FileType fileType, GroupCollection groups )
    {
        var hq = groups[ "hq" ].Success;
        var hr = groups[ "hr" ].Success;
        var id = uint.Parse( groups[ "id" ].Value );
        if( !groups[ "lang" ].Success )
        {
            return GameObjectInfo.Icon( fileType, id, hq, hr );
        }

        var language = groups[ "lang" ].Value switch
        {
            "en" => Dalamud.ClientLanguage.English,
            "ja" => Dalamud.ClientLanguage.Japanese,
            "de" => Dalamud.ClientLanguage.German,
            "fr" => Dalamud.ClientLanguage.French,
            _    => Dalamud.ClientLanguage.English,
        };
        return GameObjectInfo.Icon( fileType, id, hq, hr, language );
    }

    private static GameObjectInfo HandleMap( FileType fileType, GroupCollection groups )
    {
        var map     = Encoding.ASCII.GetBytes( groups[ "id" ].Value );
        var variant = byte.Parse( groups[ "variant" ].Value );
        if( groups[ "suffix" ].Success )
        {
            var suffix = Encoding.ASCII.GetBytes( groups[ "suffix" ].Value )[ 0 ];
            return GameObjectInfo.Map( fileType, map[ 0 ], map[ 1 ], map[ 2 ], map[ 3 ], variant, suffix );
        }

        return GameObjectInfo.Map( fileType, map[ 0 ], map[ 1 ], map[ 2 ], map[ 3 ], variant );
    }

    public GameObjectInfo GetFileInfo( GamePath path )
    {
        var (fileType, objectType, match) = ParseGamePath( path );
        if( match == null || !match.Success )
        {
            return new GameObjectInfo { FileType = fileType, ObjectType = objectType };
        }

        try
        {
            var groups = match.Groups;
            switch( objectType )
            {
                case ObjectType.Accessory: return HandleEquipment( fileType, groups );
                case ObjectType.Equipment: return HandleEquipment( fileType, groups );
                case ObjectType.Weapon:    return HandleWeapon( fileType, groups );
                case ObjectType.Map:       return HandleMap( fileType, groups );
                case ObjectType.Monster:   return HandleMonster( fileType, groups );
                case ObjectType.DemiHuman: return HandleDemiHuman( fileType, groups );
                case ObjectType.Character: return HandleCustomization( fileType, groups );
                case ObjectType.Icon:      return HandleIcon( fileType, groups );
            }
        }
        catch( Exception e )
        {
            PluginLog.Error( $"Could not parse {path}:\n{e}" );
        }

        return new GameObjectInfo { FileType = fileType, ObjectType = objectType };
    }

    private readonly Regex _vfxRegexTmb = new(@"chara/action/(?'key'[^\s]+?)\.tmb");
    private readonly Regex _vfxRegexPap = new(@"chara/human/c0101/animation/a0001/[^\s]+?/(?'key'[^\s]+?)\.pap");

    public string VfxToKey( GamePath path )
    {
        var match = _vfxRegexTmb.Match( path );
        if( match.Success )
        {
            return match.Groups[ "key" ].Value.ToLowerInvariant();
        }

        match = _vfxRegexPap.Match( path );
        return match.Success ? match.Groups[ "key" ].Value.ToLowerInvariant() : string.Empty;
    }
}