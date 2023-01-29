using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud;
using Dalamud.Data;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Utility;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Race = Penumbra.GameData.Enums.Race;

namespace Penumbra.GameData.Data;

/// <summary>
/// Handle gender- or race-locked gear in the draw model itself.
/// Racial gear gets swapped to the correct current race and gender (it is one set each).
/// Gender-locked gear gets swapped to the equivalent set if it exists (most of them do), 
/// with some items getting send to emperor's new clothes and a few funny entries.
/// </summary>
public sealed class RestrictedGear : DataSharer
{
    private readonly ExcelSheet<Item>              _items;
    private readonly ExcelSheet<EquipRaceCategory> _categories;

    public readonly IReadOnlySet<uint>              RaceGenderSet;
    public readonly IReadOnlyDictionary<uint, uint> MaleToFemale;
    public readonly IReadOnlyDictionary<uint, uint> FemaleToMale;

    public RestrictedGear(DalamudPluginInterface pi, ClientLanguage language, DataManager gameData)
        : base(pi, language, 1)
    {
        _items                                      = gameData.GetExcelSheet<Item>()!;
        _categories                                 = gameData.GetExcelSheet<EquipRaceCategory>()!;
        (RaceGenderSet, MaleToFemale, FemaleToMale) = TryCatchData("RestrictedGear", CreateRestrictedGear);
    }

    protected override void DisposeInternal()
        => DisposeTag("RestrictedGear");

    /// <summary>
    /// Resolve a model given by its model id, variant and slot for your current race and gender.
    /// </summary>
    /// <param name="armor">The equipment piece.</param>
    /// <param name="slot">The equipment slot.</param>
    /// <param name="race">The intended race.</param>
    /// <param name="gender">The intended gender.</param>
    /// <returns>True and the changed-to piece of gear or false and the same piece of gear.</returns>
    public (bool Replaced, CharacterArmor Armor) ResolveRestricted(CharacterArmor armor, EquipSlot slot, Race race, Gender gender)
    {
        var quad = armor.Set.Value | ((uint)armor.Variant << 16);
        // Check racial gear, this does not need slots.
        if (RaceGenderGroup.Contains(quad))
        {
            var idx   = ((int)race - 1) * 2 + (gender is Gender.Female or Gender.FemaleNpc ? 1 : 0);
            var value = RaceGenderGroup[idx];
            return (value != quad, new CharacterArmor((ushort)value, (byte)(value >> 16), armor.Stain));
        }

        // Check gender slots. If current gender is female, check if anything needs to be changed from male to female,
        // and vice versa.
        // Some items lead to the exact same model- and variant id just gender specified, 
        // so check for actual difference in the Replaced bool.
        var needle = quad | ((uint)slot.ToSlot() << 24);
        if (gender is Gender.Female or Gender.FemaleNpc && MaleToFemale.TryGetValue(needle, out var newValue)
         || gender is Gender.Male or Gender.MaleNpc && FemaleToMale.TryGetValue(needle, out newValue))
            return (quad != newValue, new CharacterArmor((ushort)newValue, (byte)(newValue >> 16), armor.Stain));

        // The gear is not restricted.
        return (false, armor);
    }

    private Tuple<IReadOnlySet<uint>, IReadOnlyDictionary<uint, uint>, IReadOnlyDictionary<uint, uint>> CreateRestrictedGear()
    {
        var m2f = new Dictionary<uint, uint>();
        var f2m = new Dictionary<uint, uint>();
        var rg  = RaceGenderGroup.Where(c => c is not 0 and not uint.MaxValue).ToHashSet();
        AddKnown(m2f, f2m);
        UnhandledRestrictedGear(rg, m2f, f2m, false); // Set this to true to create a print of unassigned gear on launch.
        return new Tuple<IReadOnlySet<uint>, IReadOnlyDictionary<uint, uint>, IReadOnlyDictionary<uint, uint>>(rg, m2f, f2m);
    }


