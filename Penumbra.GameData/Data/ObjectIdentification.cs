using System;
using Dalamud;
using Dalamud.Data;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.GameData.Actors;
using Action = Lumina.Excel.GeneratedSheets.Action;
using ObjectType = Penumbra.GameData.Enums.ObjectType;

namespace Penumbra.GameData.Data;

internal sealed class ObjectIdentification : DataSharer, IObjectIdentifier
{
    public const int IdentificationVersion = 1;

    public           IGamePathParser                                              GamePathParser { get; } = new GamePathParser();
    public readonly  IReadOnlyList<IReadOnlyList<uint>>                           BnpcNames;
    public readonly  IReadOnlyList<IReadOnlyList<(string Name, ObjectKind Kind)>> ModelCharaToObjects;
    public readonly  IReadOnlyDictionary<string, IReadOnlyList<Action>>           Actions;
    private readonly ActorManager.ActorManagerData                                _actorData;


    private readonly EquipmentIdentificationList _equipment;
    private readonly WeaponIdentificationList    _weapons;
    private readonly ModelIdentificationList     _modelIdentifierToModelChara;

    public ObjectIdentification(DalamudPluginInterface pluginInterface, DataManager dataManager, ClientLanguage language)
        : base(pluginInterface, language, IdentificationVersion)
    {
        _actorData = new ActorManager.ActorManagerData(pluginInterface, dataManager, language);
        _equipment = new EquipmentIdentificationList(pluginInterface, language, dataManager);
        _weapons   = new WeaponIdentificationList(pluginInterface, language, dataManager);
        Actions    = TryCatchData("Actions", () => CreateActionList(dataManager));
        _equipment = new EquipmentIdentificationList(pluginInterface, language, dataManager);

        _modelIdentifierToModelChara = new ModelIdentificationList(pluginInterface, language, dataManager);
        BnpcNames                    = TryCatchData("BNpcNames",    NpcNames.CreateNames);
        ModelCharaToObjects          = TryCatchData("ModelObjects", () => CreateModelObjects(_actorData, dataManager, language));
    }

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

    public IEnumerable<Item> Identify(SetId setId, WeaponType weaponType, ushort variant, EquipSlot slot)
        => slot switch
        {
            EquipSlot.MainHand => _weapons.Between(setId, weaponType, (byte)variant),
            EquipSlot.OffHand  => _weapons.Between(setId, weaponType, (byte)variant),
            _                  => _equipment.Between(setId, slot, (byte)variant),
        };

    protected override void DisposeInternal()
    {
        _actorData.Dispose();
        _weapons.Dispose(PluginInterface, Language);
        _equipment.Dispose(PluginInterface, Language);
        DisposeTag("Actions");
        DisposeTag("Models");

        _modelIdentifierToModelChara.Dispose(PluginInterface, Language);
        DisposeTag("BNpcNames");
        DisposeTag("ModelObjects");
    }

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

