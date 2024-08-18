using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.Interop.Services;
using Penumbra.String;

namespace Penumbra.Interop.PathResolving;

public sealed class PathState(CollectionResolver collectionResolver, MetaState metaState, CharacterUtility characterUtility)
    : IDisposable, IService
{
    public readonly CollectionResolver CollectionResolver = collectionResolver;
    public readonly MetaState          MetaState          = metaState;
    public readonly CharacterUtility   CharacterUtility   = characterUtility;

    private readonly ThreadLocal<ResolveData> _resolveData     = new(() => ResolveData.Invalid, true);
    private readonly ThreadLocal<uint>        _internalResolve = new(() => 0, false);

    public IList<ResolveData> CurrentData
        => _resolveData.Values;

    public bool InInternalResolve
        => _internalResolve.Value != 0u;


    public void Dispose()
    {
        _resolveData.Dispose();
        _internalResolve.Dispose();
    }

    public bool Consume(CiByteString _, out ResolveData collection)
    {
        if (_resolveData.IsValueCreated)
        {
            collection         = _resolveData.Value;
            _resolveData.Value = ResolveData.Invalid;
            return collection.Valid;
        }

        collection = ResolveData.Invalid;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nint ResolvePath(nint gameObject, ModCollection collection, nint path)
    {
        if (path == nint.Zero)
            return path;

        if (!InInternalResolve)
            _resolveData.Value = collection.ToResolveData(gameObject);
        return path;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nint ResolvePath(ResolveData data, nint path)
    {
        if (path == nint.Zero)
            return path;

        if (!InInternalResolve)
            _resolveData.Value = data;
        return path;
    }

    /// <summary>
    /// Temporarily disables metadata mod application and resolve data capture on the current thread. <para />
    /// Must be called to prevent race conditions between Penumbra's internal path resolution (for example for Resource Trees) and the game's path resolution. <para />
    /// Please note that this will make path resolution cases that depend on metadata incorrect.
    /// </summary>
    /// <returns> A struct that will undo this operation when disposed. Best used with: <code>using (var _ = pathState.EnterInternalResolve()) { ... }</code> </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public InternalResolveRaii EnterInternalResolve()
        => new(this);

    public readonly ref struct InternalResolveRaii
    {
        private readonly ThreadLocal<uint> _internalResolve;

        public InternalResolveRaii(PathState parent)
        {
            _internalResolve = parent._internalResolve;
            ++_internalResolve.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Dispose()
        {
            --_internalResolve.Value;
        }
    }
}
