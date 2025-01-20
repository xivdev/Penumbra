using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Interop.PathResolving;
using Penumbra.String.Classes;

namespace Penumbra.Interop;

public class GameState : IService
{
    #region Last Game Object

    private readonly ThreadLocal<Queue<nint>> _lastGameObject     = new(() => new Queue<nint>());
    public readonly  ThreadLocal<bool>        CharacterAssociated = new(() => false);

    public nint LastGameObject
        => _lastGameObject.IsValueCreated && _lastGameObject.Value!.Count > 0 ? _lastGameObject.Value.Peek() : nint.Zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public unsafe void QueueGameObject(GameObject* gameObject)
        => QueueGameObject((nint)gameObject);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void QueueGameObject(nint gameObject)
        => _lastGameObject.Value!.Enqueue(gameObject);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void DequeueGameObject()
        => _lastGameObject.Value!.TryDequeue(out _);

    #endregion

    #region Animation Data

    private readonly ThreadLocal<ResolveData> _animationLoadData = new(() => ResolveData.Invalid, true);

    public ResolveData AnimationData
        => _animationLoadData.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ResolveData SetAnimationData(ResolveData data)
    {
        var old = _animationLoadData.Value;
        _animationLoadData.Value = data;
        return old;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void RestoreAnimationData(ResolveData old)
        => _animationLoadData.Value = old;

    public readonly ThreadLocal<bool> InLoadActionTmb = new(() => false);
    public readonly ThreadLocal<bool> SkipTmbCache    = new(() => false);

    #endregion

    #region Sound Data

    private readonly ThreadLocal<ResolveData> _characterSoundData = new(() => ResolveData.Invalid, true);

    public ResolveData SoundData
        => _animationLoadData.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ResolveData SetSoundData(ResolveData data)
    {
        var old = _characterSoundData.Value;
        _characterSoundData.Value = data;
        return old;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void RestoreSoundData(ResolveData old)
        => _characterSoundData.Value = old;

    #endregion

    #region Subfiles

    public readonly ThreadLocal<ResolveData> MtrlData = new(() => ResolveData.Invalid);
    public readonly ThreadLocal<ResolveData> AvfxData = new(() => ResolveData.Invalid);

    public readonly ConcurrentDictionary<nint, ResolveData> SubFileCollection = new();

    public ResolveData LoadSubFileHelper(nint resourceHandle)
    {
        if (resourceHandle == nint.Zero)
            return ResolveData.Invalid;

        return SubFileCollection.TryGetValue(resourceHandle, out var c) ? c : ResolveData.Invalid;
    }

    #endregion

    /// <summary> Return the correct resolve data from the stored data. </summary>
    public unsafe bool HandleFiles(CollectionResolver resolver, ResourceType type, Utf8GamePath _, out ResolveData resolveData)
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

        var lastObj = LastGameObject;
        if (lastObj != nint.Zero)
        {
            resolveData = resolver.IdentifyCollection((GameObject*)lastObj, true);
            return true;
        }

        resolveData = ResolveData.Invalid;
        return false;
    }
}
