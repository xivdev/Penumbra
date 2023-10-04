using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.Collections;
using Penumbra.Api.Enums;
using Penumbra.GameData;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.Util;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace Penumbra.Interop.PathResolving;

public unsafe class AnimationHookService : IDisposable
{
    private readonly PerformanceTracker _performance;
    private readonly IObjectTable       _objects;
    private readonly CollectionResolver _collectionResolver;
    private readonly DrawObjectState    _drawObjectState;
    private readonly CollectionResolver _resolver;
    private readonly ICondition         _conditions;

    private readonly ThreadLocal<ResolveData> _animationLoadData  = new(() => ResolveData.Invalid, true);
    private readonly ThreadLocal<ResolveData> _characterSoundData = new(() => ResolveData.Invalid, true);

    public AnimationHookService(PerformanceTracker performance, IObjectTable objects, CollectionResolver collectionResolver,
        DrawObjectState drawObjectState, CollectionResolver resolver, ICondition conditions, IGameInteropProvider interop)
    {
        _performance        = performance;
        _objects            = objects;
        _collectionResolver = collectionResolver;
        _drawObjectState    = drawObjectState;
        _resolver           = resolver;
        _conditions         = conditions;

        interop.InitializeFromAttributes(this);
        _loadCharacterSoundHook =
            interop.HookFromAddress<LoadCharacterSound>(
                (nint)FFXIVClientStructs.FFXIV.Client.Game.Character.Character.VfxContainer.MemberFunctionPointers.LoadCharacterSound,
                LoadCharacterSoundDetour);

        _loadCharacterSoundHook.Enable();
        _loadTimelineResourcesHook.Enable();
        _characterBaseLoadAnimationHook.Enable();
        _loadSomePapHook.Enable();
        _someActionLoadHook.Enable();
        _loadCharacterVfxHook.Enable();
        _loadAreaVfxHook.Enable();
        _scheduleClipUpdateHook.Enable();
        _unkMountAnimationHook.Enable();
        _unkParasolAnimationHook.Enable();
        _dismountHook.Enable();
        _apricotListenerSoundPlayHook.Enable();
    }

    public bool HandleFiles(ResourceType type, Utf8GamePath _, out ResolveData resolveData)
    {
        switch (type)
        {
            case ResourceType.Scd:
                if (_characterSoundData is { IsValueCreated: true, Value.Valid: true })
                {
                    resolveData = _characterSoundData.Value;
                    return true;
                }

                if (_animationLoadData is { IsValueCreated: true, Value.Valid: true })
                {
                    resolveData = _animationLoadData.Value;
                    return true;
                }

                break;
            case ResourceType.Tmb:
            case ResourceType.Pap:
            case ResourceType.Avfx:
            case ResourceType.Atex:
                if (_animationLoadData is { IsValueCreated: true, Value.Valid: true })
                {
                    resolveData = _animationLoadData.Value;
                    return true;
                }

                break;
        }

        var lastObj = _drawObjectState.LastGameObject;
        if (lastObj != nint.Zero)
        {
            resolveData = _resolver.IdentifyCollection((GameObject*)lastObj, true);
            return true;
        }

        resolveData = ResolveData.Invalid;
        return false;
    }

    public void Dispose()
    {
        _loadCharacterSoundHook.Dispose();
        _loadTimelineResourcesHook.Dispose();
        _characterBaseLoadAnimationHook.Dispose();
        _loadSomePapHook.Dispose();
        _someActionLoadHook.Dispose();
        _loadCharacterVfxHook.Dispose();
        _loadAreaVfxHook.Dispose();
        _scheduleClipUpdateHook.Dispose();
        _unkMountAnimationHook.Dispose();
        _unkParasolAnimationHook.Dispose();
        _dismountHook.Dispose();
        _apricotListenerSoundPlayHook.Dispose();
    }

    /// <summary> Characters load some of their voice lines or whatever with this function. </summary>
    private delegate nint LoadCharacterSound(nint character, int unk1, int unk2, nint unk3, ulong unk4, int unk5, int unk6, ulong unk7);

    private readonly Hook<LoadCharacterSound> _loadCharacterSoundHook;

    private nint LoadCharacterSoundDetour(nint container, int unk1, int unk2, nint unk3, ulong unk4, int unk5, int unk6, ulong unk7)
    {
        using var performance = _performance.Measure(PerformanceType.LoadSound);
        var       last        = _characterSoundData.Value;
        var       character   = *(GameObject**)(container + 8);
        _characterSoundData.Value = _collectionResolver.IdentifyCollection(character, true);
        var ret = _loadCharacterSoundHook.Original(container, unk1, unk2, unk3, unk4, unk5, unk6, unk7);
        _characterSoundData.Value = last;
        return ret;
    }

