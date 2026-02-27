using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.Hooks.Resources;
using Penumbra.Interop.Structs;
using Penumbra.String;

namespace Penumbra.Interop.PathResolving;

/// <summary>
/// Materials and avfx do contain their own paths to textures and shader packages or atex respectively.
/// Those are loaded synchronously.
/// Thus, we need to ensure the correct files are loaded when a material is loaded.
/// </summary>
public sealed unsafe class SubfileHelper : IDisposable, IReadOnlyCollection<KeyValuePair<nint, ResolveData>>, Luna.IService
{
    private readonly GameState                _gameState;
    private readonly ResourceLoader           _loader;
    private readonly ResourceHandleDestructor _resourceHandleDestructor;
    private readonly CollectionStorage        _collections;

    public SubfileHelper(GameState gameState, ResourceLoader loader, ResourceHandleDestructor resourceHandleDestructor,
        CollectionStorage collections)
    {
        _gameState                = gameState;
        _loader                   = loader;
        _resourceHandleDestructor = resourceHandleDestructor;
        _collections              = collections;

        _loader.PreLoadFile += SubfileContainerRequested;
        _resourceHandleDestructor.Subscribe(ResourceDestroyed, ResourceHandleDestructor.Priority.SubfileHelper);
    }

    public IEnumerator<KeyValuePair<nint, ResolveData>> GetEnumerator()
        => _gameState.SubFileCollection.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _gameState.SubFileCollection.Count;

    public ResolveData MtrlData
        => _gameState.MtrlData.IsValueCreated ? _gameState.MtrlData.Value : ResolveData.Invalid;

    public ResolveData AvfxData
        => _gameState.AvfxData.IsValueCreated ? _gameState.AvfxData.Value : ResolveData.Invalid;

    /// <summary>
    /// Check specifically for shpk and tex files whether we are currently in a material load,
    /// and for scd and atex files whether we are in an avfx load. </summary>
    public bool HandleSubFiles(ResourceType type, out ResolveData collection)
    {
        switch (type)
        {
            case ResourceType.Tex when _gameState.MtrlData.Value.Valid:
            case ResourceType.Shpk when _gameState.MtrlData.Value.Valid:
                collection = _gameState.MtrlData.Value;
                return true;
            case ResourceType.Scd when _gameState.AvfxData.Value.Valid:
            case ResourceType.Atex when _gameState.AvfxData.Value.Valid:
                collection = _gameState.AvfxData.Value;
                return true;
        }

        collection = ResolveData.Invalid;
        return false;
    }

    public void Dispose()
    {
        _loader.PreLoadFile -= SubfileContainerRequested;
        _resourceHandleDestructor.Unsubscribe(ResourceDestroyed);
    }

    private void SubfileContainerRequested(ResourceHandle* handle, CiByteString actualPath, ReadOnlySpan<byte> additionalData)
    {
        // Done during SQPack load, so guaranteed to be done before the subfiles are loaded.
        switch (handle->FileType)
        {
            case ResourceType.Mtrl:
                if (PathDataHandler.ReadMtrl(additionalData, out var mtrlData))
                    _gameState.SubFileCollection[(nint)handle] = new ResolveData(_collections.ByLocalId(mtrlData.Collection));
                break;
            case ResourceType.Avfx:
                if (PathDataHandler.Read(additionalData, out var avfxData))
                    _gameState.SubFileCollection[(nint)handle] = new ResolveData(_collections.ByLocalId(avfxData.Collection));

                break;
        }
    }

    private void ResourceDestroyed(in ResourceHandleDestructor.Arguments arguments)
        => _gameState.SubFileCollection.TryRemove((nint)arguments.ResourceHandle, out _);
}
