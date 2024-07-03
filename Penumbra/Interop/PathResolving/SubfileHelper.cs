using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.Hooks.Resources;
using Penumbra.Interop.Structs;
using Penumbra.String.Classes;

namespace Penumbra.Interop.PathResolving;

/// <summary>
/// Materials and avfx do contain their own paths to textures and shader packages or atex respectively.
/// Those are loaded synchronously.
/// Thus, we need to ensure the correct files are loaded when a material is loaded.
/// </summary>
public sealed unsafe class SubfileHelper : IDisposable, IReadOnlyCollection<KeyValuePair<nint, ResolveData>>, IService
{
    private readonly GameState                _gameState;
    private readonly ResourceLoader           _loader;
    private readonly ResourceHandleDestructor _resourceHandleDestructor;

    public SubfileHelper(GameState gameState, ResourceLoader loader, ResourceHandleDestructor resourceHandleDestructor)
    {
        _gameState                = gameState;
        _loader                   = loader;
        _resourceHandleDestructor = resourceHandleDestructor;

        _loader.ResourceLoaded += SubfileContainerRequested;
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
        _loader.ResourceLoaded -= SubfileContainerRequested;
        _resourceHandleDestructor.Unsubscribe(ResourceDestroyed);
    }

    private void SubfileContainerRequested(ResourceHandle* handle, Utf8GamePath originalPath, FullPath? manipulatedPath,
        ResolveData resolveData)
    {
        switch (handle->FileType)
        {
            case ResourceType.Mtrl:
            case ResourceType.Avfx:
                if (handle->FileSize == 0)
                    _gameState.SubFileCollection[(nint)handle] = resolveData;

                break;
        }
    }

    private void ResourceDestroyed(ResourceHandle* handle)
        => _gameState.SubFileCollection.TryRemove((nint)handle, out _);
}