    /// <summary>
    /// The timeline object loads the requested .tmb and .pap files. The .tmb files load the respective .avfx files.
    /// We can obtain the associated game object from the timelines 28'th vfunc and use that to apply the correct collection.
    /// </summary>
    private delegate ulong LoadTimelineResourcesDelegate(nint timeline);

    [Signature(Sigs.LoadTimelineResources, DetourName = nameof(LoadTimelineResourcesDetour))]
    private readonly Hook<LoadTimelineResourcesDelegate> _loadTimelineResourcesHook = null!;

    private ulong LoadTimelineResourcesDetour(nint timeline)
    {
        using var performance = _performance.Measure(PerformanceType.TimelineResources);
        // Do not check timeline loading in cutscenes.
        if (_conditions[ConditionFlag.OccupiedInCutSceneEvent] || _conditions[ConditionFlag.WatchingCutscene78])
            return _loadTimelineResourcesHook.Original(timeline);

        var last = _animationLoadData.Value;
        _animationLoadData.Value = GetDataFromTimeline(timeline);
        var ret = _loadTimelineResourcesHook.Original(timeline);
        _animationLoadData.Value = last;
        return ret;
    }

    /// <summary>
    /// Probably used when the base idle animation gets loaded.
    /// Make it aware of the correct collection to load the correct pap files.
    /// </summary>
    private delegate void CharacterBaseNoArgumentDelegate(nint drawBase);

    [Signature(Sigs.CharacterBaseLoadAnimation, DetourName = nameof(CharacterBaseLoadAnimationDetour))]
    private readonly Hook<CharacterBaseNoArgumentDelegate> _characterBaseLoadAnimationHook = null!;

    private void CharacterBaseLoadAnimationDetour(nint drawObject)
    {
        using var performance = _performance.Measure(PerformanceType.LoadCharacterBaseAnimation);
        var       last        = _animationLoadData.Value;
        var       lastObj     = _drawObjectState.LastGameObject;
        if (lastObj == nint.Zero && _drawObjectState.TryGetValue(drawObject, out var p))
            lastObj = p.Item1;
        _animationLoadData.Value = _collectionResolver.IdentifyCollection((GameObject*)lastObj, true);
        _characterBaseLoadAnimationHook.Original(drawObject);
        _animationLoadData.Value = last;
    }

    /// <summary> Unknown what exactly this is but it seems to load a bunch of paps. </summary>
    private delegate void LoadSomePap(nint a1, int a2, nint a3, int a4);

    [Signature(Sigs.LoadSomePap, DetourName = nameof(LoadSomePapDetour))]
    private readonly Hook<LoadSomePap> _loadSomePapHook = null!;

    private void LoadSomePapDetour(nint a1, int a2, nint a3, int a4)
    {
        using var performance = _performance.Measure(PerformanceType.LoadPap);
        var       timelinePtr = a1 + Offsets.TimeLinePtr;
        var       last        = _animationLoadData.Value;
        if (timelinePtr != nint.Zero)
        {
            var actorIdx = (int)(*(*(ulong**)timelinePtr + 1) >> 3);
            if (actorIdx >= 0 && actorIdx < _objects.Length)
                _animationLoadData.Value = _collectionResolver.IdentifyCollection((GameObject*)_objects.GetObjectAddress(actorIdx), true);
        }

        _loadSomePapHook.Original(a1, a2, a3, a4);
        _animationLoadData.Value = last;
    }

    private delegate void SomeActionLoadDelegate(ActionTimelineManager* timelineManager);

    /// <summary> Seems to load character actions when zoning or changing class, maybe. </summary>
    [Signature(Sigs.LoadSomeAction, DetourName = nameof(SomeActionLoadDetour))]
    private readonly Hook<SomeActionLoadDelegate> _someActionLoadHook = null!;

    private void SomeActionLoadDetour(ActionTimelineManager* timelineManager)
    {
        using var performance = _performance.Measure(PerformanceType.LoadAction);
        var       last        = _animationLoadData.Value;
        _animationLoadData.Value = _collectionResolver.IdentifyCollection((GameObject*)timelineManager->Parent, true);
        _someActionLoadHook.Original(timelineManager);
        _animationLoadData.Value = last;
    }

    /// <summary> Load a VFX specifically for a character. </summary>
    private delegate nint LoadCharacterVfxDelegate(byte* vfxPath, VfxParams* vfxParams, byte unk1, byte unk2, float unk3, int unk4);

