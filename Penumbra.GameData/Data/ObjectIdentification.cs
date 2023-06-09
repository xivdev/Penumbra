using System;
using System.Collections.Concurrent;
using Dalamud;
using Dalamud.Data;
using Lumina.Excel.GeneratedSheets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
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

        _modelIdentifierToModelChara = new ModelIdentificationList(pluginInterface, language, dataManager);
        BnpcNames                    = TryCatchData("BNpcNames",    NpcNames.CreateNames);
        ModelCharaToObjects          = TryCatchData("ModelObjects", () => CreateModelObjects(_actorData, dataManager, language));
    }

    public void Identify(IDictionary<string, object?> set, string path)
    {
        if (path.EndsWith(".pap", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tmb", StringComparison.OrdinalIgnoreCase))
        {
            if (IdentifyVfx(set, path))
                return;
        }

        var info = GamePathParser.GetFileInfo(path);
        IdentifyParsed(set, info);
    }

    public Dictionary<string, object?> Identify(string path)
    {
        Dictionary<string, object?> ret = new();
        Identify(ret, path);
        return ret;
    }

    public IEnumerable<EquipItem> Identify(SetId setId, WeaponType weaponType, ushort variant, EquipSlot slot)
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

    private IReadOnlyDictionary<string, IReadOnlyList<Action>> CreateActionList(DataManager gameData)
    {
        var sheet   = gameData.GetExcelSheet<Action>(Language)!;
        var storage = new ConcurrentDictionary<string, ConcurrentBag<Action>>();

        void AddAction(string? key, Action action)
        {
            if (key.IsNullOrEmpty())
                return;

            key = key.ToLowerInvariant();
            if (storage.TryGetValue(key, out var actions))
                actions.Add(action);
            else
                storage[key] = new ConcurrentBag<Action> { action };
        }

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
        };

        Parallel.ForEach(sheet.Where(a => !a.Name.RawData.IsEmpty), options, action =>
        {
            var startKey = action.AnimationStart?.Value?.Name?.Value?.Key.ToDalamudString().ToString();
            var endKey   = action.AnimationEnd?.Value?.Key.ToDalamudString().ToString();
            var hitKey   = action.ActionTimelineHit?.Value?.Key.ToDalamudString().ToString();
            AddAction(startKey, action);
            AddAction(endKey,   action);
            AddAction(hitKey,   action);
        });

        return storage.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<Action>)kvp.Value.ToArray());
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
        switch (info.FileType)
        {
            case FileType.Sound:
                AddCounterString(set, FileType.Sound.ToString());
                return;
            case FileType.Animation:
            case FileType.Pap:
                AddCounterString(set, FileType.Animation.ToString());
                return;
            case FileType.Shader:
                AddCounterString(set, FileType.Shader.ToString());
                return;
        }

        switch (info.ObjectType)
        {
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
        }
    }

    private bool IdentifyVfx(IDictionary<string, object?> set, string path)
    {
        var key = GamePathParser.VfxToKey(path);
        if (key.Length == 0 || !Actions.TryGetValue(key, out var actions) || actions.Count == 0)
            return false;

        foreach (var action in actions)
            set[$"Action: {action.Name}"] = action;
        return true;
    }

    private IReadOnlyList<IReadOnlyList<(string Name, ObjectKind Kind)>> CreateModelObjects(ActorManager.ActorManagerData actors,
        DataManager gameData, ClientLanguage language)
    {
        var modelSheet = gameData.GetExcelSheet<ModelChara>(language)!;
        var ret        = new List<ConcurrentBag<(string Name, ObjectKind Kind)>>((int)modelSheet.RowCount);

        for (var i = -1; i < modelSheet.Last().RowId; ++i)
            ret.Add(new ConcurrentBag<(string Name, ObjectKind Kind)>());

        void AddChara(int modelChara, ObjectKind kind, uint dataId)
        {
            if (modelChara == 0 || modelChara >= ret.Count)
                return;

            if (actors.TryGetName(kind, dataId, out var name))
                ret[modelChara].Add((name, kind));
        }

        var oTask = Task.Run(() =>
        {
            foreach (var ornament in gameData.GetExcelSheet<Ornament>(language)!)
                AddChara(ornament.Model, (ObjectKind)15, ornament.RowId);
        });

        var mTask = Task.Run(() =>
        {
            foreach (var mount in gameData.GetExcelSheet<Mount>(language)!)
                AddChara((int)mount.ModelChara.Row, ObjectKind.MountType, mount.RowId);
        });

        var cTask = Task.Run(() =>
        {
            foreach (var companion in gameData.GetExcelSheet<Companion>(language)!)
                AddChara((int)companion.Model.Row, ObjectKind.Companion, companion.RowId);
        });

        var eTask = Task.Run(() =>
        {
            foreach (var eNpc in gameData.GetExcelSheet<ENpcBase>(language)!)
                AddChara((int)eNpc.ModelChara.Row, ObjectKind.EventNpc, eNpc.RowId);
        });

        var options = new ParallelOptions()
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount / 2,
        };

        Parallel.ForEach(gameData.GetExcelSheet<BNpcBase>(language)!.Where(b => b.RowId < BnpcNames.Count), options, bNpc =>
        {
            foreach (var name in BnpcNames[(int)bNpc.RowId])
                AddChara((int)bNpc.ModelChara.Row, ObjectKind.BattleNpc, name);
        });

        Task.WaitAll(oTask, mTask, cTask, eTask);

        return ret.Select(s => s.Count > 0
            ? s.ToArray()
            : Array.Empty<(string Name, ObjectKind Kind)>()).ToArray();
    }

    public static unsafe ulong KeyFromCharacterBase(CharacterBase* drawObject)
    {
        var type = (*(delegate* unmanaged<CharacterBase*, uint>**)drawObject)[Offsets.DrawObjectGetModelTypeVfunc](drawObject);
        var unk  = (ulong)*((byte*)drawObject + Offsets.DrawObjectModelUnk1) << 8;
        return type switch
        {
            1 => type | unk,
            2 => type | unk | ((ulong)*(ushort*)((byte*)drawObject + Offsets.DrawObjectModelUnk3) << 16),
            3 => type
              | unk
              | ((ulong)*(ushort*)((byte*)drawObject + Offsets.DrawObjectModelUnk2) << 16)
              | ((ulong)**(ushort**)((byte*)drawObject + Offsets.DrawObjectModelUnk4) << 32)
              | ((ulong)**(ushort**)((byte*)drawObject + Offsets.DrawObjectModelUnk3) << 40),
            _ => 0u,
        };
    }
}
