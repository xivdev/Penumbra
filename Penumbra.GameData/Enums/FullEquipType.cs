using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.GeneratedSheets;

namespace Penumbra.GameData.Enums;

public enum FullEquipType : byte
{
    Unknown,

    Head,
    Body,
    Hands,
    Legs,
    Feet,

    Ears,
    Neck,
    Wrists,
    Finger,

    Fists,      // PGL, MNK
    Sword,      // GLA, PLD Main
    Axe,        // MRD, WAR
    Bow,        // ARC, BRD
    Lance,      // LNC, DRG,
    Staff,      // THM, BLM, CNJ, WHM
    Wand,       // THM, BLM, CNJ, WHM Main
    Book,       // ACN, SMN, SCH
    Daggers,    // ROG, NIN
    Broadsword, // DRK,
    Gun,        // MCH,
    Orrery,     // AST,
    Katana,     // SAM
    Rapier,     // RDM
    Cane,       // BLU
    Gunblade,   // GNB,
    Glaives,    // DNC,
    Scythe,     // RPR,
    Nouliths,   // SGE
    Shield,     // GLA, PLD, THM, BLM, CNJ, WHM Off

    Saw,             // CRP
    CrossPeinHammer, // BSM
    RaisingHammer,   // ARM
    LapidaryHammer,  // GSM
    Knife,           // LTW
    Needle,          // WVR
    Alembic,         // ALC
    Frypan,          // CUL
    Pickaxe,         // MIN
    Hatchet,         // BTN
    FishingRod,      // FSH

    ClawHammer,    // CRP Off
    File,          // BSM Off
    Pliers,        // ARM Off
    GrindingWheel, // GSM Off
    Awl,           // LTW Off
    SpinningWheel, // WVR Off
    Mortar,        // ALC Off
    CulinaryKnife, // CUL Off
    Sledgehammer,  // MIN Off
    GardenScythe,  // BTN Off
    Gig,           // FSH Off
}

public static class FullEquipTypeExtensions
{
    public static FullEquipType ToEquipType(this Item item)
    {
        var slot   = (EquipSlot)item.EquipSlotCategory.Row;
        var weapon = (WeaponCategory)item.ItemUICategory.Row;
        return slot.ToEquipType(weapon);
    }

    public static bool IsWeapon(this FullEquipType type)
        => type switch
        {
            FullEquipType.Fists      => true,
            FullEquipType.Sword      => true,
            FullEquipType.Axe        => true,
            FullEquipType.Bow        => true,
            FullEquipType.Lance      => true,
            FullEquipType.Staff      => true,
            FullEquipType.Wand       => true,
            FullEquipType.Book       => true,
            FullEquipType.Daggers    => true,
            FullEquipType.Broadsword => true,
            FullEquipType.Gun        => true,
            FullEquipType.Orrery     => true,
            FullEquipType.Katana     => true,
            FullEquipType.Rapier     => true,
            FullEquipType.Cane       => true,
            FullEquipType.Gunblade   => true,
            FullEquipType.Glaives    => true,
            FullEquipType.Scythe     => true,
            FullEquipType.Nouliths   => true,
            FullEquipType.Shield     => true,
            _                        => false,
        };

    public static bool IsTool(this FullEquipType type)
        => type switch
        {
            FullEquipType.Saw             => true,
            FullEquipType.CrossPeinHammer => true,
            FullEquipType.RaisingHammer   => true,
            FullEquipType.LapidaryHammer  => true,
            FullEquipType.Knife           => true,
            FullEquipType.Needle          => true,
            FullEquipType.Alembic         => true,
            FullEquipType.Frypan          => true,
            FullEquipType.Pickaxe         => true,
            FullEquipType.Hatchet         => true,
            FullEquipType.FishingRod      => true,
            _                             => false,
        };

    public static bool IsEquipment(this FullEquipType type)
        => type switch
        {
            FullEquipType.Head  => true,
            FullEquipType.Body  => true,
            FullEquipType.Hands => true,
            FullEquipType.Legs  => true,
            FullEquipType.Feet  => true,
            _                   => false,
        };

    public static bool IsAccessory(this FullEquipType type)
        => type switch
        {
            FullEquipType.Ears   => true,
            FullEquipType.Neck   => true,
            FullEquipType.Wrists => true,
            FullEquipType.Finger => true,
            _                    => false,
        };

