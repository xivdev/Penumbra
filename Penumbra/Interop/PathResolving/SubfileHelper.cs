using System.Collections.Concurrent;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.Interop.ResourceLoading;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Services;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Interop.PathResolving;

/// <summary>
/// Materials and avfx do contain their own paths to textures and shader packages or atex respectively.
/// Those are loaded synchronously.
/// Thus, we need to ensure the correct files are loaded when a material is loaded.
/// </summary>
public unsafe class SubfileHelper : IDisposable, IReadOnlyCollection<KeyValuePair<nint, ResolveData>>
{
    private readonly PerformanceTracker  _performance;
    private readonly ResourceLoader      _loader;
    private readonly GameEventManager    _events;
    private readonly CommunicatorService _communicator;

    private readonly ThreadLocal<ResolveData> _mtrlData = new(() => ResolveData.Invalid);
    private readonly ThreadLocal<ResolveData> _avfxData = new(() => ResolveData.Invalid);

    private readonly ConcurrentDictionary<nint, ResolveData> _subFileCollection = new();

    public SubfileHelper(PerformanceTracker performance, ResourceLoader loader, GameEventManager events, CommunicatorService communicator)
    {
        SignatureHelper.Initialise(this);

        _performance  = performance;
        _loader       = loader;
        _events       = events;
        _communicator = communicator;

        _loadMtrlShpkHook.Enable();
        _loadMtrlTexHook.Enable();
        _apricotResourceLoadHook.Enable();
        _loader.ResourceLoaded           += SubfileContainerRequested;
        _events.ResourceHandleDestructor += ResourceDestroyed;
    }


    public IEnumerator<KeyValuePair<nint, ResolveData>> GetEnumerator()
        => _subFileCollection.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _subFileCollection.Count;

    public ResolveData MtrlData
        => _mtrlData.IsValueCreated ? _mtrlData.Value : ResolveData.Invalid;

    public ResolveData AvfxData
        => _avfxData.IsValueCreated ? _avfxData.Value : ResolveData.Invalid;

    /// <summary>
    /// Check specifically for shpk and tex files whether we are currently in a material load,
    /// and for scd and atex files whether we are in an avfx load. </summary>
    public bool HandleSubFiles(ResourceType type, out ResolveData collection)
    {
        switch (type)
        {
            case ResourceType.Tex when _mtrlData.Value.Valid:
            case ResourceType.Shpk when _mtrlData.Value.Valid:
                collection = _mtrlData.Value;
                return true;
            case ResourceType.Scd when _avfxData.Value.Valid:
            case ResourceType.Atex when _avfxData.Value.Valid:
                collection = _avfxData.Value;
                return true;
        }

        collection = ResolveData.Invalid;
        return false;
    }

    /// <summary> Materials, TMB, and AVFX need to be set per collection so they can load their sub files independently from each other. </summary>
    public static void HandleCollection(ResolveData resolveData, ByteString path, bool nonDefault, ResourceType type, FullPath? resolved,
        out (FullPath?, ResolveData) data)
    {
        if (nonDefault)
            switch (type)
            {
                case ResourceType.Mtrl:
                case ResourceType.Avfx:
                case ResourceType.Tmb:
                    var fullPath = new FullPath($"|{resolveData.ModCollection.Name}_{resolveData.ModCollection.ChangeCounter}|{path}");
                    data = (fullPath, resolveData);
                    return;
            }

        data = (resolved, resolveData);
    }

    public void Dispose()
    {
        _loader.ResourceLoaded           -= SubfileContainerRequested;
        _events.ResourceHandleDestructor -= ResourceDestroyed;
        _loadMtrlShpkHook.Dispose();
        _loadMtrlTexHook.Dispose();
        _apricotResourceLoadHook.Dispose();
    }

    private void SubfileContainerRequested(ResourceHandle* handle, Utf8GamePath originalPath, FullPath? manipulatedPath,
        ResolveData resolveData)
    {
        switch (handle->FileType)
        {
            case ResourceType.Mtrl:
            case ResourceType.Avfx:
                if (handle->FileSize == 0)
                    _subFileCollection[(nint)handle] = resolveData;

                break;
        }
    }

    private void ResourceDestroyed(ResourceHandle* handle)
        => _subFileCollection.TryRemove((nint)handle, out _);

    private delegate byte LoadMtrlFilesDelegate(nint mtrlResourceHandle);

    [Signature(Sigs.LoadMtrlTex, DetourName = nameof(LoadMtrlTexDetour))]
    private readonly Hook<LoadMtrlFilesDelegate> _loadMtrlTexHook = null!;

    private byte LoadMtrlTexDetour(nint mtrlResourceHandle)
    {
        using var performance = _performance.Measure(PerformanceType.LoadTextures);
        var       last        = _mtrlData.Value;
        _mtrlData.Value = LoadFileHelper(mtrlResourceHandle);
        var ret = _loadMtrlTexHook.Original(mtrlResourceHandle);
        _mtrlData.Value = last;
        return ret;
    }

    [Signature(Sigs.LoadMtrlShpk, DetourName = nameof(LoadMtrlShpkDetour))]
    private readonly Hook<LoadMtrlFilesDelegate> _loadMtrlShpkHook = null!;

    private byte LoadMtrlShpkDetour(nint mtrlResourceHandle)
    {
        using var performance = _performance.Measure(PerformanceType.LoadShaders);
        var       last        = _mtrlData.Value;
        var       mtrlData    = LoadFileHelper(mtrlResourceHandle);
        _mtrlData.Value = mtrlData;
        var ret = _loadMtrlShpkHook.Original(mtrlResourceHandle);
        _mtrlData.Value = last;
        _communicator.MtrlShpkLoaded.Invoke(mtrlResourceHandle, mtrlData.AssociatedGameObject);
        return ret;
    }

    private ResolveData LoadFileHelper(nint resourceHandle)
    {
        if (resourceHandle == nint.Zero)
            return ResolveData.Invalid;

        return _subFileCollection.TryGetValue(resourceHandle, out var c) ? c : ResolveData.Invalid;
    }


    private delegate byte ApricotResourceLoadDelegate(nint handle, nint unk1, byte unk2);

    [Signature(Sigs.ApricotResourceLoad, DetourName = nameof(ApricotResourceLoadDetour))]
    private readonly Hook<ApricotResourceLoadDelegate> _apricotResourceLoadHook = null!;

    private byte ApricotResourceLoadDetour(nint handle, nint unk1, byte unk2)
    {
        using var performance = _performance.Measure(PerformanceType.LoadApricotResources);
        var       last        = _avfxData.Value;
        _avfxData.Value = LoadFileHelper(handle);
        var ret = _apricotResourceLoadHook.Original(handle, unk1, unk2);
        _avfxData.Value = last;
        return ret;
    }
}
