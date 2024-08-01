using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.CrashHandler.Buffers;
using Penumbra.GameData;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Structs;
using Penumbra.Services;
using Penumbra.String;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Load a VFX specifically for a character. </summary>
public sealed unsafe class LoadCharacterVfx : FastHook<LoadCharacterVfx.Delegate>
{
    private readonly GameState           _state;
    private readonly CollectionResolver  _collectionResolver;
    private readonly ObjectManager       _objects;
    private readonly CrashHandlerService _crashHandler;

    public LoadCharacterVfx(HookManager hooks, GameState state, CollectionResolver collectionResolver, ObjectManager objects,
        CrashHandlerService crashHandler)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        _objects            = objects;
        _crashHandler       = crashHandler;
        Task                = hooks.CreateHook<Delegate>("Load Character VFX", Sigs.LoadCharacterVfx, Detour, !HookOverrides.Instance.Animation.LoadCharacterVfx);
    }

    public delegate nint Delegate(byte* vfxPath, VfxParams* vfxParams, byte unk1, byte unk2, float unk3, int unk4);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private nint Detour(byte* vfxPath, VfxParams* vfxParams, byte unk1, byte unk2, float unk3, int unk4)
    {
        var newData = ResolveData.Invalid;
        if (vfxParams != null && vfxParams->GameObjectId != unchecked((uint)-1))
        {
            var obj = vfxParams->GameObjectType switch
            {
                0 => _objects.ById(vfxParams->GameObjectId),
                2 => _objects[(int)vfxParams->GameObjectId],
                4 => GetOwnedObject(vfxParams->GameObjectId),
                _ => Actor.Null,
            };
            newData = obj.Valid
                ? _collectionResolver.IdentifyCollection((GameObject*)obj.Address, true)
                : ResolveData.Invalid;
        }

        var last = _state.SetAnimationData(newData);
        _crashHandler.LogAnimation(newData.AssociatedGameObject, newData.ModCollection, AnimationInvocationType.LoadCharacterVfx);
        var ret = Task.Result.Original(vfxPath, vfxParams, unk1, unk2, unk3, unk4);
        Penumbra.Log.Excessive(
            $"[Load Character VFX] Invoked with {new ByteString(vfxPath)}, 0x{vfxParams->GameObjectId:X}, {vfxParams->TargetCount}, {unk1}, {unk2}, {unk3}, {unk4} -> 0x{ret:X}.");
        _state.RestoreAnimationData(last);
        return ret;
    }

    /// <summary> Search an object by its id, then get its minion/mount/ornament. </summary>
    private Actor GetOwnedObject(uint id)
    {
        var owner = _objects.ById(id);
        return !owner.Valid
            ? Actor.Null
            : _objects[owner.Index.Index + 1];
    }
}