    // Add all unknown restricted gear and pair it with emperor's new gear on start up.
    // Can also print unhandled items.
    private void UnhandledRestrictedGear(IReadOnlySet<uint> rg, Dictionary<uint, uint> m2f, Dictionary<uint, uint> f2m, bool print)
    {
        if (print)
            PluginLog.Information("#### MALE ONLY ######");

        void AddEmperor(Item item, bool male, bool female)
        {
            var slot = ((EquipSlot)item.EquipSlotCategory.Row).ToSlot();
            var emperor = slot switch
            {
                EquipSlot.Head    => 10032u,
                EquipSlot.Body    => 10033u,
                EquipSlot.Hands   => 10034u,
                EquipSlot.Legs    => 10035u,
                EquipSlot.Feet    => 10036u,
                EquipSlot.Ears    => 09293u,
                EquipSlot.Neck    => 09292u,
                EquipSlot.Wrists  => 09294u,
                EquipSlot.RFinger => 09295u,
                EquipSlot.LFinger => 09295u,
                _                 => 0u,
            };
            if (emperor == 0)
                return;

            if (male)
                AddItem(m2f, f2m, item.RowId, emperor, true, false);
            if (female)
                AddItem(m2f, f2m, emperor, item.RowId, false, true);
        }

        var unhandled = 0;
        foreach (var item in _items.Where(i => i.EquipRestriction == 2))
        {
            if (m2f.ContainsKey((uint)item.ModelMain | ((uint)((EquipSlot)item.EquipSlotCategory.Row).ToSlot() << 24)))
                continue;

            ++unhandled;
            AddEmperor(item, true, false);

            if (print)
                PluginLog.Information($"{item.RowId:D5} {item.Name.ToDalamudString().TextValue}");
        }

        if (print)
            PluginLog.Information("#### FEMALE ONLY ####");
        foreach (var item in _items.Where(i => i.EquipRestriction == 3))
        {
            if (f2m.ContainsKey((uint)item.ModelMain | ((uint)((EquipSlot)item.EquipSlotCategory.Row).ToSlot() << 24)))
                continue;

            ++unhandled;
            AddEmperor(item, false, true);

            if (print)
                PluginLog.Information($"{item.RowId:D5} {item.Name.ToDalamudString().TextValue}");
        }

        if (print)
            PluginLog.Information("#### OTHER #########");

        foreach (var item in _items.Where(i => i.EquipRestriction > 3))
        {
            if (rg.Contains((uint)item.ModelMain))
                continue;

            ++unhandled;
            if (print)
                PluginLog.Information(
                    $"{item.RowId:D5} {item.Name.ToDalamudString().TextValue} RestrictionGroup {_categories.GetRow(item.EquipRestriction)!.RowId:D2}");
        }

        if (unhandled > 0)
            PluginLog.Warning($"There were {unhandled} restricted items not handled and directed to Emperor's New Set.");
    }

    // Add a item redirection by its item - NOT MODEL - id.
    // This uses the items model as well as its slot.
    // Creates a <-> redirection by default but can add -> or <- redirections by setting the corresponding bools to false.
    // Prints warnings if anything does not make sense.
    private void AddItem(Dictionary<uint, uint> m2f, Dictionary<uint, uint> f2m, uint itemIdMale, uint itemIdFemale, bool addMale = true,
        bool addFemale = true)
    {
        if (!addMale && !addFemale)
            return;

        var mItem = _items.GetRow(itemIdMale);
        var fItem = _items.GetRow(itemIdFemale);
        if (mItem == null || fItem == null)
        {
            PluginLog.Warning($"Could not add item {itemIdMale} or {itemIdFemale} to restricted items.");
            return;
        }

        if (mItem.EquipRestriction != 2 && addMale)
        {
            PluginLog.Warning($"{mItem.Name.ToDalamudString().TextValue} is not restricted anymore.");
            return;
        }

        if (fItem.EquipRestriction != 3 && addFemale)
        {
            PluginLog.Warning($"{fItem.Name.ToDalamudString().TextValue} is not restricted anymore.");
            return;
        }

        var mSlot = ((EquipSlot)mItem.EquipSlotCategory.Row).ToSlot();
        var fSlot = ((EquipSlot)fItem.EquipSlotCategory.Row).ToSlot();
        if (!mSlot.IsAccessory() && !mSlot.IsEquipment())
        {
            PluginLog.Warning($"{mItem.Name.ToDalamudString().TextValue} is not equippable to a known slot.");
            return;
        }

        if (mSlot != fSlot)
        {
            PluginLog.Warning($"{mItem.Name.ToDalamudString().TextValue} and {fItem.Name.ToDalamudString().TextValue} are not compatible.");
            return;
        }

        var mModelIdSlot = (uint)mItem.ModelMain | ((uint)mSlot << 24);
        var fModelIdSlot = (uint)fItem.ModelMain | ((uint)fSlot << 24);

        if (addMale)
            m2f.TryAdd(mModelIdSlot, fModelIdSlot);
        if (addFemale)
            f2m.TryAdd(fModelIdSlot, mModelIdSlot);
    }

