using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using OtterGui.Services;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Communication;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.Interop.Structs;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace Penumbra.Interop.Services;

public unsafe partial class RedrawService : IService
{
    public const int GPosePlayerIdx = 201;
    public const int GPoseSlots     = 42;
    public const int GPoseEndIdx    = GPosePlayerIdx + GPoseSlots;

    private readonly string?[] _gPoseNames = new string?[GPoseSlots];
    private          int       _gPoseNameCounter;

    internal IReadOnlyList<string?> GPoseNames
        => _gPoseNames;

    internal bool InGPose
        => _clientState.IsGPosing;

    // VFuncs that disable and enable draw, used only for GPose actors.
    private static void DisableDraw(IGameObject actor)
        => ((delegate* unmanaged<nint, void >**)actor.Address)[0][VolatileOffsets.RedrawService.DisableDrawVFunc](actor.Address);

    private static void EnableDraw(IGameObject actor)
        => ((delegate* unmanaged<nint, void >**)actor.Address)[0][VolatileOffsets.RedrawService.EnableDrawVFunc](actor.Address);

    // Check whether we currently are in GPose.
    // Also clear the name list.
    private void SetGPose()
        => _gPoseNameCounter = 0;

    private static bool IsGPoseActor(int idx)
        => idx is >= GPosePlayerIdx and < GPoseEndIdx;

    // Return whether an object has to be replaced by a GPose object.
    // If the object does not exist, is already a GPose actor
    // or no actor of the same name is found in the GPose actor list,
    // obj will be the object itself (or null) and false will be returned.
    // If we are in GPose and a game object with the same name as the original actor is found,
    // this will be in obj and true will be returned.
    private bool FindCorrectActor(int idx, out IGameObject? obj)
    {
        obj = _objects.GetDalamudObject(idx);
        if (!InGPose || obj == null || IsGPoseActor(idx))
            return false;

        var name = obj.Name.ToString();
        for (var i = 0; i < _gPoseNameCounter; ++i)
        {
            var gPoseName = _gPoseNames[i];
            if (gPoseName == null)
                break;

            if (name == gPoseName)
            {
                obj = _objects.GetDalamudObject(GPosePlayerIdx + i);
                return true;
            }
        }

        for (; _gPoseNameCounter < GPoseSlots; ++_gPoseNameCounter)
        {
            var gPoseName = _objects.GetDalamudObject(GPosePlayerIdx + _gPoseNameCounter)?.Name.ToString();
            _gPoseNames[_gPoseNameCounter] = gPoseName;
            if (gPoseName == null)
                break;

            if (name == gPoseName)
            {
                obj = _objects.GetDalamudObject(GPosePlayerIdx + _gPoseNameCounter);
                return true;
            }
        }

        return false;
    }

    // Do not ever redraw any of the five UI Window actors.
    private static bool BadRedrawIndices(IGameObject? actor, out int tableIndex)
    {
        if (actor == null)
        {
            tableIndex = -1;
            return true;
        }

        tableIndex = ObjectTableIndex(actor);
        return tableIndex is >= (int)ScreenActor.CharacterScreen and <= (int)ScreenActor.Card8;
    }
}

public sealed unsafe partial class RedrawService : IDisposable
{
    private const int FurnitureIdx = 1337;

    private readonly IFramework          _framework;
    private readonly ObjectManager       _objects;
    private readonly ITargetManager      _targets;
    private readonly ICondition          _conditions;
    private readonly IClientState        _clientState;
    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;

    private readonly List<int> _queue           = new(100);
    private readonly List<int> _afterGPoseQueue = new(GPoseSlots);
    private          int       _target          = -1;

    internal IReadOnlyList<int> Queue
        => _queue;

    internal IReadOnlyList<int> AfterGPoseQueue
        => _afterGPoseQueue;

    internal int Target
        => _target;

    public event GameObjectRedrawnDelegate? GameObjectRedrawn;

    public RedrawService(IFramework framework, ObjectManager objects, ITargetManager targets, ICondition conditions, IClientState clientState,
        Configuration config, CommunicatorService communicator)
    {
        _framework        =  framework;
        _objects          =  objects;
        _targets          =  targets;
        _conditions       =  conditions;
        _clientState      =  clientState;
        _config           =  config;
        _communicator     =  communicator;
        _framework.Update += OnUpdateEvent;
        _communicator.ModFileChanged.Subscribe(OnModFileChanged, ModFileChanged.Priority.RedrawService);
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdateEvent;
        _communicator.ModFileChanged.Unsubscribe(OnModFileChanged);
    }