    [Signature(Sigs.LoadCharacterVfx, DetourName = nameof(LoadCharacterVfxDetour))]
    private readonly Hook<LoadCharacterVfxDelegate> _loadCharacterVfxHook = null!;

    private nint LoadCharacterVfxDetour(byte* vfxPath, VfxParams* vfxParams, byte unk1, byte unk2, float unk3, int unk4)
    {
        using var performance = _performance.Measure(PerformanceType.LoadCharacterVfx);
        var       last        = _animationLoadData.Value;
        if (vfxParams != null && vfxParams->GameObjectId != unchecked((uint)-1))
        {
            var obj = vfxParams->GameObjectType switch
            {
                0 => _objects.SearchById(vfxParams->GameObjectId),
                2 => _objects[(int)vfxParams->GameObjectId],
                4 => GetOwnedObject(vfxParams->GameObjectId),
                _ => null,
            };
            _animationLoadData.Value = obj != null
                ? _collectionResolver.IdentifyCollection((GameObject*)obj.Address, true)
                : ResolveData.Invalid;
        }
        else
        {
            _animationLoadData.Value = ResolveData.Invalid;
        }

        var ret = _loadCharacterVfxHook.Original(vfxPath, vfxParams, unk1, unk2, unk3, unk4);
        Penumbra.Log.Excessive(
            $"Load Character VFX: {new ByteString(vfxPath)} 0x{vfxParams->GameObjectId:X} {vfxParams->TargetCount} {unk1} {unk2} {unk3} {unk4} -> "
          + $"0x{ret:X} {_animationLoadData.Value.ModCollection.Name} {_animationLoadData.Value.AssociatedGameObject} {last.ModCollection.Name} {last.AssociatedGameObject}");
        _animationLoadData.Value = last;
        return ret;
    }

    /// <summary> Load a ground-based area VFX. </summary>
    private delegate nint LoadAreaVfxDelegate(uint vfxId, float* pos, GameObject* caster, float unk1, float unk2, byte unk3);

    [Signature(Sigs.LoadAreaVfx, DetourName = nameof(LoadAreaVfxDetour))]
    private readonly Hook<LoadAreaVfxDelegate> _loadAreaVfxHook = null!;

    private nint LoadAreaVfxDetour(uint vfxId, float* pos, GameObject* caster, float unk1, float unk2, byte unk3)
    {
        using var performance = _performance.Measure(PerformanceType.LoadAreaVfx);
        var       last        = _animationLoadData.Value;
        _animationLoadData.Value = caster != null
            ? _collectionResolver.IdentifyCollection(caster, true)
            : ResolveData.Invalid;

        var ret = _loadAreaVfxHook.Original(vfxId, pos, caster, unk1, unk2, unk3);
        Penumbra.Log.Excessive(
            $"Load Area VFX: {vfxId}, {pos[0]} {pos[1]} {pos[2]} {(caster != null ? new ByteString(caster->GetName()).ToString() : "Unknown")} {unk1} {unk2} {unk3}"
          + $" -> {ret:X} {_animationLoadData.Value.ModCollection.Name} {_animationLoadData.Value.AssociatedGameObject} {last.ModCollection.Name} {last.AssociatedGameObject}");
        _animationLoadData.Value = last;
        return ret;
    }


    /// <summary> Called when some action timelines update. </summary>
    private delegate void ScheduleClipUpdate(ClipScheduler* x);

    [Signature(Sigs.ScheduleClipUpdate, DetourName = nameof(ScheduleClipUpdateDetour))]
    private readonly Hook<ScheduleClipUpdate> _scheduleClipUpdateHook = null!;

    private void ScheduleClipUpdateDetour(ClipScheduler* x)
    {
        using var performance = _performance.Measure(PerformanceType.ScheduleClipUpdate);
        var       last        = _animationLoadData.Value;
        var       timeline    = x->SchedulerTimeline;
        _animationLoadData.Value = GetDataFromTimeline(timeline);
        _scheduleClipUpdateHook.Original(x);
        _animationLoadData.Value = last;
    }

    /// <summary> Search an object by its id, then get its minion/mount/ornament. </summary>
    private Dalamud.Game.ClientState.Objects.Types.GameObject? GetOwnedObject(uint id)
    {
        var owner = _objects.SearchById(id);
        if (owner == null)
            return null;

        var idx = ((GameObject*)owner.Address)->ObjectIndex;
        return _objects[idx + 1];
    }