    // @formatter:off
    // Add all currently existing and known gender restricted items.
    private void AddKnown(Dictionary<uint, uint> m2f, Dictionary<uint, uint> f2m)
    {
        AddItem(m2f, f2m, 02967, 02970);              // Lord's Yukata (Blue)                       <-> Lady's Yukata (Red)
        AddItem(m2f, f2m, 02968, 02971);              // Lord's Yukata (Green)                      <-> Lady's Yukata (Blue)
        AddItem(m2f, f2m, 02969, 02972);              // Lord's Yukata (Grey)                       <-> Lady's Yukata (Black)
        AddItem(m2f, f2m, 02973, 02978);              // Red Summer Top                             <-> Red Summer Halter
        AddItem(m2f, f2m, 02974, 02979);              // Green Summer Top                           <-> Green Summer Halter
        AddItem(m2f, f2m, 02975, 02980);              // Blue Summer Top                            <-> Blue Summer Halter
        AddItem(m2f, f2m, 02976, 02981);              // Solar Summer Top                           <-> Solar Summer Halter
        AddItem(m2f, f2m, 02977, 02982);              // Lunar Summer Top                           <-> Lunar Summer Halter
        AddItem(m2f, f2m, 02996, 02997);              // Hempen Undershirt                          <-> Hempen Camise
        AddItem(m2f, f2m, 03280, 03283);              // Lord's Drawers (Black)                     <-> Lady's Knickers (Black)
        AddItem(m2f, f2m, 03281, 03284);              // Lord's Drawers (White)                     <-> Lady's Knickers (White)
        AddItem(m2f, f2m, 03282, 03285);              // Lord's Drawers (Gold)                      <-> Lady's Knickers (Gold)
        AddItem(m2f, f2m, 03286, 03291);              // Red Summer Trunks                          <-> Red Summer Tanga
        AddItem(m2f, f2m, 03287, 03292);              // Green Summer Trunks                        <-> Green Summer Tanga
        AddItem(m2f, f2m, 03288, 03293);              // Blue Summer Trunks                         <-> Blue Summer Tanga
        AddItem(m2f, f2m, 03289, 03294);              // Solar Summer Trunks                        <-> Solar Summer Tanga
        AddItem(m2f, f2m, 03290, 03295);              // Lunar Summer Trunks                        <-> Lunar Summer Tanga
        AddItem(m2f, f2m, 03307, 03308);              // Hempen Underpants                          <-> Hempen Pantalettes
        AddItem(m2f, f2m, 03748, 03749);              // Lord's Clogs                               <-> Lady's Clogs
        AddItem(m2f, f2m, 06045, 06041);              // Bohemian's Coat                            <-> Guardian Corps Coat
        AddItem(m2f, f2m, 06046, 06042);              // Bohemian's Gloves                          <-> Guardian Corps Gauntlets
        AddItem(m2f, f2m, 06047, 06043);              // Bohemian's Trousers                        <-> Guardian Corps Skirt
        AddItem(m2f, f2m, 06048, 06044);              // Bohemian's Boots                           <-> Guardian Corps Boots
        AddItem(m2f, f2m, 06094, 06098);              // Summer Evening Top                         <-> Summer Morning Halter
        AddItem(m2f, f2m, 06095, 06099);              // Summer Evening Trunks                      <-> Summer Morning Tanga
        AddItem(m2f, f2m, 06096, 06100);              // Striped Summer Top                         <-> Striped Summer Halter
        AddItem(m2f, f2m, 06097, 06101);              // Striped Summer Trunks                      <-> Striped Summer Tanga
        AddItem(m2f, f2m, 06102, 06104);              // Black Summer Top                           <-> Black Summer Halter
        AddItem(m2f, f2m, 06103, 06105);              // Black Summer Trunks                        <-> Black Summer Tanga
        AddItem(m2f, f2m, 08532, 08535);              // Lord's Yukata (Blackflame)                 <-> Lady's Yukata (Redfly)
        AddItem(m2f, f2m, 08533, 08536);              // Lord's Yukata (Whiteflame)                 <-> Lady's Yukata (Bluefly)
        AddItem(m2f, f2m, 08534, 08537);              // Lord's Yukata (Blueflame)                  <-> Lady's Yukata (Pinkfly)
        AddItem(m2f, f2m, 08542, 08549);              // Ti Leaf Lei                                <-> Coronal Summer Halter
        AddItem(m2f, f2m, 08543, 08550);              // Red Summer Maro                            <-> Red Summer Pareo
        AddItem(m2f, f2m, 08544, 08551);              // South Seas Talisman                        <-> Sea Breeze Summer Halter
        AddItem(m2f, f2m, 08545, 08552);              // Blue Summer Maro                           <-> Sea Breeze Summer Pareo
        AddItem(m2f, f2m, 08546, 08553);              // Coeurl Talisman                            <-> Coeurl Beach Halter
        AddItem(m2f, f2m, 08547, 08554);              // Coeurl Beach Maro                          <-> Coeurl Beach Pareo
        AddItem(m2f, f2m, 08548, 08555);              // Coeurl Beach Briefs                        <-> Coeurl Beach Tanga
        AddItem(m2f, f2m, 10316, 10317);              // Southern Seas Vest                         <-> Southern Seas Swimsuit
        AddItem(m2f, f2m, 10318, 10319);              // Southern Seas Trunks                       <-> Southern Seas Tanga
        AddItem(m2f, f2m, 10320, 10321);              // Striped Southern Seas Vest                 <-> Striped Southern Seas Swimsuit
        AddItem(m2f, f2m, 13298, 13567);              // Black-feathered Flat Hat                   <-> Red-feathered Flat Hat
        AddItem(m2f, f2m, 13300, 13639);              // Lord's Suikan                              <-> Lady's Suikan
        AddItem(m2f, f2m, 13724, 13725);              // Little Lord's Clogs                        <-> Little Lady's Clogs
        AddItem(m2f, f2m, 14854, 14857);              // Eastern Lord's Togi                        <-> Eastern Lady's Togi
        AddItem(m2f, f2m, 14855, 14858);              // Eastern Lord's Trousers                    <-> Eastern Lady's Loincloth
        AddItem(m2f, f2m, 14856, 14859);              // Eastern Lord's Crakows                     <-> Eastern Lady's Crakows
        AddItem(m2f, f2m, 15639, 15642);              // Far Eastern Patriarch's Hat                <-> Far Eastern Matriarch's Sun Hat
        AddItem(m2f, f2m, 15640, 15643);              // Far Eastern Patriarch's Tunic              <-> Far Eastern Matriarch's Dress
        AddItem(m2f, f2m, 15641, 15644);              // Far Eastern Patriarch's Longboots          <-> Far Eastern Matriarch's Boots
        AddItem(m2f, f2m, 15922, 15925);              // Moonfire Vest                              <-> Moonfire Halter
        AddItem(m2f, f2m, 15923, 15926);              // Moonfire Trunks                            <-> Moonfire Tanga
        AddItem(m2f, f2m, 15924, 15927);              // Moonfire Caligae                           <-> Moonfire Sandals
        AddItem(m2f, f2m, 16106, 16111);              // Makai Mauler's Facemask                    <-> Makai Manhandler's Facemask
        AddItem(m2f, f2m, 16107, 16112);              // Makai Mauler's Oilskin                     <-> Makai Manhandler's Jerkin
        AddItem(m2f, f2m, 16108, 16113);              // Makai Mauler's Fingerless Gloves           <-> Makai Manhandler's Fingerless Gloves
        AddItem(m2f, f2m, 16109, 16114);              // Makai Mauler's Leggings                    <-> Makai Manhandler's Quartertights
        AddItem(m2f, f2m, 16110, 16115);              // Makai Mauler's Boots                       <-> Makai Manhandler's Longboots
        AddItem(m2f, f2m, 16116, 16121);              // Makai Marksman's Eyepatch                  <-> Makai Markswoman's Ribbon
        AddItem(m2f, f2m, 16117, 16122);              // Makai Marksman's Battlegarb                <-> Makai Markswoman's Battledress
        AddItem(m2f, f2m, 16118, 16123);              // Makai Marksman's Fingerless Gloves         <-> Makai Markswoman's Fingerless Gloves
        AddItem(m2f, f2m, 16119, 16124);              // Makai Marksman's Slops                     <-> Makai Markswoman's Quartertights
        AddItem(m2f, f2m, 16120, 16125);              // Makai Marksman's Boots                     <-> Makai Markswoman's Longboots
        AddItem(m2f, f2m, 16126, 16131);              // Makai Sun Guide's Circlet                  <-> Makai Moon Guide's Circlet
        AddItem(m2f, f2m, 16127, 16132);              // Makai Sun Guide's Oilskin                  <-> Makai Moon Guide's Gown
        AddItem(m2f, f2m, 16128, 16133);              // Makai Sun Guide's Fingerless Gloves        <-> Makai Moon Guide's Fingerless Gloves
        AddItem(m2f, f2m, 16129, 16134);              // Makai Sun Guide's Slops                    <-> Makai Moon Guide's Quartertights
        AddItem(m2f, f2m, 16130, 16135);              // Makai Sun Guide's Boots                    <-> Makai Moon Guide's Longboots
        AddItem(m2f, f2m, 16136, 16141);              // Makai Priest's Coronet                     <-> Makai Priestess's Headdress
        AddItem(m2f, f2m, 16137, 16142);              // Makai Priest's Doublet Robe                <-> Makai Priestess's Jerkin
        AddItem(m2f, f2m, 16138, 16143);              // Makai Priest's Fingerless Gloves           <-> Makai Priestess's Fingerless Gloves
        AddItem(m2f, f2m, 16139, 16144);              // Makai Priest's Slops                       <-> Makai Priestess's Skirt
        AddItem(m2f, f2m, 16140, 16145);              // Makai Priest's Boots                       <-> Makai Priestess's Longboots
        AddItem(m2f, f2m, 16588, 16592);              // Far Eastern Gentleman's Hat                <-> Far Eastern Beauty's Hairpin
        AddItem(m2f, f2m, 16589, 16593);              // Far Eastern Gentleman's Robe               <-> Far Eastern Beauty's Robe
        AddItem(m2f, f2m, 16590, 16594);              // Far Eastern Gentleman's Haidate            <-> Far Eastern Beauty's Koshita
        AddItem(m2f, f2m, 16591, 16595);              // Far Eastern Gentleman's Boots              <-> Far Eastern Beauty's Boots
        AddItem(m2f, f2m, 17204, 17209);              // Common Makai Mauler's Facemask             <-> Common Makai Manhandler's Facemask
        AddItem(m2f, f2m, 17205, 17210);              // Common Makai Mauler's Oilskin              <-> Common Makai Manhandler's Jerkin
        AddItem(m2f, f2m, 17206, 17211);              // Common Makai Mauler's Fingerless Gloves    <-> Common Makai Manhandler's Fingerless Glove
        AddItem(m2f, f2m, 17207, 17212);              // Common Makai Mauler's Leggings             <-> Common Makai Manhandler's Quartertights
        AddItem(m2f, f2m, 17208, 17213);              // Common Makai Mauler's Boots                <-> Common Makai Manhandler's Longboots
        AddItem(m2f, f2m, 17214, 17219);              // Common Makai Marksman's Eyepatch           <-> Common Makai Markswoman's Ribbon
        AddItem(m2f, f2m, 17215, 17220);              // Common Makai Marksman's Battlegarb         <-> Common Makai Markswoman's Battledress
        AddItem(m2f, f2m, 17216, 17221);              // Common Makai Marksman's Fingerless Gloves  <-> Common Makai Markswoman's Fingerless Glove
        AddItem(m2f, f2m, 17217, 17222);              // Common Makai Marksman's Slops              <-> Common Makai Markswoman's Quartertights
        AddItem(m2f, f2m, 17218, 17223);              // Common Makai Marksman's Boots              <-> Common Makai Markswoman's Longboots
        AddItem(m2f, f2m, 17224, 17229);              // Common Makai Sun Guide's Circlet           <-> Common Makai Moon Guide's Circlet
        AddItem(m2f, f2m, 17225, 17230);              // Common Makai Sun Guide's Oilskin           <-> Common Makai Moon Guide's Gown
        AddItem(m2f, f2m, 17226, 17231);              // Common Makai Sun Guide's Fingerless Gloves <-> Common Makai Moon Guide's Fingerless Glove
        AddItem(m2f, f2m, 17227, 17232);              // Common Makai Sun Guide's Slops             <-> Common Makai Moon Guide's Quartertights
        AddItem(m2f, f2m, 17228, 17233);              // Common Makai Sun Guide's Boots             <-> Common Makai Moon Guide's Longboots
        AddItem(m2f, f2m, 17234, 17239);              // Common Makai Priest's Coronet              <-> Common Makai Priestess's Headdress
        AddItem(m2f, f2m, 17235, 17240);              // Common Makai Priest's Doublet Robe         <-> Common Makai Priestess's Jerkin
        AddItem(m2f, f2m, 17236, 17241);              // Common Makai Priest's Fingerless Gloves    <-> Common Makai Priestess's Fingerless Gloves
        AddItem(m2f, f2m, 17237, 17242);              // Common Makai Priest's Slops                <-> Common Makai Priestess's Skirt
        AddItem(m2f, f2m, 17238, 17243);              // Common Makai Priest's Boots                <-> Common Makai Priestess's Longboots
        AddItem(m2f, f2m, 20479, 20484);              // Star of the Nezha Lord                     <-> Star of the Nezha Lady
        AddItem(m2f, f2m, 20480, 20485);              // Nezha Lord's Togi                          <-> Nezha Lady's Togi
        AddItem(m2f, f2m, 20481, 20486);              // Nezha Lord's Gloves                        <-> Nezha Lady's Gloves
        AddItem(m2f, f2m, 20482, 20487);              // Nezha Lord's Slops                         <-> Nezha Lady's Slops
        AddItem(m2f, f2m, 20483, 20488);              // Nezha Lord's Boots                         <-> Nezha Lady's Kneeboots
        AddItem(m2f, f2m, 22367, 22372);              // Faerie Tale Prince's Circlet               <-> Faerie Tale Princess's Tiara
        AddItem(m2f, f2m, 22368, 22373);              // Faerie Tale Prince's Vest                  <-> Faerie Tale Princess's Dress
        AddItem(m2f, f2m, 22369, 22374);              // Faerie Tale Prince's Gloves                <-> Faerie Tale Princess's Gloves
        AddItem(m2f, f2m, 22370, 22375);              // Faerie Tale Prince's Slops                 <-> Faerie Tale Princess's Long Skirt
        AddItem(m2f, f2m, 22371, 22376);              // Faerie Tale Prince's Boots                 <-> Faerie Tale Princess's Heels
        AddItem(m2f, f2m, 24599, 24602);              // Far Eastern Schoolboy's Hat                <-> Far Eastern Schoolgirl's Hair Ribbon
        AddItem(m2f, f2m, 24600, 24603);              // Far Eastern Schoolboy's Hakama             <-> Far Eastern Schoolgirl's Hakama
        AddItem(m2f, f2m, 24601, 24604);              // Far Eastern Schoolboy's Zori               <-> Far Eastern Schoolgirl's Boots
        AddItem(m2f, f2m, 28600, 28605);              // Eastern Lord Errant's Hat                  <-> Eastern Lady Errant's Hat
        AddItem(m2f, f2m, 28601, 28606);              // Eastern Lord Errant's Jacket               <-> Eastern Lady Errant's Coat
        AddItem(m2f, f2m, 28602, 28607);              // Eastern Lord Errant's Wristbands           <-> Eastern Lady Errant's Gloves
        AddItem(m2f, f2m, 28603, 28608);              // Eastern Lord Errant's Trousers             <-> Eastern Lady Errant's Skirt
        AddItem(m2f, f2m, 28604, 28609);              // Eastern Lord Errant's Shoes                <-> Eastern Lady Errant's Boots
        AddItem(m2f, f2m, 36336, 36337);              // Omega-M Attire                             <-> Omega-F Attire
        AddItem(m2f, f2m, 36338, 36339);              // Omega-M Ear Cuffs                          <-> Omega-F Earrings
        AddItem(m2f, f2m, 37442, 37447);              // Makai Vanguard's Monocle                   <-> Makai Vanbreaker's Ribbon
        AddItem(m2f, f2m, 37443, 37448);              // Makai Vanguard's Battlegarb                <-> Makai Vanbreaker's Battledress
        AddItem(m2f, f2m, 37444, 37449);              // Makai Vanguard's Fingerless Gloves         <-> Makai Vanbreaker's Fingerless Gloves
        AddItem(m2f, f2m, 37445, 37450);              // Makai Vanguard's Leggings                  <-> Makai Vanbreaker's Quartertights
        AddItem(m2f, f2m, 37446, 37451);              // Makai Vanguard's Boots                     <-> Makai Vanbreaker's Longboots
        AddItem(m2f, f2m, 37452, 37457);              // Makai Harbinger's Facemask                 <-> Makai Harrower's Facemask
        AddItem(m2f, f2m, 37453, 37458);              // Makai Harbinger's Battlegarb               <-> Makai Harrower's Jerkin
        AddItem(m2f, f2m, 37454, 37459);              // Makai Harbinger's Fingerless Gloves        <-> Makai Harrower's Fingerless Gloves
        AddItem(m2f, f2m, 37455, 37460);              // Makai Harbinger's Leggings                 <-> Makai Harrower's Quartertights
        AddItem(m2f, f2m, 37456, 37461);              // Makai Harbinger's Boots                    <-> Makai Harrower's Longboots
        AddItem(m2f, f2m, 37462, 37467);              // Common Makai Vanguard's Monocle            <-> Common Makai Vanbreaker's Ribbon
        AddItem(m2f, f2m, 37463, 37468);              // Common Makai Vanguard's Battlegarb         <-> Common Makai Vanbreaker's Battledress
        AddItem(m2f, f2m, 37464, 37469);              // Common Makai Vanguard's Fingerless Gloves  <-> Common Makai Vanbreaker's Fingerless Gloves
        AddItem(m2f, f2m, 37465, 37470);              // Common Makai Vanguard's Leggings           <-> Common Makai Vanbreaker's Quartertights
        AddItem(m2f, f2m, 37466, 37471);              // Common Makai Vanguard's Boots              <-> Common Makai Vanbreaker's Longboots
        AddItem(m2f, f2m, 37472, 37477);              // Common Makai Harbinger's Facemask          <-> Common Makai Harrower's Facemask
        AddItem(m2f, f2m, 37473, 37478);              // Common Makai Harbinger's Battlegarb        <-> Common Makai Harrower's Jerkin
        AddItem(m2f, f2m, 37474, 37479);              // Common Makai Harbinger's Fingerless Gloves <-> Common Makai Harrower's Fingerless Gloves
        AddItem(m2f, f2m, 37475, 37480);              // Common Makai Harbinger's Leggings          <-> Common Makai Harrower's Quartertights
        AddItem(m2f, f2m, 37476, 37481);              // Common Makai Harbinger's Boots             <-> Common Makai Harrower's Longboots
        AddItem(m2f, f2m, 13323, 13322);              // Scion Thief's Tunic                        <-> Scion Conjurer's Dalmatica
        AddItem(m2f, f2m, 13693, 10034, true, false); // Scion Thief's Halfgloves                    -> The Emperor's New Gloves
        AddItem(m2f, f2m, 13694, 13691);              // Scion Thief's Gaskins                      <-> Scion Conjurer's Chausses
        AddItem(m2f, f2m, 13695, 13692);              // Scion Thief's Armored Caligae              <-> Scion Conjurer's Pattens
        AddItem(m2f, f2m, 13326, 30063);              // Scion Thaumaturge's Robe                   <-> Scion Sorceress's Headdress
        AddItem(m2f, f2m, 13696, 30062);              // Scion Thaumaturge's Monocle                <-> Scion Sorceress's Robe
        AddItem(m2f, f2m, 13697, 30064);              // Scion Thaumaturge's Gauntlets              <-> Scion Sorceress's Shadowtalons
        AddItem(m2f, f2m, 13698, 10035, true, false); // Scion Thaumaturge's Gaskins                 -> The Emperor's New Breeches
        AddItem(m2f, f2m, 13699, 30065);              // Scion Thaumaturge's Moccasins              <-> Scion Sorceress's High Boots
        AddItem(m2f, f2m, 13327, 15942);              // Scion Chronocler's Cowl                    <-> Scion Healer's Robe
        AddItem(m2f, f2m, 13700, 10034, true, false); // Scion Chronocler's Ringbands                -> The Emperor's New Gloves
        AddItem(m2f, f2m, 13701, 15943);              // Scion Chronocler's Tights                  <-> Scion Healer's Halftights
        AddItem(m2f, f2m, 13702, 15944);              // Scion Chronocler's Caligae                 <-> Scion Healer's Highboots
        AddItem(m2f, f2m, 14861, 13324);              // Head Engineer's Goggles                    <-> Scion Striker's Visor
        AddItem(m2f, f2m, 14862, 13325);              // Head Engineer's Attire                     <-> Scion Striker's Attire
        AddItem(m2f, f2m, 15938, 33751);              // Scion Rogue's Jacket                       <-> Oracle Top
        AddItem(m2f, f2m, 15939, 10034, true, false); // Scion Rogue's Armguards                     -> The Emperor's New Gloves
        AddItem(m2f, f2m, 15940, 33752);              // Scion Rogue's Gaskins                      <-> Oracle Leggings
        AddItem(m2f, f2m, 15941, 33753);              // Scion Rogue's Boots                        <-> Oracle Pantalettes
        AddItem(m2f, f2m, 16042, 16046);              // Abes Jacket                                <-> High Summoner's Dress
        AddItem(m2f, f2m, 16043, 16047);              // Abes Gloves                                <-> High Summoner's Armlets
        AddItem(m2f, f2m, 16044, 10035, true, false); // Abes Halfslops                              -> The Emperor's New Breeches
        AddItem(m2f, f2m, 16045, 16048);              // Abes Boots                                 <-> High Summoner's Boots
        AddItem(m2f, f2m, 17473, 28553);              // Lord Commander's Coat                      <-> Majestic Dress
        AddItem(m2f, f2m, 17474, 28554);              // Lord Commander's Gloves                    <-> Majestic Wristdresses
        AddItem(m2f, f2m, 10036, 28555, false);       // Emperor's New Boots                        <-  Majestic Boots
        AddItem(m2f, f2m, 21021, 21026);              // Werewolf Feet                              <-> Werewolf Legs
        AddItem(m2f, f2m, 22452, 20633);              // Cracked Manderville Monocle                <-> Blackbosom Hat
        AddItem(m2f, f2m, 22453, 20634);              // Torn Manderville Coatee                    <-> Blackbosom Dress
        AddItem(m2f, f2m, 22454, 20635);              // Singed Manderville Gloves                  <-> Blackbosom Dress Gloves
        AddItem(m2f, f2m, 22455, 10035, true, false); // Stained Manderville Bottoms                 -> The Emperor's New Breeches
        AddItem(m2f, f2m, 22456, 20636);              // Scuffed Manderville Gaiters                <-> lackbosom Boots
        AddItem(m2f, f2m, 23013, 21302);              // Doman Liege's Dogi                         <-> Scion Liberator's Jacket
        AddItem(m2f, f2m, 23014, 21303);              // Doman Liege's Kote                         <-> Scion Liberator's Fingerless Gloves
        AddItem(m2f, f2m, 23015, 21304);              // Doman Liege's Kyakui                       <-> Scion Liberator's Pantalettes
        AddItem(m2f, f2m, 23016, 21305);              // Doman Liege's Kyahan                       <-> Scion Liberator's Sabatons
        AddItem(m2f, f2m, 09293, 21306, false);       // The Emperor's New Earrings                 <-  Scion Liberator's Earrings
        AddItem(m2f, f2m, 24158, 23008, true, false); // Leal Samurai's Kasa                         -> Eastern Socialite's Hat
        AddItem(m2f, f2m, 24159, 23009, true, false); // Leal Samurai's Dogi                         -> Eastern Socialite's Cheongsam
        AddItem(m2f, f2m, 24160, 23010, true, false); // Leal Samurai's Tekko                        -> Eastern Socialite's Gloves
        AddItem(m2f, f2m, 24161, 23011, true, false); // Leal Samurai's Tsutsu-hakama                -> Eastern Socialite's Skirt
        AddItem(m2f, f2m, 24162, 23012, true, false); // Leal Samurai's Geta                         -> Eastern Socialite's Boots
        AddItem(m2f, f2m, 02966, 13321, false);       // Reindeer Suit                              <-  Antecedent's Attire
        AddItem(m2f, f2m, 15479, 36843, false);       // Swine Body                                 <-  Lyse's Leadership Attire
        AddItem(m2f, f2m, 21941, 24999, false);       // Ala Mhigan Gown                            <-  Gown of Light
        AddItem(m2f, f2m, 30757, 25000, false);       // Southern Seas Skirt                        <-  Skirt of Light
        AddItem(m2f, f2m, 36821, 27933, false);       // Archfiend Helm                             <-  Scion Hearer's Hood
        AddItem(m2f, f2m, 36822, 27934, false);       // Archfiend Armor                            <-  Scion Hearer's Coat
        AddItem(m2f, f2m, 36825, 27935, false);       // Archfiend Sabatons                         <-  Scion Hearer's Shoes
        AddItem(m2f, f2m, 38253, 38257);              // Valentione Emissary's Hat                  <-> Valentione Emissary's Dress Hat
        AddItem(m2f, f2m, 38254, 38258);              // Valentione Emissary's Jacket               <-> Valentione Emissary's Ruffled Dress
        AddItem(m2f, f2m, 38255, 38259);              // Valentione Emissary's Bottoms              <-> Valentione Emissary's Culottes
        AddItem(m2f, f2m, 38256, 38260);              // Valentione Emissary's Boots                <-> Valentione Emissary's Boots
        AddItem(m2f, f2m, 32393, 39302, false);       // Edenmete Gown of Casting                   <-  Gaia's Attire
    }

    // The racial starter sets are available for all 4 slots each,
    // but have no associated accessories or hats.
    private static readonly uint[] RaceGenderGroup =
    {
        0x020054,
        0x020055,
        0x020056,
        0x020057,
        0x02005C,
        0x02005D,
        0x020058,
        0x020059,
        0x02005A,
        0x02005B,
        0x020101,
        0x020102,
        0x010255,
        uint.MaxValue, // TODO: Female Hrothgar
        0x0102E8,
        0x010245,
    };
    // @Formatter:on
}