    public static string ToName(this FullEquipType type)
        => type switch
        {
            FullEquipType.Head            => EquipSlot.Head.ToName(),
            FullEquipType.Body            => EquipSlot.Body.ToName(),
            FullEquipType.Hands           => EquipSlot.Hands.ToName(),
            FullEquipType.Legs            => EquipSlot.Legs.ToName(),
            FullEquipType.Feet            => EquipSlot.Feet.ToName(),
            FullEquipType.Ears            => EquipSlot.Ears.ToName(),
            FullEquipType.Neck            => EquipSlot.Neck.ToName(),
            FullEquipType.Wrists          => EquipSlot.Wrists.ToName(),
            FullEquipType.Finger          => "Ring",
            FullEquipType.Fists           => "Fist Weapon",
            FullEquipType.Sword           => "Sword",
            FullEquipType.Axe             => "Axe",
            FullEquipType.Bow             => "Bow",
            FullEquipType.Lance           => "Lance",
            FullEquipType.Staff           => "Staff",
            FullEquipType.Wand            => "Mace",
            FullEquipType.Book            => "Book",
            FullEquipType.Daggers         => "Dagger",
            FullEquipType.Broadsword      => "Broadsword",
            FullEquipType.Gun             => "Gun",
            FullEquipType.Orrery          => "Orrery",
            FullEquipType.Katana          => "Katana",
            FullEquipType.Rapier          => "Rapier",
            FullEquipType.Cane            => "Cane",
            FullEquipType.Gunblade        => "Gunblade",
            FullEquipType.Glaives         => "Glaive",
            FullEquipType.Scythe          => "Scythe",
            FullEquipType.Nouliths        => "Nouliths",
            FullEquipType.Shield          => "Shield",
            FullEquipType.Saw             => "Saw (Carpenter)",
            FullEquipType.CrossPeinHammer => "Hammer (Blacksmith)",
            FullEquipType.RaisingHammer   => "Hammer (Armorsmith)",
            FullEquipType.LapidaryHammer  => "Hammer (Goldsmith)",
            FullEquipType.Knife           => "Knife (Leatherworker)",
            FullEquipType.Needle          => "Needle (Weaver)",
            FullEquipType.Alembic         => "Alembic (Alchemist)",
            FullEquipType.Frypan          => "Frypan (Culinarian)",
            FullEquipType.Pickaxe         => "Pickaxe (Miner)",
            FullEquipType.Hatchet         => "Hatchet (Botanist)",
            FullEquipType.FishingRod      => "Fishing Rod",
            FullEquipType.ClawHammer      => "Clawhammer (Carpenter)",
            FullEquipType.File            => "File (Blacksmith)",
            FullEquipType.Pliers          => "Pliers (Armorsmith)",
            FullEquipType.GrindingWheel   => "Grinding Wheel (Goldsmith)",
            FullEquipType.Awl             => "Awl (Leatherworker)",
            FullEquipType.SpinningWheel   => "Spinning Wheel (Weaver)",
            FullEquipType.Mortar          => "Mortar (Alchemist)",
            FullEquipType.CulinaryKnife   => "Knife (Culinarian)",
            FullEquipType.Sledgehammer    => "Sledgehammer (Miner)",
            FullEquipType.GardenScythe    => "Garden Scythe (Botanist)",
            FullEquipType.Gig             => "Gig (Fisher)",
            _                             => "Unknown",
        };