    /// <summary> Use timelines vfuncs to obtain the associated game object. </summary>
    private ResolveData GetDataFromTimeline(nint timeline)
    {
        try
        {
            if (timeline != nint.Zero)
            {
                var getGameObjectIdx = ((delegate* unmanaged<nint, int>**)timeline)[0][Offsets.GetGameObjectIdxVfunc];
                var idx              = getGameObjectIdx(timeline);
                if (idx >= 0 && idx < _objects.Length)
                {
                    var obj = (GameObject*)_objects.GetObjectAddress(idx);
                    return obj != null ? _collectionResolver.IdentifyCollection(obj, true) : ResolveData.Invalid;
                }
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Error getting timeline data for 0x{timeline:X}:\n{e}");
        }

        return ResolveData.Invalid;
    }

    private delegate void UnkMountAnimationDelegate(DrawObject* drawObject, uint unk1, byte unk2, uint unk3);

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 89 54 24", DetourName = nameof(UnkMountAnimationDetour))]
    private readonly Hook<UnkMountAnimationDelegate> _unkMountAnimationHook = null!;

    private void UnkMountAnimationDetour(DrawObject* drawObject, uint unk1, byte unk2, uint unk3)
    {
        var last = _animationLoadData.Value;
        _animationLoadData.Value = _collectionResolver.IdentifyCollection(drawObject, true);
        _unkMountAnimationHook.Original(drawObject, unk1, unk2, unk3);
        _animationLoadData.Value = last;
    }


    private delegate void UnkParasolAnimationDelegate(DrawObject* drawObject, int unk1);

    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 89 54 24 ?? 57 48 83 EC ?? 48 8B F9", DetourName = nameof(UnkParasolAnimationDetour))]
    private readonly Hook<UnkParasolAnimationDelegate> _unkParasolAnimationHook = null!;

    private void UnkParasolAnimationDetour(DrawObject* drawObject, int unk1)
    {
        var last = _animationLoadData.Value;
        _animationLoadData.Value = _collectionResolver.IdentifyCollection(drawObject, true);
        _unkParasolAnimationHook.Original(drawObject, unk1);
        _animationLoadData.Value = last;
    }

    [Signature("E8 ?? ?? ?? ?? F6 43 ?? ?? 74 ?? 48 8B CB", DetourName = nameof(DismountDetour))]
    private readonly Hook<DismountDelegate> _dismountHook = null!;

    private delegate void DismountDelegate(nint a1, nint a2);

    private void DismountDetour(nint a1, nint a2)
    {
        if (a1 == nint.Zero)
        {
            _dismountHook.Original(a1, a2);
            return;
        }

        var gameObject = *(GameObject**)(a1 + 8);
        if (gameObject == null)
        {
            _dismountHook.Original(a1, a2);
            return;
        }

        var last = _animationLoadData.Value;
        _animationLoadData.Value = _collectionResolver.IdentifyCollection(gameObject, true);
        _dismountHook.Original(a1, a2);
        _animationLoadData.Value = last;
    }

    [Signature("48 89 6C 24 ?? 41 54 41 56 41 57 48 81 EC", DetourName = nameof(ApricotListenerSoundPlayDetour))]
    private readonly Hook<ApricotListenerSoundPlayDelegate> _apricotListenerSoundPlayHook = null!;

    private delegate nint ApricotListenerSoundPlayDelegate(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6);

    private nint ApricotListenerSoundPlayDetour(nint a1, nint a2, nint a3, nint a4, nint a5, nint a6)
    {
        if (a6 == nint.Zero)
            return _apricotListenerSoundPlayHook!.Original(a1, a2, a3, a4, a5, a6);

        var last = _animationLoadData.Value;
        // a6 is some instance of Apricot.IInstanceListenner, in some cases we can obtain the associated caster via vfunc 1.
        var gameObject = (*(delegate* unmanaged<nint, GameObject*>**)a6)[1](a6);
        if (gameObject != null)
        {
            _animationLoadData.Value = _collectionResolver.IdentifyCollection(gameObject, true);
        }
        else
        {
            // for VfxListenner we can obtain the associated draw object as its first member,
            // if the object has different type, drawObject will contain other values or garbage,
            // but only be used in a dictionary pointer lookup, so this does not hurt.
            var drawObject = ((DrawObject**)a6)[1];
            if (drawObject != null)
                _animationLoadData.Value = _collectionResolver.IdentifyCollection(drawObject, true);
        }

        var ret = _apricotListenerSoundPlayHook!.Original(a1, a2, a3, a4, a5, a6);
        _animationLoadData.Value = last;
        return ret;
    }
}