    public static DrawState* ActorDrawState(IGameObject actor)
        => (DrawState*)(&((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)actor.Address)->RenderFlags);

    private static int ObjectTableIndex(IGameObject actor)
        => ((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)actor.Address)->ObjectIndex;

    private void WriteInvisible(IGameObject? actor)
    {
        if (BadRedrawIndices(actor, out var tableIndex))
            return;

        *ActorDrawState(actor!) |= DrawState.Invisibility;

        var gPose = IsGPoseActor(tableIndex);
        if (gPose)
            DisableDraw(actor!);

        if (actor is IPlayerCharacter
         && _objects.GetDalamudObject(tableIndex + 1) is { ObjectKind: ObjectKind.MountType or ObjectKind.Ornament } mountOrOrnament)
        {
            *ActorDrawState(mountOrOrnament) |= DrawState.Invisibility;
            if (gPose)
                DisableDraw(mountOrOrnament);
        }
    }

    private void WriteVisible(IGameObject? actor)
    {
        if (BadRedrawIndices(actor, out var tableIndex))
            return;

        *ActorDrawState(actor!) &= ~DrawState.Invisibility;

        var gPose = IsGPoseActor(tableIndex);
        if (gPose)
            EnableDraw(actor!);

        if (actor is IPlayerCharacter
         && _objects.GetDalamudObject(tableIndex + 1) is { ObjectKind: ObjectKind.MountType or ObjectKind.Ornament } mountOrOrnament)
        {
            *ActorDrawState(mountOrOrnament) &= ~DrawState.Invisibility;
            if (gPose)
                EnableDraw(mountOrOrnament);
        }

        GameObjectRedrawn?.Invoke(actor!.Address, tableIndex);
    }

    private void ReloadActor(IGameObject? actor)
    {
        if (BadRedrawIndices(actor, out var tableIndex))
            return;

        if (actor!.Address == _targets.Target?.Address)
            _target = tableIndex;

        _queue.Add(~tableIndex);
    }

    private void ReloadActorAfterGPose(IGameObject? actor)
    {
        if (_objects[GPosePlayerIdx].Valid)
        {
            ReloadActor(actor);
            return;
        }

        if (actor != null)
        {
            WriteInvisible(actor);
            _afterGPoseQueue.Add(~ObjectTableIndex(actor));
        }
    }

    private void HandleTarget()
    {
        if (_target < 0)
            return;

        var actor = _objects.GetDalamudObject(_target);
        if (actor == null || _targets.Target != null)
            return;

        _targets.Target = actor;
        _target         = -1;
    }

    private void HandleRedraw()
    {
        if (_queue.Count == 0)
            return;

        var numKept = 0;
        for (var i = 0; i < _queue.Count; ++i)
        {
            var idx = _queue[i];
            if (idx == ~FurnitureIdx)
            {
                DisableFurniture();
                continue;
            }

            if (FindCorrectActor(idx < 0 ? ~idx : idx, out var obj))
                _afterGPoseQueue.Add(idx < 0 ? idx : ~idx);

            if (obj == null)
                continue;

            if (idx < 0)
            {
                if (DelayRedraw(obj))
                {
                    _queue[numKept++] = ~ObjectTableIndex(obj);
                }
                else
                {
                    WriteInvisible(obj);
                    _queue[numKept++] = ObjectTableIndex(obj);
                }
            }
            else
            {
                WriteVisible(obj);
            }
        }

        _queue.RemoveRange(numKept, _queue.Count - numKept);
    }

    private static uint GetCurrentAnimationId(IGameObject obj)
    {
        var gameObj = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)obj.Address;
        if (gameObj == null || !gameObj->IsCharacter())
            return 0;