    private IReadOnlyList<(ulong Key, IReadOnlyList<Item> Values)> CreateWeaponList(DataManager gameData)
    {
        var items   = gameData.GetExcelSheet<Item>(Language)!;
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

    private IReadOnlyList<(ulong Key, IReadOnlyList<Item> Values)> CreateEquipmentList(DataManager gameData)
    {
        var items   = gameData.GetExcelSheet<Item>(Language)!;
        var storage = new SortedList<ulong, HashSet<Item>>();
        foreach (var item in items)
        {
            switch ((EquipSlot)item.EquipSlotCategory.Row)
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

    private IReadOnlyDictionary<string, IReadOnlyList<Action>> CreateActionList(DataManager gameData)
    {
        var sheet   = gameData.GetExcelSheet<Action>(Language)!;
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

    private static readonly Comparer _arrayComparer = new();


    private static (int, int) FindIndexRange(List<(ulong, IReadOnlyList<Item>)> list, ulong key, ulong mask)
    {
        var maskedKey = key & mask;
        var idx       = list.BinarySearch(0, list.Count, (key, null!), _arrayComparer);
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
        var items = _equipment.Between(info.PrimaryId, info.EquipSlot, info.Variant);
        foreach (var item in items)
            set[item.Name.ToString()] = item;
    }

    private void FindWeapon(IDictionary<string, object?> set, GameObjectInfo info)
    {
        var items = _weapons.Between(info.PrimaryId, info.SecondaryId, info.Variant);
        foreach (var item in items)
            set[item.Name.ToString()] = item;
    }

    private void FindModel(IDictionary<string, object?> set, GameObjectInfo info)
    {
        var type = info.ObjectType.ToModelType();
        if (type is 0 or CharacterBase.ModelType.Weapon)
            return;

        var models = _modelIdentifierToModelChara.Between(type, info.PrimaryId, (byte)info.SecondaryId, info.Variant);
        foreach (var model in models.Where(m => m.RowId < ModelCharaToObjects.Count))
        {
            var objectList = ModelCharaToObjects[(int)model.RowId];
            foreach (var (name, kind) in objectList)
                set[$"{name} ({kind.ToName()})"] = model;
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
                FindModel(set, info);
                break;
            case ObjectType.Monster:
                FindModel(set, info);
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
        if (key.Length == 0 || !Actions.TryGetValue(key, out var actions))
            return;

        foreach (var action in actions)
            set[$"Action: {action.Name}"] = action;
    }

    private IReadOnlyList<IReadOnlyList<(string Name, ObjectKind Kind)>> CreateModelObjects(ActorManager.ActorManagerData actors,
        DataManager gameData,
        ClientLanguage language)
    {
        var modelSheet     = gameData.GetExcelSheet<ModelChara>(language)!;
        var bnpcSheet      = gameData.GetExcelSheet<BNpcBase>(language)!;
        var enpcSheet      = gameData.GetExcelSheet<ENpcBase>(language)!;
        var ornamentSheet  = gameData.GetExcelSheet<Ornament>(language)!;
        var mountSheet     = gameData.GetExcelSheet<Mount>(language)!;
        var companionSheet = gameData.GetExcelSheet<Companion>(language)!;
        var ret            = new List<HashSet<(string Name, ObjectKind Kind)>>((int)modelSheet.RowCount);

        for (var i = -1; i < modelSheet.Last().RowId; ++i)
            ret.Add(new HashSet<(string Name, ObjectKind Kind)>());

        void Add(int modelChara, ObjectKind kind, uint dataId)
        {
            if (modelChara == 0 || modelChara >= ret.Count)
                return;

            if (actors.TryGetName(kind, dataId, out var name))
                ret[modelChara].Add((name, kind));
        }

        foreach (var ornament in ornamentSheet)
            Add(ornament.Model, (ObjectKind)15, ornament.RowId);

        foreach (var mount in mountSheet)
            Add((int)mount.ModelChara.Row, ObjectKind.MountType, mount.RowId);

        foreach (var companion in companionSheet)
            Add((int)companion.Model.Row, ObjectKind.Companion, companion.RowId);

        foreach (var enpc in enpcSheet)
            Add((int)enpc.ModelChara.Row, ObjectKind.EventNpc, enpc.RowId);

        foreach (var bnpc in bnpcSheet.Where(b => b.RowId < BnpcNames.Count))
        {
            foreach (var name in BnpcNames[(int)bnpc.RowId])
                Add((int)bnpc.ModelChara.Row, ObjectKind.BattleNpc, name);
        }

        return ret.Select(s => s.Count > 0
            ? s.ToArray()
            : Array.Empty<(string Name, ObjectKind Kind)>()).ToArray();
    }

    public static unsafe ulong KeyFromCharacterBase(CharacterBase* drawObject)
    {
        var type = (*(delegate* unmanaged<CharacterBase*, uint>**)drawObject)[50](drawObject);
        var unk  = (ulong)*((byte*)drawObject + 0x8E8) << 8;
        return type switch
        {
            1 => type | unk,
            2 => type | unk | ((ulong)*(ushort*)((byte*)drawObject + 0x908) << 16),
            3 => type
              | unk
              | ((ulong)*(ushort*)((byte*)drawObject + 0x8F0) << 16)
              | ((ulong)**(ushort**)((byte*)drawObject + 0x910) << 32)
              | ((ulong)**(ushort**)((byte*)drawObject + 0x910) << 40),
            _ => 0u,
        };
    }
}
