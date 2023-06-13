using System.Text.RegularExpressions;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Penumbra.GameData.Data;

public static partial class GamePaths
{
    [GeneratedRegex(@"c(?'racecode'\d{4})")]
    public static partial Regex RaceCodeParser();

    public static GenderRace ParseRaceCode(string path)
    {
        var match = RaceCodeParser().Match(path);
        return match.Success
            ? Names.GenderRaceFromCode(match.Groups["racecode"].Value)
            : GenderRace.Unknown;
    }

    public static partial class Monster
    {
        public static partial class Imc
        {
            [GeneratedRegex(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/b\k'id'\.imc")]
            public static partial Regex Regex();

            public static string Path(SetId monsterId, SetId bodyId)
                => $"chara/monster/m{monsterId.Value:D4}/obj/body/b{bodyId.Value:D4}/b{bodyId.Value:D4}.imc";
        }

        public static partial class Mdl
        {
            [GeneratedRegex(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/model/m\k'monster'b\k'id'\.mdl")]
            public static partial Regex Regex();

            public static string Path(SetId monsterId, SetId bodyId)
                => $"chara/monster/m{monsterId.Value:D4}/obj/body/b{bodyId.Value:D4}/model/m{monsterId.Value:D4}b{bodyId.Value:D4}.mdl";
        }

        public static partial class Mtrl
        {
            [GeneratedRegex(
                @"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/material/v(?'variant'\d{4})/mt_m\k'monster'b\k'id'_[a-z]+\.mtrl")]
            public static partial Regex Regex();

            public static string Path(SetId monsterId, SetId bodyId, byte variant, string suffix)
                => $"chara/monster/m{monsterId.Value:D4}/obj/body/b{bodyId.Value:D4}/material/v{variant:D4}/mt_m{monsterId.Value:D4}b{bodyId.Value:D4}_{suffix}.mtrl";
        }

        public static partial class Tex
        {
            [GeneratedRegex(
                @"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/texture/v(?'variant'\d{2})_m\k'monster'b\k'id'(_[a-z])?_[a-z]\.tex")]
            public static partial Regex Regex();

            public static string Path(SetId monsterId, SetId bodyId, byte variant, char suffix1, char suffix2 = '\0')
                => $"chara/monster/m{monsterId.Value:D4}/obj/body/b{bodyId.Value:D4}/texture/v{variant:D2}_m{monsterId.Value:D4}b{bodyId.Value:D4}{(suffix2 != '\0' ? $"_{suffix2}" : string.Empty)}_{suffix1}.tex";
        }

        public static partial class Sklb
        {
            public static string Path(SetId monsterId)
                => $"chara/monster/m{monsterId.Value:D4}/skeleton/base/b0001/skl_m{monsterId.Value:D4}b0001.sklb";
        }

        public static partial class Skp
        {
            public static string Path(SetId monsterId)
                => $"chara/monster/m{monsterId.Value:D4}/skeleton/base/b0001/skl_m{monsterId.Value:D4}b0001.skp";
        }

        public static partial class Eid
        {
            public static string Path(SetId monsterId)
                => $"chara/monster/m{monsterId.Value:D4}/skeleton/base/b0001/eid_m{monsterId.Value:D4}b0001.eid";
        }
    }

    public static partial class Weapon
    {
        public static partial class Imc
        {
            [GeneratedRegex(@"chara/weapon/w(?'id'\d{4})/obj/body/b(?'weapon'\d{4})/b\k'weapon'\.imc")]
            public static partial Regex Regex();

            public static string Path(SetId weaponId, SetId bodyId)
                => $"chara/weapon/w{weaponId.Value:D4}/obj/body/b{bodyId.Value:D4}/b{bodyId.Value:D4}.imc";
        }

        public static partial class Mdl
        {
            [GeneratedRegex(@"chara/weapon/w(?'id'\d{4})/obj/body/b(?'weapon'\d{4})/model/w\k'id'b\k'weapon'\.mdl")]
            public static partial Regex Regex();

            public static string Path(SetId weaponId, SetId bodyId)
                => $"chara/weapon/w{weaponId.Value:D4}/obj/body/b{bodyId.Value:D4}/model/w{weaponId.Value:D4}b{bodyId.Value:D4}.mdl";
        }

        public static partial class Mtrl
        {
            [GeneratedRegex(
                @"chara/weapon/w(?'id'\d{4})/obj/body/b(?'weapon'\d{4})/material/v(?'variant'\d{4})/mt_w\k'id'b\k'weapon'_[a-z]+\.mtrl")]
            public static partial Regex Regex();

            public static string Path(SetId weaponId, SetId bodyId, byte variant, string suffix)
                => $"chara/weapon/w{weaponId.Value:D4}/obj/body/b{bodyId.Value:D4}/material/v{variant:D4}/mt_w{weaponId.Value:D4}b{bodyId.Value:D4}_{suffix}.mtrl";
        }

        public static partial class Tex
        {
            [GeneratedRegex(
                @"chara/weapon/w(?'id'\d{4})/obj/body/b(?'weapon'\d{4})/texture/v(?'variant'\d{2})_w\k'id'b\k'weapon'(_[a-z])?_[a-z]\.tex")]
            public static partial Regex Regex();

            public static string Path(SetId weaponId, SetId bodyId, byte variant, char suffix1, char suffix2 = '\0')
                => $"chara/weapon/w{weaponId.Value:D4}/obj/body/b{bodyId.Value:D4}/texture/v{variant:D2}_w{weaponId.Value:D4}b{bodyId.Value:D4}{(suffix2 != '\0' ? $"_{suffix2}" : string.Empty)}_{suffix1}.tex";
        }
    }

    public static partial class DemiHuman
    {
        public static partial class Imc
        {
            [GeneratedRegex(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/e\k'equip'\.imc")]
            public static partial Regex Regex();

            public static string Path(SetId demiId, SetId equipId)
                => $"chara/demihuman/d{demiId.Value:D4}/obj/equipment/e{equipId.Value:D4}/e{equipId.Value:D4}.imc";
        }

        public static partial class Mdl
        {
            [GeneratedRegex(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/model/d\k'id'e\k'equip'_(?'slot'[a-z]{3})\.mdl")]
            public static partial Regex Regex();

            public static string Path(SetId demiId, SetId equipId, EquipSlot slot)
                => $"chara/demihuman/d{demiId.Value:D4}/obj/equipment/e{equipId.Value:D4}/model/d{demiId.Value:D4}e{equipId.Value:D4}_{slot.ToSuffix()}.mdl";
        }

        public static partial class Mtrl
        {
            [GeneratedRegex(
                @"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/material/v(?'variant'\d{4})/mt_d\k'id'e\k'equip'_(?'slot'[a-z]{3})_[a-z]+\.mtrl")]
            public static partial Regex Regex();

            public static string Path(SetId demiId, SetId equipId, EquipSlot slot, byte variant, string suffix)
                => $"chara/demihuman/d{demiId.Value:D4}/obj/equipment/e{equipId.Value:D4}/material/v{variant:D4}/mt_d{demiId.Value:D4}e{equipId.Value:D4}_{slot.ToSuffix()}_{suffix}.mtrl";
        }

        public static partial class Tex
        {
            [GeneratedRegex(
                @"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/texture/v(?'variant'\d{2})_d\k'id'e\k'equip'_(?'slot'[a-z]{3})(_[a-z])?_[a-z]\.tex")]
            public static partial Regex Regex();

            public static string Path(SetId demiId, SetId equipId, EquipSlot slot, byte variant, char suffix1, char suffix2 = '\0')
                => $"chara/demihuman/d{demiId.Value:D4}/obj/equipment/e{equipId.Value:D4}/texture/v{variant:D2}_d{demiId.Value:D4}e{equipId.Value:D4}_{slot.ToSuffix()}{(suffix2 != '\0' ? $"_{suffix2}" : string.Empty)}_{suffix1}.tex";
        }
    }

    public static partial class Equipment
    {
        public static partial class Imc
        {
            [GeneratedRegex(@"chara/equipment/e(?'id'\d{4})/e\k'id'\.imc")]
            public static partial Regex Regex();

            public static string Path(SetId equipId)
                => $"chara/equipment/e{equipId.Value:D4}/e{equipId.Value:D4}.imc";
        }

        public static partial class Mdl
        {
            [GeneratedRegex(@"chara/equipment/e(?'id'\d{4})/model/c(?'race'\d{4})e\k'id'_(?'slot'[a-z]{3})\.mdl")]
            public static partial Regex Regex();

            public static string Path(SetId equipId, GenderRace raceCode, EquipSlot slot)
                => $"chara/equipment/e{equipId.Value:D4}/model/c{raceCode.ToRaceCode()}e{equipId.Value:D4}_{slot.ToSuffix()}.mdl";
        }

        public static partial class Mtrl
        {
            [GeneratedRegex(
                @"chara/equipment/e(?'id'\d{4})/material/v(?'variant'\d{4})/mt_c(?'race'\d{4})e\k'id'_(?'slot'[a-z]{3})_[a-z]+\.mtrl")]
            public static partial Regex Regex();

            public static string Path(SetId equipId, GenderRace raceCode, EquipSlot slot, byte variant, string suffix)
                => $"{FolderPath(equipId, variant)}/mt_c{raceCode.ToRaceCode()}e{equipId.Value:D4}_{slot.ToSuffix()}_{suffix}.mtrl";

            public static string FolderPath(SetId equipId, byte variant)
                => $"chara/equipment/e{equipId.Value:D4}/material/v{variant:D4}";
        }

        public static partial class Tex
        {
            [GeneratedRegex(
                @"chara/equipment/e(?'id'\d{4})/texture/v(?'variant'\d{2})_c(?'race'\d{4})e\k'id'_(?'slot'[a-z]{3})(_[a-z])?_[a-z]\.tex")]
            public static partial Regex Regex();

            public static string Path(SetId equipId, GenderRace raceCode, EquipSlot slot, byte variant, char suffix1, char suffix2 = '\0')
                => $"chara/equipment/e{equipId.Value:D4}/texture/v{variant:D2}_c{raceCode.ToRaceCode()}e{equipId.Value:D4}_{slot.ToSuffix()}{(suffix2 != '\0' ? $"_{suffix2}" : string.Empty)}_{suffix1}.tex";
        }

        public static partial class Avfx
        {
            [GeneratedRegex(@"chara/equipment/e(?'id'\d{4})/vfx/eff/ve(?'variant'\d{4})\.avfx")]
            public static partial Regex Regex();

            public static string Path(SetId equipId, byte effectId)
                => $"chara/equipment/e{equipId.Value:D4}/vfx/eff/ve{effectId:D4}.avfx";
        }

        public static partial class Decal
        {
            [GeneratedRegex(@"chara/common/texture/decal_equip/-decal_(?'decalId'\d{3})\.tex")]
            public static partial Regex Regex();

            public static string Path(byte decalId)
                => $"chara/common/texture/decal_equip/-decal_{decalId:D3}.tex";
        }
    }

    public static partial class Accessory
    {
        public static partial class Imc
        {
            [GeneratedRegex(@"chara/accessory/a(?'id'\d{4})/a\k'id'\.imc")]
            public static partial Regex Regex();

            public static string Path(SetId accessoryId)
                => $"chara/accessory/a{accessoryId.Value:D4}/a{accessoryId.Value:D4}.imc";
        }

        public static partial class Mdl
        {
            [GeneratedRegex(@"chara/accessory/a(?'id'\d{4})/model/c(?'race'\d{4})a\k'id'_(?'slot'[a-z]{3})\.mdl")]
            public static partial Regex Regex();

            public static string Path(SetId accessoryId, GenderRace raceCode, EquipSlot slot)
                => $"chara/accessory/a{accessoryId.Value:D4}/model/c{raceCode.ToRaceCode()}a{accessoryId.Value:D4}_{slot.ToSuffix()}.mdl";
        }

        public static partial class Mtrl
        {
            [GeneratedRegex(
                @"chara/accessory/a(?'id'\d{4})/material/v(?'variant'\d{4})/mt_c(?'race'\d{4})a\k'id'_(?'slot'[a-z]{3})_[a-z]+\.mtrl")]
            public static partial Regex Regex();

            public static string Path(SetId accessoryId, GenderRace raceCode, EquipSlot slot, byte variant, string suffix)
                => $"{FolderPath(accessoryId, variant)}/c{raceCode.ToRaceCode()}a{accessoryId.Value:D4}_{slot.ToSuffix()}_{suffix}.mtrl";

            public static string FolderPath(SetId accessoryId, byte variant)
                => $"chara/accessory/a{accessoryId.Value:D4}/material/v{variant:D4}";
        }

        public static partial class Tex
        {
            [GeneratedRegex(
                @"chara/accessory/a(?'id'\d{4})/texture/v(?'variant'\d{2})_c(?'race'\d{4})a\k'id'_(?'slot'[a-z]{3})(_[a-z])?_[a-z]\.tex")]
            public static partial Regex Regex();

            public static string Path(SetId accessoryId, GenderRace raceCode, EquipSlot slot, byte variant, char suffix1, char suffix2 = '\0')
                => $"chara/accessory/a{accessoryId.Value:D4}/texture/v{variant:D2}_c{raceCode.ToRaceCode()}a{accessoryId.Value:D4}_{slot.ToSuffix()}{(suffix2 != '\0' ? $"_{suffix2}" : string.Empty)}_{suffix1}.tex";
        }
    }

    public static partial class Skeleton
    {
        public static partial class Phyb
        {
            public static string Path(GenderRace raceCode, string slot, SetId slotId)
                => $"chara/human/c{raceCode.ToRaceCode()}/skeleton/{slot}/{slot[0]}{slotId.Value:D4}/phy_c{raceCode.ToRaceCode()}{slot[0]}{slotId.Value:D4}.phyb";
        }

        public static partial class Sklb
        {
            public static string Path(GenderRace raceCode, string slot, SetId slotId)
                => $"chara/human/c{raceCode.ToRaceCode()}/skeleton/{slot}/{slot[0]}{slotId.Value:D4}/skl_c{raceCode.ToRaceCode()}{slot[0]}{slotId.Value:D4}.sklb";
        }
    }

    public static partial class Character
    {
        public static partial class Mdl
        {
            [GeneratedRegex(
                @"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/model/c\k'race'\k'typeabr'\k'id'_(?'slot'[a-z]{3})\.mdl")]
            public static partial Regex Regex();

            public static string Path(GenderRace raceCode, BodySlot slot, SetId slotId, CustomizationType type)
                => $"chara/human/c{raceCode.ToRaceCode()}/obj/{slot.ToSuffix()}/{slot.ToAbbreviation()}{slotId.Value:D4}/model/c{raceCode.ToRaceCode()}{slot.ToAbbreviation()}{slotId.Value:D4}_{type.ToSuffix()}.mdl";
        }

        public static partial class Mtrl
        {
            [GeneratedRegex(
                @"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/material(/v(?'variant'\d{4}))?/mt_c\k'race'\k'typeabr'\k'id'(_(?'slot'[a-z]{3}))?_[a-z]+\.mtrl")]
            public static partial Regex Regex();

            public static string FolderPath(GenderRace raceCode, BodySlot slot, SetId slotId, byte variant = byte.MaxValue)
                => $"chara/human/c{raceCode.ToRaceCode()}/obj/{slot.ToSuffix()}/{slot.ToAbbreviation()}{slotId.Value:D4}/material{(variant != byte.MaxValue ? $"/v{variant:D4}" : string.Empty)}";

            public static string HairPath(GenderRace raceCode, SetId slotId, string fileName, out GenderRace actualGr)
            {
                actualGr = MaterialHandling.GetGameGenderRace(raceCode, slotId);
                var folder = FolderPath(actualGr, BodySlot.Hair, slotId, 1);
                return actualGr == raceCode
                    ? $"{folder}{fileName}"
                    : $"{folder}/mt_c{actualGr.ToRaceCode()}{fileName[9..]}";
            }

            public static string TailPath(GenderRace raceCode, SetId slotId, string fileName, byte variant, out SetId actualSlotId)
            {
                switch (raceCode)
                {
                    case GenderRace.HrothgarMale:
                    case GenderRace.HrothgarFemale:
                    case GenderRace.HrothgarMaleNpc:
                    case GenderRace.HrothgarFemaleNpc:
                        var folder = FolderPath(raceCode, BodySlot.Tail, 1, variant == byte.MaxValue ? (byte)1 : variant);
                        actualSlotId = 1;
                        return $"{folder}{fileName}";
                    default:
                        actualSlotId = slotId;
                        return $"{FolderPath(raceCode, BodySlot.Tail, slotId, variant)}{fileName}";
                }
            }

            public static string Path(GenderRace raceCode, BodySlot slot, SetId slotId, string fileName,
                out GenderRace actualGr, out SetId actualSlotId, byte variant = byte.MaxValue)
            {
                switch (slot)
                {
                    case BodySlot.Hair:
                        actualSlotId = slotId;
                        return HairPath(raceCode, slotId, fileName, out actualGr);
                    case BodySlot.Tail:
                        actualGr = raceCode;
                        return TailPath(raceCode, slotId, fileName, variant, out actualSlotId);
                    default:
                        actualSlotId = slotId;
                        actualGr     = raceCode;
                        return $"{FolderPath(raceCode, slot, slotId, variant)}{fileName}";
                }
            }
        }

        public static partial class Tex
        {
            [GeneratedRegex(
                @"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/texture/(?'minus'(--)?)(v(?'variant'\d{2})_)?c\k'race'\k'typeabr'\k'id'(_(?'slot'[a-z]{3}))?(_[a-z])?_[a-z]\.tex")]
            public static partial Regex Regex();

            public static string Path(GenderRace raceCode, BodySlot slot, SetId slotId, char suffix1, bool minus = false,
                CustomizationType type = CustomizationType.Unknown, byte variant = byte.MaxValue, char suffix2 = '\0')
                => $"chara/human/c{raceCode.ToRaceCode()}/obj/{slot.ToSuffix()}/{slot.ToAbbreviation()}{slotId.Value:D4}/texture/"
                  + (minus ? "--" : string.Empty)
                  + (variant != byte.MaxValue ? $"v{variant:D2}_" : string.Empty)
                  + $"c{raceCode.ToRaceCode()}{slot.ToAbbreviation()}{slotId.Value:D4}{(type != CustomizationType.Unknown ? $"_{type.ToSuffix()}" : string.Empty)}{(suffix2 != '\0' ? $"_{suffix2}" : string.Empty)}_{suffix1}.tex";


            [GeneratedRegex(@"chara/common/texture/(?'catchlight'catchlight)(.*)\.tex")]
            public static partial Regex CatchlightRegex();

            [GeneratedRegex(@"chara/common/texture/skin(?'skin'.*)\.tex")]
            public static partial Regex SkinRegex();

            [GeneratedRegex(@"chara/common/texture/decal_(?'location'[a-z]+)/[-_]?decal_(?'id'\d+).tex")]
            public static partial Regex DecalRegex();

            [GeneratedRegex(@"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/texture")]
            public static partial Regex FolderRegex();
        }
    }

    public static partial class Icon
    {
        [GeneratedRegex(@"ui/icon/(?'group'\d*)(/(?'lang'[a-z]{2}))?(/(?'hq'hq))?/(?'id'\d*)(?'hr'_hr1)?\.tex")]
        public static partial Regex Regex();
    }

    public static partial class Map
    {
        [GeneratedRegex(@"ui/map/(?'id'[a-z0-9]{4})/(?'variant'\d{2})/\k'id'\k'variant'(?'suffix'[a-z])?(_[a-z])?\.tex")]
        public static partial Regex Regex();
    }

    public static partial class Font
    {
        [GeneratedRegex(@"common/font/(?'fontname'.*)_(?'id'\d\d)(_lobby)?\.fdt")]
        public static partial Regex Regex();
    }

    public static partial class Vfx
    {
        [GeneratedRegex(@"chara[\/]action[\/](?'key'[^\s]+?)\.tmb", RegexOptions.IgnoreCase)]
        public static partial Regex Tmb();

        [GeneratedRegex(@"chara[\/]human[\/]c0101[\/]animation[\/]a0001[\/][^\s]+?[\/](?'key'[^\s]+?)\.pap", RegexOptions.IgnoreCase)]
        public static partial Regex Pap();
    }

    public static partial class Shader
    {
        public static string ShpkPath(string name)
            => $"shader/sm5/shpk/{name}";
    }
}