        var chara = (Character*)gameObj;
        var ptr   = (byte*)&chara->Timeline + 0xF0;
        return *(uint*)ptr;
    }

    private static bool DelayRedraw(IGameObject obj)
        => ((Character*)obj.Address)->Mode switch
        {
            (CharacterModes)6 => // fishing
                GetCurrentAnimationId(obj) switch
                {
                    278  => true, // line out.
                    283  => true, // reeling in
                    284  => true, // reeling in
                    287  => true, // reeling in 2
                    3149 => true, // line out sitting,
                    3155 => true, // reeling in sitting,
                    3159 => true, // reeling in sitting 2,
                    _    => false,
                },
            _ => false,
        };

    private void HandleAfterGPose()
    {
        if (_afterGPoseQueue.Count == 0 || InGPose)
            return;

        var numKept = 0;
        for (var i = 0; i < _afterGPoseQueue.Count; ++i)
        {
            var idx = _afterGPoseQueue[i];
            if (idx < 0)
            {
                var newIdx = ~idx;
                WriteInvisible(_objects.GetDalamudObject(newIdx));
                _afterGPoseQueue[numKept++] = newIdx;
            }
            else
            {
                WriteVisible(_objects.GetDalamudObject(idx));
            }
        }

        _afterGPoseQueue.RemoveRange(numKept, _afterGPoseQueue.Count - numKept);
    }

    private void OnUpdateEvent(object framework)
    {
        if (_conditions[ConditionFlag.BetweenAreas51]
         || _conditions[ConditionFlag.BetweenAreas]
         || _conditions[ConditionFlag.OccupiedInCutSceneEvent])
            return;

        SetGPose();
        HandleRedraw();
        HandleAfterGPose();
        HandleTarget();
    }

    public void RedrawObject(IGameObject? actor, RedrawType settings)
    {
        switch (settings)
        {
            case RedrawType.Redraw:
                ReloadActor(actor);
                break;
            case RedrawType.AfterGPose:
                ReloadActorAfterGPose(actor);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(settings), settings, null);
        }
    }

    private IGameObject? GetLocalPlayer()
    {
        var gPosePlayer = _objects.GetDalamudObject(GPosePlayerIdx);
        return gPosePlayer ?? _objects.GetDalamudObject(0);
    }

    public bool GetName(string lowerName, out IGameObject? actor)
    {
        (actor, var ret) = lowerName switch
        {
            ""          => (null, true),
            "<me>"      => (GetLocalPlayer(), true),
            "self"      => (GetLocalPlayer(), true),
            "<t>"       => (_targets.Target, true),
            "target"    => (_targets.Target, true),
            "<f>"       => (_targets.FocusTarget, true),
            "focus"     => (_targets.FocusTarget, true),
            "<mo>"      => (_targets.MouseOverTarget, true),
            "mouseover" => (_targets.MouseOverTarget, true),
            _           => (null, false),
        };
        if (!ret && lowerName.Length > 1 && lowerName[0] == '#' && ushort.TryParse(lowerName[1..], out var objectIndex))
        {
            ret   = true;
            actor = _objects.GetDalamudObject((int)objectIndex);
        }

        return ret;
    }

    public void RedrawObject(int tableIndex, RedrawType settings)
    {
        if (tableIndex >= 0 && tableIndex < _objects.TotalCount)
            RedrawObject(_objects.GetDalamudObject(tableIndex), settings);
    }

    public void RedrawObject(string name, RedrawType settings)
    {
        var lowerName = name.ToLowerInvariant().Trim();
        if (lowerName == "furniture")
            _queue.Add(~FurnitureIdx);
        else if (GetName(lowerName, out var target))
            RedrawObject(target, settings);
        else
            foreach (var actor in _objects.Objects.Where(a => a.Name.ToString().ToLowerInvariant() == lowerName))
                RedrawObject(actor, settings);
    }

    public void RedrawAll(RedrawType settings)
    {
        foreach (var actor in _objects.Objects)
            RedrawObject(actor, settings);
    }

    private void DisableFurniture()
    {
        var housingManager = HousingManager.Instance();
        if (housingManager == null)
            return;

        var currentTerritory = (IndoorTerritory*)housingManager->CurrentTerritory;
        if (currentTerritory == null || currentTerritory->GetTerritoryType() is not HousingTerritoryType.Indoor)
            return;


        foreach (ref var f in currentTerritory->Furniture)
        {
            var gameObject = f.Index >= 0 ? currentTerritory->HousingObjectManager.Objects[f.Index].Value : null;
            if (gameObject == null)
                continue;

            gameObject->DisableDraw();
        }
    }

    private void OnModFileChanged(Mod _1, FileRegistry _2)
    {
        if (!_config.Ephemeral.ForceRedrawOnFileChange)
            return;

        RedrawObject(0, RedrawType.Redraw);
    }
}
