using System;
using Dalamud;
using Dalamud.Data;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Utility;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace Penumbra.GameData;

internal class ObjectIdentification : IObjectIdentifier
{
    public IGamePathParser GamePathParser { get; } = new GamePathParser();

    public void Identify(IDictionary<string, object?> set, string path)
    {
        if (path.EndsWith(".pap", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tmb", StringComparison.OrdinalIgnoreCase))
        {
            IdentifyVfx(set, path);
        }
        else
        {
            var info = GamePathParser.GetFileInfo(path);
            IdentifyParsed(set, info);
        }
    }

    public Dictionary<string, object?> Identify(string path)
    {
        Dictionary<string, object?> ret = new();
        Identify(ret, path);
        return ret;
    }

    public IReadOnlyList<Item>? Identify(SetId setId, WeaponType weaponType, ushort variant, EquipSlot slot)
    {
        switch (slot)
        {
            case EquipSlot.MainHand:
            case EquipSlot.OffHand:
            {
                var (begin, _) = FindIndexRange((List<(ulong, IReadOnlyList<Item>)>)_weapons,
                    ((ulong)setId << 32) | ((ulong)weaponType << 16) | variant,
                    0xFFFFFFFFFFFF);
                return begin >= 0 ? _weapons[begin].Item2 : null;
            }
            default:
            {
                var (begin, _) = FindIndexRange((List<(ulong, IReadOnlyList<Item>)>)_equipment,
                    ((ulong)setId << 32) | ((ulong)slot.ToSlot() << 16) | variant,
                    0xFFFFFFFFFFFF);
                return begin >= 0 ? _equipment[begin].Item2 : null;
            }
        }
    }

    private const int Version = 1;

    private readonly DataManager            _dataManager;
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly ClientLanguage         _language;

    private readonly IReadOnlyList<(ulong Key, IReadOnlyList<Item> Values)> _weapons;
    private readonly IReadOnlyList<(ulong Key, IReadOnlyList<Item> Values)> _equipment;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<Action>>     _actions;

    private readonly string _weaponsTag;
    private readonly string _equipmentTag;
    private readonly string _actionsTag;
    private          bool   _disposed = false;

    public ObjectIdentification(DalamudPluginInterface pluginInterface, DataManager dataManager, ClientLanguage language)
    {
        _pluginInterface = pluginInterface;
        _dataManager     = dataManager;
        _language        = language;

        _weaponsTag   = $"Penumbra.Identification.Weapons.{_language}.V{Version}";
        _equipmentTag = $"Penumbra.Identification.Equipment.{_language}.V{Version}";
        _actionsTag   = $"Penumbra.Identification.Actions.{_language}.V{Version}";

        try
        {
            _weapons = pluginInterface.GetOrCreateData(_weaponsTag, CreateWeaponList);
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error creating shared identification data for weapons:\n{ex}");
            _weapons = CreateWeaponList();
        }

        try
        {
            _equipment = pluginInterface.GetOrCreateData(_equipmentTag, CreateEquipmentList);
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error creating shared identification data for equipment:\n{ex}");
            _equipment = CreateEquipmentList();
        }

        try
        {
            _actions = pluginInterface.GetOrCreateData(_actionsTag, CreateActionList);
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error creating shared identification data for actions:\n{ex}");
            _actions = CreateActionList();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        GC.SuppressFinalize(this);
        _pluginInterface.RelinquishData(_weaponsTag);
        _pluginInterface.RelinquishData(_equipmentTag);
        _pluginInterface.RelinquishData(_actionsTag);
        _disposed = true;
    }

    ~ObjectIdentification()
        => Dispose();

    private static bool Add(IDictionary<ulong, HashSet<Item>> dict, ulong key, Item item)
    {
        if (dict.TryGetValue(key, out var list))
            return list.Add(item);

        dict[key] = new HashSet<Item> { item };
        return true;
    }

    private static ulong EquipmentKey(Item i)
    {
        var model   = (ulong)((Lumina.Data.Parsing.Quad)i.ModelMain).A;
        var variant = (ulong)((Lumina.Data.Parsing.Quad)i.ModelMain).B;
        var slot    = (ulong)((EquipSlot)i.EquipSlotCategory.Row).ToSlot();
        return (model << 32) | (slot << 16) | variant;
    }

    private static ulong WeaponKey(Item i, bool offhand)
    {
        var quad    = offhand ? (Lumina.Data.Parsing.Quad)i.ModelSub : (Lumina.Data.Parsing.Quad)i.ModelMain;
        var model   = (ulong)quad.A;
        var type    = (ulong)quad.B;
        var variant = (ulong)quad.C;

        return (model << 32) | (type << 16) | variant;
    }

    private IReadOnlyList<(ulong Key, IReadOnlyList<Item> Values)> CreateWeaponList()
    {
        var items   = _dataManager.GetExcelSheet<Item>(_language)!;
        var storage = new SortedList<ulong, HashSet<Item>>();
        foreach (var item in items.Where(i
                     => (EquipSlot)i.EquipSlotCategory.Row is EquipSlot.MainHand or EquipSlot.OffHand or EquipSlot.BothHand))
        {
            if (item.ModelMain != 0)
                Add(storage, WeaponKey(item, false), item);

            if (item.ModelSub != 0)
                Add(storage, WeaponKey(item, true), item);
        }

        return storage.Select(kvp => (kvp.Key, (IReadOnlyList<Item>)kvp.Value.ToArray())).ToList();
    }

    private IReadOnlyList<(ulong Key, IReadOnlyList<Item> Values)> CreateEquipmentList()
    {
        var items   = _dataManager.GetExcelSheet<Item>(_language)!;
        var storage = new SortedList<ulong, HashSet<Item>>();
        foreach (var item in items)
        {
            switch (((EquipSlot)item.EquipSlotCategory.Row).ToSlot())
            {
                // Accessories
                case EquipSlot.RFinger:
                case EquipSlot.Wrists:
                case EquipSlot.Ears:
                case EquipSlot.Neck:
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
                case EquipSlot.ChestHands:
                    Add(storage, EquipmentKey(item), item);
                    break;
            }
        }

        return storage.Select(kvp => (kvp.Key, (IReadOnlyList<Item>)kvp.Value.ToArray())).ToList();
    }

    private IReadOnlyDictionary<string, IReadOnlyList<Action>> CreateActionList()
    {
        var sheet   = _dataManager.GetExcelSheet<Action>(_language)!;
        var storage = new Dictionary<string, HashSet<Action>>((int)sheet.RowCount);

        void AddAction(string? key, Action action)
        {
            if (key.IsNullOrEmpty())
                return;

            key = key.ToLowerInvariant();
            if (storage.TryGetValue(key, out var actions))
                actions.Add(action);
            else
                storage[key] = new HashSet<Action> { action };
        }

        foreach (var action in sheet.Where(a => !a.Name.RawData.IsEmpty))
        {
            var startKey = action.AnimationStart?.Value?.Name?.Value?.Key.ToDalamudString().ToString();
            var endKey   = action.AnimationEnd?.Value?.Key.ToDalamudString().ToString();
            var hitKey   = action.ActionTimelineHit?.Value?.Key.ToDalamudString().ToString();
            AddAction(startKey, action);
            AddAction(endKey,   action);
            AddAction(hitKey,   action);
        }

        return storage.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<Action>)kvp.Value.ToArray());
    }

    private class Comparer : IComparer<(ulong, IReadOnlyList<Item>)>
    {
        public int Compare((ulong, IReadOnlyList<Item>) x, (ulong, IReadOnlyList<Item>) y)
            => x.Item1.CompareTo(y.Item1);
    }

    private static (int, int) FindIndexRange(List<(ulong, IReadOnlyList<Item>)> list, ulong key, ulong mask)
    {
        var maskedKey = key & mask;
        var idx       = list.BinarySearch(0, list.Count, (key, null!), new Comparer());
        if (idx < 0)
        {
            if (~idx == list.Count || maskedKey != (list[~idx].Item1 & mask))
                return (-1, -1);

            idx = ~idx;
        }

        var endIdx = idx + 1;
        while (endIdx < list.Count && maskedKey == (list[endIdx].Item1 & mask))
            ++endIdx;

        return (idx, endIdx);
    }

    private void FindEquipment(IDictionary<string, object?> set, GameObjectInfo info)
    {
        var key  = (ulong)info.PrimaryId << 32;
        var mask = 0xFFFF00000000ul;
        if (info.EquipSlot != EquipSlot.Unknown)
        {
            key  |= (ulong)info.EquipSlot.ToSlot() << 16;
            mask |= 0xFFFF0000;
        }

        if (info.Variant != 0)
        {
            key  |= info.Variant;
            mask |= 0xFFFF;
        }

        var (start, end) = FindIndexRange((List<(ulong, IReadOnlyList<Item>)>)_equipment, key, mask);
        if (start == -1)
            return;

        for (; start < end; ++start)
        {
            foreach (var item in _equipment[start].Item2)
                set[item.Name.ToString()] = item;
        }
    }

    private void FindWeapon(IDictionary<string, object?> set, GameObjectInfo info)
    {
        var key  = (ulong)info.PrimaryId << 32;
        var mask = 0xFFFF00000000ul;
        if (info.SecondaryId != 0)
        {
            key  |= (ulong)info.SecondaryId << 16;
            mask |= 0xFFFF0000;
        }

        if (info.Variant != 0)
        {
            key  |= info.Variant;
            mask |= 0xFFFF;
        }

        var (start, end) = FindIndexRange((List<(ulong, IReadOnlyList<Item>)>)_weapons, key, mask);
        if (start == -1)
            return;

        for (; start < end; ++start)
        {
            foreach (var item in _weapons[start].Item2)
                set[item.Name.ToString()] = item;
        }
    }

    private static void AddCounterString(IDictionary<string, object?> set, string data)
    {
        if (set.TryGetValue(data, out var obj) && obj is int counter)
            set[data] = counter + 1;
        else
            set[data] = 1;
    }

    private void IdentifyParsed(IDictionary<string, object?> set, GameObjectInfo info)
    {
        switch (info.ObjectType)
        {
            case ObjectType.Unknown:
                switch (info.FileType)
                {
                    case FileType.Sound:
                        AddCounterString(set, FileType.Sound.ToString());
                        break;
                    case FileType.Animation:
                    case FileType.Pap:
                        AddCounterString(set, FileType.Animation.ToString());
                        break;
                    case FileType.Shader:
                        AddCounterString(set, FileType.Shader.ToString());
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
                AddCounterString(set, info.ObjectType.ToString());
                break;
            case ObjectType.DemiHuman:
                set[$"Demi Human: {info.PrimaryId}"] = null;
                break;
            case ObjectType.Monster:
                set[$"Monster: {info.PrimaryId}"] = null;
                break;
            case ObjectType.Icon:
                set[$"Icon: {info.IconId}"] = null;
                break;
            case ObjectType.Accessory:
            case ObjectType.Equipment:
                FindEquipment(set, info);
                break;
            case ObjectType.Weapon:
                FindWeapon(set, info);
                break;
            case ObjectType.Character:
                var (gender, race) = info.GenderRace.Split();
                var raceString   = race != ModelRace.Unknown ? race.ToName() + " " : "";
                var genderString = gender != Gender.Unknown ? gender.ToName() + " " : "Player ";
                switch (info.CustomizationType)
                {
                    case CustomizationType.Skin:
                        set[$"Customization: {raceString}{genderString}Skin Textures"] = null;
                        break;
                    case CustomizationType.DecalFace:
                        set[$"Customization: Face Decal {info.PrimaryId}"] = null;
                        break;
                    case CustomizationType.Iris when race == ModelRace.Unknown:
                        set[$"Customization: All Eyes (Catchlight)"] = null;
                        break;
                    case CustomizationType.DecalEquip:
                        set[$"Equipment Decal {info.PrimaryId}"] = null;
                        break;
                    default:
                    {
                        var customizationString = race == ModelRace.Unknown
                         || info.BodySlot == BodySlot.Unknown
                         || info.CustomizationType == CustomizationType.Unknown
                                ? "Customization: Unknown"
                                : $"Customization: {race} {gender} {info.BodySlot} ({info.CustomizationType}) {info.PrimaryId}";
                        set[customizationString] = null;
                        break;
                    }
                }

                break;

            default: throw new InvalidEnumArgumentException();
        }
    }

    private void IdentifyVfx(IDictionary<string, object?> set, string path)
    {
        var key = GamePathParser.VfxToKey(path);
        if (key.Length == 0 || !_actions.TryGetValue(key, out var actions))
            return;

        foreach (var action in actions)
            set[$"Action: {action.Name}"] = action;
    }
}
