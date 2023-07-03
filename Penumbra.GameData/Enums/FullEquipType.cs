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

    Fists, // PGL, MNK
    FistsOff,
    Sword, // GLA, PLD Main
    Axe,   // MRD, WAR
    Bow,   // ARC, BRD
    BowOff,
    Lance,   // LNC, DRG,
    Staff,   // THM, BLM, CNJ, WHM
    Wand,    // THM, BLM, CNJ, WHM Main
    Book,    // ACN, SMN, SCH
    Daggers, // ROG, NIN
    DaggersOff,
    Broadsword, // DRK,
    Gun,        // MCH,
    GunOff,
    Orrery, // AST,
    OrreryOff,
    Katana, // SAM
    KatanaOff,
    Rapier, // RDM
    RapierOff,
    Cane,     // BLU
    Gunblade, // GNB,
    Glaives,  // DNC,
    GlaivesOff,
    Scythe,   // RPR,
    Nouliths, // SGE
    Shield,   // GLA, PLD, THM, BLM, CNJ, WHM Off

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
    internal static FullEquipType ToEquipType(this Item item)
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
            FullEquipType.FistsOff        => "Fist Weapon (Offhand)",
            FullEquipType.Sword           => "Sword",
            FullEquipType.Axe             => "Axe",
            FullEquipType.Bow             => "Bow",
            FullEquipType.BowOff          => "Quiver",
            FullEquipType.Lance           => "Lance",
            FullEquipType.Staff           => "Staff",
            FullEquipType.Wand            => "Mace",
            FullEquipType.Book            => "Book",
            FullEquipType.Daggers         => "Dagger",
            FullEquipType.DaggersOff      => "Dagger (Offhand)",
            FullEquipType.Broadsword      => "Broadsword",
            FullEquipType.Gun             => "Gun",
            FullEquipType.GunOff          => "Aetherotransformer",
            FullEquipType.Orrery          => "Orrery",
            FullEquipType.OrreryOff       => "Card Holder",
            FullEquipType.Katana          => "Katana",
            FullEquipType.KatanaOff       => "Sheathe",
            FullEquipType.Rapier          => "Rapier",
            FullEquipType.RapierOff       => "Focus",
            FullEquipType.Cane            => "Cane",
            FullEquipType.Gunblade        => "Gunblade",
            FullEquipType.Glaives         => "Glaive",
            FullEquipType.GlaivesOff      => "Glaive (Offhand)",
            FullEquipType.Scythe          => "Scythe",
            FullEquipType.Nouliths        => "Nouliths",
            FullEquipType.Shield          => "Shield",
            FullEquipType.Saw             => "Saw",
            FullEquipType.CrossPeinHammer => "Cross Pein Hammer",
            FullEquipType.RaisingHammer   => "Raising Hammer",
            FullEquipType.LapidaryHammer  => "Lapidary Hammer",
            FullEquipType.Knife           => "Round Knife",
            FullEquipType.Needle          => "Needle",
            FullEquipType.Alembic         => "Alembic",
            FullEquipType.Frypan          => "Frypan",
            FullEquipType.Pickaxe         => "Pickaxe",
            FullEquipType.Hatchet         => "Hatchet",
            FullEquipType.FishingRod      => "Fishing Rod",
            FullEquipType.ClawHammer      => "Clawhammer",
            FullEquipType.File            => "File",
            FullEquipType.Pliers          => "Pliers",
            FullEquipType.GrindingWheel   => "Grinding Wheel",
            FullEquipType.Awl             => "Awl",
            FullEquipType.SpinningWheel   => "Spinning Wheel",
            FullEquipType.Mortar          => "Mortar",
            FullEquipType.CulinaryKnife   => "Culinary Knife",
            FullEquipType.Sledgehammer    => "Sledgehammer",
            FullEquipType.GardenScythe    => "Garden Scythe",
            FullEquipType.Gig             => "Gig",
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
            FullEquipType.FistsOff        => EquipSlot.OffHand,
            FullEquipType.Sword           => EquipSlot.MainHand,
            FullEquipType.Axe             => EquipSlot.MainHand,
            FullEquipType.Bow             => EquipSlot.MainHand,
            FullEquipType.BowOff          => EquipSlot.OffHand,
            FullEquipType.Lance           => EquipSlot.MainHand,
            FullEquipType.Staff           => EquipSlot.MainHand,
            FullEquipType.Wand            => EquipSlot.MainHand,
            FullEquipType.Book            => EquipSlot.MainHand,
            FullEquipType.Daggers         => EquipSlot.MainHand,
            FullEquipType.DaggersOff      => EquipSlot.OffHand,
            FullEquipType.Broadsword      => EquipSlot.MainHand,
            FullEquipType.Gun             => EquipSlot.MainHand,
            FullEquipType.GunOff          => EquipSlot.OffHand,
            FullEquipType.Orrery          => EquipSlot.MainHand,
            FullEquipType.OrreryOff       => EquipSlot.OffHand,
            FullEquipType.Katana          => EquipSlot.MainHand,
            FullEquipType.KatanaOff       => EquipSlot.OffHand,
            FullEquipType.Rapier          => EquipSlot.MainHand,
            FullEquipType.RapierOff       => EquipSlot.OffHand,
            FullEquipType.Cane            => EquipSlot.MainHand,
            FullEquipType.Gunblade        => EquipSlot.MainHand,
            FullEquipType.Glaives         => EquipSlot.MainHand,
            FullEquipType.GlaivesOff      => EquipSlot.OffHand,
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

    public static FullEquipType ToEquipType(this EquipSlot slot, WeaponCategory category = WeaponCategory.Unknown, bool mainhand = true)
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
            EquipSlot.MainHand          => category.ToEquipType(mainhand),
            EquipSlot.OffHand           => category.ToEquipType(mainhand),
            EquipSlot.BothHand          => category.ToEquipType(mainhand),
            _                           => FullEquipType.Unknown,
        };

    public static FullEquipType ToEquipType(this WeaponCategory category, bool mainhand = true)
        => category switch
        {
            WeaponCategory.Pugilist when mainhand    => FullEquipType.Fists,
            WeaponCategory.Pugilist                  => FullEquipType.FistsOff,
            WeaponCategory.Gladiator                 => FullEquipType.Sword,
            WeaponCategory.Marauder                  => FullEquipType.Axe,
            WeaponCategory.Archer when mainhand      => FullEquipType.Bow,
            WeaponCategory.Archer                    => FullEquipType.BowOff,
            WeaponCategory.Lancer                    => FullEquipType.Lance,
            WeaponCategory.Thaumaturge1              => FullEquipType.Wand,
            WeaponCategory.Thaumaturge2              => FullEquipType.Staff,
            WeaponCategory.Conjurer1                 => FullEquipType.Wand,
            WeaponCategory.Conjurer2                 => FullEquipType.Staff,
            WeaponCategory.Arcanist                  => FullEquipType.Book,
            WeaponCategory.Shield                    => FullEquipType.Shield,
            WeaponCategory.CarpenterMain             => FullEquipType.Saw,
            WeaponCategory.CarpenterOff              => FullEquipType.ClawHammer,
            WeaponCategory.BlacksmithMain            => FullEquipType.CrossPeinHammer,
            WeaponCategory.BlacksmithOff             => FullEquipType.File,
            WeaponCategory.ArmorerMain               => FullEquipType.RaisingHammer,
            WeaponCategory.ArmorerOff                => FullEquipType.Pliers,
            WeaponCategory.GoldsmithMain             => FullEquipType.LapidaryHammer,
            WeaponCategory.GoldsmithOff              => FullEquipType.GrindingWheel,
            WeaponCategory.LeatherworkerMain         => FullEquipType.Knife,
            WeaponCategory.LeatherworkerOff          => FullEquipType.Awl,
            WeaponCategory.WeaverMain                => FullEquipType.Needle,
            WeaponCategory.WeaverOff                 => FullEquipType.SpinningWheel,
            WeaponCategory.AlchemistMain             => FullEquipType.Alembic,
            WeaponCategory.AlchemistOff              => FullEquipType.Mortar,
            WeaponCategory.CulinarianMain            => FullEquipType.Frypan,
            WeaponCategory.CulinarianOff             => FullEquipType.CulinaryKnife,
            WeaponCategory.MinerMain                 => FullEquipType.Pickaxe,
            WeaponCategory.MinerOff                  => FullEquipType.Sledgehammer,
            WeaponCategory.BotanistMain              => FullEquipType.Hatchet,
            WeaponCategory.BotanistOff               => FullEquipType.GardenScythe,
            WeaponCategory.FisherMain                => FullEquipType.FishingRod,
            WeaponCategory.FisherOff                 => FullEquipType.Gig,
            WeaponCategory.Rogue when mainhand       => FullEquipType.Daggers,
            WeaponCategory.Rogue                     => FullEquipType.DaggersOff,
            WeaponCategory.DarkKnight                => FullEquipType.Broadsword,
            WeaponCategory.Machinist when mainhand   => FullEquipType.Gun,
            WeaponCategory.Machinist                 => FullEquipType.GunOff,
            WeaponCategory.Astrologian when mainhand => FullEquipType.Orrery,
            WeaponCategory.Astrologian               => FullEquipType.OrreryOff,
            WeaponCategory.Samurai when mainhand     => FullEquipType.Katana,
            WeaponCategory.Samurai                   => FullEquipType.KatanaOff,
            WeaponCategory.RedMage when mainhand     => FullEquipType.Rapier,
            WeaponCategory.RedMage                   => FullEquipType.RapierOff,
            WeaponCategory.Scholar                   => FullEquipType.Book,
            WeaponCategory.BlueMage                  => FullEquipType.Cane,
            WeaponCategory.Gunbreaker                => FullEquipType.Gunblade,
            WeaponCategory.Dancer when mainhand      => FullEquipType.Glaives,
            WeaponCategory.Dancer                    => FullEquipType.GlaivesOff,
            WeaponCategory.Reaper                    => FullEquipType.Scythe,
            WeaponCategory.Sage                      => FullEquipType.Nouliths,
            _                                        => FullEquipType.Unknown,
        };

    public static FullEquipType Offhand(this FullEquipType type)
        => type switch
        {
            FullEquipType.Fists           => FullEquipType.FistsOff,
            FullEquipType.Sword           => FullEquipType.Shield,
            FullEquipType.Wand            => FullEquipType.Shield,
            FullEquipType.Daggers         => FullEquipType.DaggersOff,
            FullEquipType.Gun             => FullEquipType.GunOff,
            FullEquipType.Orrery          => FullEquipType.OrreryOff,
            FullEquipType.Rapier          => FullEquipType.RapierOff,
            FullEquipType.Glaives         => FullEquipType.GlaivesOff,
            FullEquipType.Bow             => FullEquipType.BowOff,
            FullEquipType.Katana          => FullEquipType.KatanaOff,
            FullEquipType.Saw             => FullEquipType.ClawHammer,
            FullEquipType.CrossPeinHammer => FullEquipType.File,
            FullEquipType.RaisingHammer   => FullEquipType.Pliers,
            FullEquipType.LapidaryHammer  => FullEquipType.GrindingWheel,
            FullEquipType.Knife           => FullEquipType.Awl,
            FullEquipType.Needle          => FullEquipType.SpinningWheel,
            FullEquipType.Alembic         => FullEquipType.Mortar,
            FullEquipType.Frypan          => FullEquipType.CulinaryKnife,
            FullEquipType.Pickaxe         => FullEquipType.Sledgehammer,
            FullEquipType.Hatchet         => FullEquipType.GardenScythe,
            FullEquipType.FishingRod      => FullEquipType.Gig,
            _                             => FullEquipType.Unknown,
        };

    internal static string OffhandTypeSuffix(this FullEquipType type)
        => type switch
        {
            FullEquipType.FistsOff   => " (Offhand)",
            FullEquipType.DaggersOff => " (Offhand)",
            FullEquipType.GunOff     => " (Aetherotransformer)",
            FullEquipType.OrreryOff  => " (Card Holder)",
            FullEquipType.RapierOff  => " (Focus)",
            FullEquipType.GlaivesOff => " (Offhand)",
            FullEquipType.BowOff     => " (Quiver)",
            FullEquipType.KatanaOff  => " (Sheathe)",
            _                        => string.Empty,
        };

    public static bool IsOffhandType(this FullEquipType type)
        => type.OffhandTypeSuffix().Length > 0;

    public static readonly IReadOnlyList<FullEquipType> WeaponTypes
        = Enum.GetValues<FullEquipType>().Where(v => v.IsWeapon()).ToArray();

    public static readonly IReadOnlyList<FullEquipType> ToolTypes
        = Enum.GetValues<FullEquipType>().Where(v => v.IsTool()).ToArray();

    public static readonly IReadOnlyList<FullEquipType> EquipmentTypes
        = Enum.GetValues<FullEquipType>().Where(v => v.IsEquipment()).ToArray();

    public static readonly IReadOnlyList<FullEquipType> AccessoryTypes
        = Enum.GetValues<FullEquipType>().Where(v => v.IsAccessory()).ToArray();

    public static readonly IReadOnlyList<FullEquipType> OffhandTypes
        = Enum.GetValues<FullEquipType>().Where(v => v.OffhandTypeSuffix().Length > 0).ToArray();
}
