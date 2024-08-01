using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.CrashHandler.Buffers;
using Penumbra.Interop.PathResolving;
using Penumbra.Services;

namespace Penumbra.Interop.Hooks.Animation;

/// <summary> Characters load some of their voice lines or whatever with this function. </summary>
public sealed unsafe class LoadCharacterSound : FastHook<LoadCharacterSound.Delegate>
{
    private readonly GameState           _state;
    private readonly CollectionResolver  _collectionResolver;
    private readonly CrashHandlerService _crashHandler;

    public LoadCharacterSound(HookManager hooks, GameState state, CollectionResolver collectionResolver, CrashHandlerService crashHandler)
    {
        _state              = state;
        _collectionResolver = collectionResolver;
        _crashHandler       = crashHandler;
        Task = hooks.CreateHook<Delegate>("Load Character Sound", (nint)VfxContainer.MemberFunctionPointers.LoadCharacterSound, Detour,
            !HookOverrides.Instance.Animation.LoadCharacterSound);
    }

    public delegate nint Delegate(VfxContainer* container, int unk1, int unk2, nint unk3, ulong unk4, int unk5, int unk6, ulong unk7);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private nint Detour(VfxContainer* container, int unk1, int unk2, nint unk3, ulong unk4, int unk5, int unk6, ulong unk7)
    {
        var character = (GameObject*)container->OwnerObject;
        var newData   = _collectionResolver.IdentifyCollection(character, true);
        var last      = _state.SetSoundData(newData);
        _crashHandler.LogAnimation(newData.AssociatedGameObject, newData.ModCollection, AnimationInvocationType.LoadCharacterSound);
        var ret = Task.Result.Original(container, unk1, unk2, unk3, unk4, unk5, unk6, unk7);
        Penumbra.Log.Excessive(
            $"[Load Character Sound] Invoked with {(nint)container:X} {unk1} {unk2} {unk3} {unk4} {unk5} {unk6} {unk7} -> {ret}.");
        _state.RestoreSoundData(last);
        return ret;
    }
}