    public static EquipSlot ToSlot(this FullEquipType type)
        => type switch
        {
            FullEquipType.Head            => EquipSlot.Head,
            FullEquipType.Body            => EquipSlot.Body,
            FullEquipType.Hands           => EquipSlot.Hands,
            FullEquipType.Legs            => EquipSlot.Legs,
            FullEquipType.Feet            => EquipSlot.Feet,
            FullEquipType.Ears            => EquipSlot.Ears,
            FullEquipType.Neck            => EquipSlot.Neck,
            FullEquipType.Wrists          => EquipSlot.Wrists,
            FullEquipType.Finger          => EquipSlot.RFinger,
            FullEquipType.Fists           => EquipSlot.MainHand,
            FullEquipType.Sword           => EquipSlot.MainHand,
            FullEquipType.Axe             => EquipSlot.MainHand,
            FullEquipType.Bow             => EquipSlot.MainHand,
            FullEquipType.Lance           => EquipSlot.MainHand,
            FullEquipType.Staff           => EquipSlot.MainHand,
            FullEquipType.Wand            => EquipSlot.MainHand,
            FullEquipType.Book            => EquipSlot.MainHand,
            FullEquipType.Daggers         => EquipSlot.MainHand,
            FullEquipType.Broadsword      => EquipSlot.MainHand,
            FullEquipType.Gun             => EquipSlot.MainHand,
            FullEquipType.Orrery          => EquipSlot.MainHand,
            FullEquipType.Katana          => EquipSlot.MainHand,
            FullEquipType.Rapier          => EquipSlot.MainHand,
            FullEquipType.Cane            => EquipSlot.MainHand,
            FullEquipType.Gunblade        => EquipSlot.MainHand,
            FullEquipType.Glaives         => EquipSlot.MainHand,
            FullEquipType.Scythe          => EquipSlot.MainHand,
            FullEquipType.Nouliths        => EquipSlot.MainHand,
            FullEquipType.Shield          => EquipSlot.OffHand,
            FullEquipType.Saw             => EquipSlot.MainHand,
            FullEquipType.CrossPeinHammer => EquipSlot.MainHand,
            FullEquipType.RaisingHammer   => EquipSlot.MainHand,
            FullEquipType.LapidaryHammer  => EquipSlot.MainHand,
            FullEquipType.Knife           => EquipSlot.MainHand,
            FullEquipType.Needle          => EquipSlot.MainHand,
            FullEquipType.Alembic         => EquipSlot.MainHand,
            FullEquipType.Frypan          => EquipSlot.MainHand,
            FullEquipType.Pickaxe         => EquipSlot.MainHand,
            FullEquipType.Hatchet         => EquipSlot.MainHand,
            FullEquipType.FishingRod      => EquipSlot.MainHand,
            FullEquipType.ClawHammer      => EquipSlot.OffHand,
            FullEquipType.File            => EquipSlot.OffHand,
            FullEquipType.Pliers          => EquipSlot.OffHand,
            FullEquipType.GrindingWheel   => EquipSlot.OffHand,
            FullEquipType.Awl             => EquipSlot.OffHand,
            FullEquipType.SpinningWheel   => EquipSlot.OffHand,
            FullEquipType.Mortar          => EquipSlot.OffHand,
            FullEquipType.CulinaryKnife   => EquipSlot.OffHand,
            FullEquipType.Sledgehammer    => EquipSlot.OffHand,
            FullEquipType.GardenScythe    => EquipSlot.OffHand,
            FullEquipType.Gig             => EquipSlot.OffHand,
            _                             => EquipSlot.Unknown,
        };

    public static FullEquipType ToEquipType(this EquipSlot slot, WeaponCategory category = WeaponCategory.Unknown)
        => slot switch
        {
            EquipSlot.Head              => FullEquipType.Head,
            EquipSlot.Body              => FullEquipType.Body,
            EquipSlot.Hands             => FullEquipType.Hands,
            EquipSlot.Legs              => FullEquipType.Legs,
            EquipSlot.Feet              => FullEquipType.Feet,
            EquipSlot.Ears              => FullEquipType.Ears,
            EquipSlot.Neck              => FullEquipType.Neck,
            EquipSlot.Wrists            => FullEquipType.Wrists,
            EquipSlot.RFinger           => FullEquipType.Finger,
            EquipSlot.LFinger           => FullEquipType.Finger,
            EquipSlot.HeadBody          => FullEquipType.Body,
            EquipSlot.BodyHandsLegsFeet => FullEquipType.Body,
            EquipSlot.LegsFeet          => FullEquipType.Legs,
            EquipSlot.FullBody          => FullEquipType.Body,
            EquipSlot.BodyHands         => FullEquipType.Body,
            EquipSlot.BodyLegsFeet      => FullEquipType.Body,
            EquipSlot.ChestHands        => FullEquipType.Body,
            EquipSlot.MainHand          => category.ToEquipType(),
            EquipSlot.OffHand           => category.ToEquipType(),
            EquipSlot.BothHand          => category.ToEquipType(),
            _                           => FullEquipType.Unknown,
        };

    public static FullEquipType ToEquipType(this WeaponCategory category)
        => category switch
        {
            WeaponCategory.Pugilist          => FullEquipType.Fists,
            WeaponCategory.Gladiator         => FullEquipType.Sword,
            WeaponCategory.Marauder          => FullEquipType.Axe,
            WeaponCategory.Archer            => FullEquipType.Bow,
            WeaponCategory.Lancer            => FullEquipType.Lance,
            WeaponCategory.Thaumaturge1      => FullEquipType.Wand,
            WeaponCategory.Thaumaturge2      => FullEquipType.Staff,
            WeaponCategory.Conjurer1         => FullEquipType.Wand,
            WeaponCategory.Conjurer2         => FullEquipType.Staff,
            WeaponCategory.Arcanist          => FullEquipType.Book,
            WeaponCategory.Shield            => FullEquipType.Shield,
            WeaponCategory.CarpenterMain     => FullEquipType.Saw,
            WeaponCategory.CarpenterOff      => FullEquipType.ClawHammer,
            WeaponCategory.BlacksmithMain    => FullEquipType.CrossPeinHammer,
            WeaponCategory.BlacksmithOff     => FullEquipType.File,
            WeaponCategory.ArmorerMain       => FullEquipType.RaisingHammer,
            WeaponCategory.ArmorerOff        => FullEquipType.Pliers,
            WeaponCategory.GoldsmithMain     => FullEquipType.LapidaryHammer,
            WeaponCategory.GoldsmithOff      => FullEquipType.GrindingWheel,
            WeaponCategory.LeatherworkerMain => FullEquipType.Knife,
            WeaponCategory.LeatherworkerOff  => FullEquipType.Awl,
            WeaponCategory.WeaverMain        => FullEquipType.Needle,
            WeaponCategory.WeaverOff         => FullEquipType.SpinningWheel,
            WeaponCategory.AlchemistMain     => FullEquipType.Alembic,
            WeaponCategory.AlchemistOff      => FullEquipType.Mortar,
            WeaponCategory.CulinarianMain    => FullEquipType.Frypan,
            WeaponCategory.CulinarianOff     => FullEquipType.CulinaryKnife,
            WeaponCategory.MinerMain         => FullEquipType.Pickaxe,
            WeaponCategory.MinerOff          => FullEquipType.Sledgehammer,
            WeaponCategory.BotanistMain      => FullEquipType.Hatchet,
            WeaponCategory.BotanistOff       => FullEquipType.GardenScythe,
            WeaponCategory.FisherMain        => FullEquipType.FishingRod,
            WeaponCategory.Rogue             => FullEquipType.Gig,
            WeaponCategory.DarkKnight        => FullEquipType.Broadsword,
            WeaponCategory.Machinist         => FullEquipType.Gun,
            WeaponCategory.Astrologian       => FullEquipType.Orrery,
            WeaponCategory.Samurai           => FullEquipType.Katana,
            WeaponCategory.RedMage           => FullEquipType.Rapier,
            WeaponCategory.Scholar           => FullEquipType.Book,
            WeaponCategory.FisherOff         => FullEquipType.Gig,
            WeaponCategory.BlueMage          => FullEquipType.Cane,
            WeaponCategory.Gunbreaker        => FullEquipType.Gunblade,
            WeaponCategory.Dancer            => FullEquipType.Glaives,
            WeaponCategory.Reaper            => FullEquipType.Scythe,
            WeaponCategory.Sage              => FullEquipType.Nouliths,
            _                                => FullEquipType.Unknown,
        };

    public static FullEquipType Offhand(this FullEquipType type)
        => type switch
        {
            FullEquipType.Fists   => FullEquipType.Fists,
            FullEquipType.Sword   => FullEquipType.Shield,
            FullEquipType.Wand    => FullEquipType.Shield,
            FullEquipType.Daggers => FullEquipType.Daggers,
            FullEquipType.Gun     => FullEquipType.Gun,
            FullEquipType.Orrery  => FullEquipType.Orrery,
            FullEquipType.Rapier  => FullEquipType.Rapier,
            FullEquipType.Glaives => FullEquipType.Glaives,
            _                     => FullEquipType.Unknown,
        };

    public static readonly IReadOnlyList<FullEquipType> WeaponTypes
        = Enum.GetValues<FullEquipType>().Where(v => v.IsWeapon()).ToArray();

    public static readonly IReadOnlyList<FullEquipType> ToolTypes
        = Enum.GetValues<FullEquipType>().Where(v => v.IsTool()).ToArray();

    public static readonly IReadOnlyList<FullEquipType> EquipmentTypes
        = Enum.GetValues<FullEquipType>().Where(v => v.IsEquipment()).ToArray();

    public static readonly IReadOnlyList<FullEquipType> AccessoryTypes
        = Enum.GetValues<FullEquipType>().Where(v => v.IsAccessory()).ToArray();
}
