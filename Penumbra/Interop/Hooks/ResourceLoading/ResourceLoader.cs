using FFXIVClientStructs.FFXIV.Client.System.Resource;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Interop.Hooks.Resources;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.SafeHandles;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using FileMode = Penumbra.Interop.Structs.FileMode;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public unsafe class ResourceLoader : IDisposable, IService
{
    private readonly ResourceService          _resources;
    private readonly FileReadService          _fileReadService;
    private readonly RsfService               _rsfService;
    private readonly PapHandler               _papHandler;
    private readonly Configuration            _config;
    private readonly ResourceHandleDestructor _destructor;

    private readonly ConcurrentDictionary<nint, Utf8GamePath> _ongoingLoads = [];

    private ResolveData                                        _resolvedData = ResolveData.Invalid;
    public event Action<Utf8GamePath, FullPath?, ResolveData>? PapRequested;

    public IReadOnlyDictionary<nint, Utf8GamePath> OngoingLoads
        => _ongoingLoads;

    public ResourceLoader(ResourceService resources, FileReadService fileReadService, RsfService rsfService, Configuration config, PeSigScanner sigScanner,
        ResourceHandleDestructor destructor)
    {
        _resources       = resources;
        _fileReadService = fileReadService;
        _rsfService      = rsfService;
        _config          = config;
        _destructor      = destructor;
        ResetResolvePath();

        _resources.ResourceRequested     += ResourceHandler;
        _resources.ResourceStateUpdating += ResourceStateUpdatingHandler;
        _resources.ResourceStateUpdated  += ResourceStateUpdatedHandler;
        _resources.ResourceHandleIncRef  += IncRefProtection;
        _resources.ResourceHandleDecRef  += DecRefProtection;
        _fileReadService.ReadSqPack      += ReadSqPackDetour;
        _destructor.Subscribe(ResourceDestructorHandler, ResourceHandleDestructor.Priority.ResourceLoader);

        _papHandler = new PapHandler(sigScanner, PapResourceHandler);
        _papHandler.Enable();
    }

    private int PapResourceHandler(void* self, byte* path, int length)
    {
        if (!_config.EnableMods || !Utf8GamePath.FromPointer(path, MetaDataComputation.CiCrc32, out var gamePath))
            return length;

        var (resolvedPath, data) = _incMode.Value
            ? (null, ResolveData.Invalid)
            : _resolvedData.Valid
                ? (_resolvedData.ModCollection.ResolvePath(gamePath), _resolvedData)
                : ResolvePath(gamePath, ResourceCategory.Chara, ResourceType.Pap);


        if (!resolvedPath.HasValue)
        {
            PapRequested?.Invoke(gamePath, null, data);
            return length;
        }

        PapRequested?.Invoke(gamePath, resolvedPath.Value, data);
        NativeMemory.Copy(resolvedPath.Value.InternalName.Path, path, (nuint)resolvedPath.Value.InternalName.Length);
        path[resolvedPath.Value.InternalName.Length] = 0;
        return resolvedPath.Value.InternalName.Length;
    }

    /// <summary> Load a resource for a given path and a specific collection. </summary>
    public ResourceHandle* LoadResolvedResource(ResourceCategory category, ResourceType type, CiByteString path, ResolveData resolveData)
    {
        _resolvedData = resolveData;
        var ret = _resources.GetResource(category, type, path);
        _resolvedData = ResolveData.Invalid;
        return ret;
    }

    /// <summary> Load a resource for a given path and a specific collection. </summary>
    public SafeResourceHandle LoadResolvedSafeResource(ResourceCategory category, ResourceType type, CiByteString path, ResolveData resolveData)
    {
        _resolvedData = resolveData;
        var ret = _resources.GetSafeResource(category, type, path);
        _resolvedData = ResolveData.Invalid;
        return ret;
    }

    /// <summary> The function to use to resolve a given path. </summary>
    public Func<Utf8GamePath, ResourceCategory, ResourceType, (FullPath?, ResolveData)> ResolvePath = null!;

    /// <summary> Reset the ResolvePath function to always return null. </summary>
    public void ResetResolvePath()
        => ResolvePath = (_, _, _) => (null, ResolveData.Invalid);

    public delegate void ResourceLoadedDelegate(ResourceHandle* handle, Utf8GamePath originalPath, FullPath? manipulatedPath,
        ResolveData resolveData);

    /// <summary>
    /// Event fired whenever a resource is returned.
    /// If the path was manipulated by penumbra, manipulatedPath will be the file path of the loaded resource.
    /// resolveData is additional data returned by the current ResolvePath function which can contain the collection and associated game object.
    /// </summary>
    public event ResourceLoadedDelegate? ResourceLoaded;

    public delegate void FileLoadedDelegate(ResourceHandle* resource, CiByteString path, bool returnValue, bool custom,
        ReadOnlySpan<byte> additionalData);

    /// <summary>
    /// Event fired whenever a resource is newly loaded.
    /// ReturnValue indicates the return value of the loading function (which does not imply that the resource was actually successfully loaded)
    /// custom is true if the file was loaded from local files instead of the default SqPacks.
    /// AdditionalData is either empty or the part of the path inside the leading pipes.
    /// </summary>
    public event FileLoadedDelegate? FileLoaded;

    public delegate void ResourceCompleteDelegate(ResourceHandle* resource, CiByteString path, Utf8GamePath originalPath,
        ReadOnlySpan<byte> additionalData, bool isAsync);

    /// <summary>
    /// Event fired just before a resource finishes loading.
    /// <see cref="ResourceHandle.LoadState"/> must be checked to know whether the load was successful or not.
    /// AdditionalData is either empty or the part of the path inside the leading pipes.
    /// </summary>
    public event ResourceCompleteDelegate? BeforeResourceComplete;

    /// <summary>
    /// Event fired when a resource has finished loading.
    /// <see cref="ResourceHandle.LoadState"/> must be checked to know whether the load was successful or not.
    /// AdditionalData is either empty or the part of the path inside the leading pipes.
    /// </summary>
    public event ResourceCompleteDelegate? ResourceComplete;

    public void Dispose()
    {
        _resources.ResourceRequested     -= ResourceHandler;
        _resources.ResourceStateUpdating -= ResourceStateUpdatingHandler;
        _resources.ResourceStateUpdated  -= ResourceStateUpdatedHandler;
        _resources.ResourceHandleIncRef  -= IncRefProtection;
        _resources.ResourceHandleDecRef  -= DecRefProtection;
        _fileReadService.ReadSqPack      -= ReadSqPackDetour;
        _destructor.Unsubscribe(ResourceDestructorHandler);
        _papHandler.Dispose();
    }

    private void ResourceHandler(ref ResourceCategory category, ref ResourceType type, ref int hash, ref Utf8GamePath path,
        Utf8GamePath original, GetResourceParameters* parameters, ref bool sync, ref ResourceHandle* returnValue)
    {
        if (!_config.EnableMods || returnValue != null)
            return;

        CompareHash(ComputeHash(path.Path, parameters), hash, path);

        // If no replacements are being made, we still want to be able to trigger the event.
        var (resolvedPath, data) = _incMode.Value
            ? (null, ResolveData.Invalid)
            : _resolvedData.Valid
                ? (_resolvedData.ModCollection.ResolvePath(path), _resolvedData)
                : ResolvePath(path, category, type);

        if (resolvedPath == null || !Utf8GamePath.FromByteString(resolvedPath.Value.InternalName, out var p))
        {
            returnValue = _resources.GetOriginalResource(sync, category, type, hash, path.Path, original, parameters);
            TrackResourceLoad(returnValue, original);
            ResourceLoaded?.Invoke(returnValue, path, resolvedPath, data);
            return;
        }

        _rsfService.AddCrc(type, resolvedPath);
        // Replace the hash and path with the correct one for the replacement.
        hash = ComputeHash(resolvedPath.Value.InternalName, parameters);
        var oldPath = path;
        path        = p;
        returnValue = _resources.GetOriginalResource(sync, category, type, hash, path.Path, original, parameters);
        TrackResourceLoad(returnValue, original);
        ResourceLoaded?.Invoke(returnValue, oldPath, resolvedPath.Value, data);
    }

    private void TrackResourceLoad(ResourceHandle* handle, Utf8GamePath original)
    {
        if (handle->UnkState == 2 && handle->LoadState >= LoadState.Success)
            return;

        _ongoingLoads.TryAdd((nint)handle, original.Clone());
    }

    private void ResourceStateUpdatedHandler(ResourceHandle* handle, Utf8GamePath syncOriginal, (byte, LoadState) previousState, ref uint returnValue)
    {
        if (handle->UnkState != 2 || handle->LoadState < LoadState.Success || previousState is { Item1: 2, Item2: >= LoadState.Success })
            return;

        if (!_ongoingLoads.TryRemove((nint)handle, out var asyncOriginal))
            asyncOriginal = Utf8GamePath.Empty;

        var path = handle->CsHandle.FileName;
        if (!syncOriginal.IsEmpty && !asyncOriginal.IsEmpty && !syncOriginal.Equals(asyncOriginal))
            Penumbra.Log.Warning($"[ResourceLoader] Resource original paths inconsistency: 0x{(nint)handle:X}, of path {path}, sync original {syncOriginal}, async original {asyncOriginal}.");
        var original = !asyncOriginal.IsEmpty ? asyncOriginal : syncOriginal;

        Penumbra.Log.Excessive($"[ResourceLoader] Resource is complete: 0x{(nint)handle:X}, of path {path}, original {original}, state {previousState.Item1}:{previousState.Item2} -> {handle->UnkState}:{handle->LoadState}, sync: {asyncOriginal.IsEmpty}");
        if (PathDataHandler.Split(path.AsSpan(), out var actualPath, out var additionalData))
            ResourceComplete?.Invoke(handle, new CiByteString(actualPath), original, additionalData, !asyncOriginal.IsEmpty);
        else
            ResourceComplete?.Invoke(handle, path.AsByteString(), original, [], !asyncOriginal.IsEmpty);
    }

    private void ResourceStateUpdatingHandler(ResourceHandle* handle, Utf8GamePath syncOriginal)
    {
        if (handle->UnkState != 1 || handle->LoadState != LoadState.Success)
            return;

        if (!_ongoingLoads.TryGetValue((nint)handle, out var asyncOriginal))
            asyncOriginal = Utf8GamePath.Empty;

        var path     = handle->CsHandle.FileName;
        var original = asyncOriginal.IsEmpty ? syncOriginal : asyncOriginal;

        Penumbra.Log.Excessive($"[ResourceLoader] Resource is about to be complete: 0x{(nint)handle:X}, of path {path}, original {original}");
        if (PathDataHandler.Split(path.AsSpan(), out var actualPath, out var additionalData))
            BeforeResourceComplete?.Invoke(handle, new CiByteString(actualPath), original, additionalData, !asyncOriginal.IsEmpty);
        else
            BeforeResourceComplete?.Invoke(handle, path.AsByteString(), original, [], !asyncOriginal.IsEmpty);
    }

    private void ReadSqPackDetour(SeFileDescriptor* fileDescriptor, ref int priority, ref bool isSync, ref byte? returnValue)
    {
        if (fileDescriptor->ResourceHandle == null)
        {
            Penumbra.Log.Verbose(
                $"[ResourceLoader] Failure to load file from SqPack: invalid File Descriptor: {Marshal.PtrToStringUni((nint)(&fileDescriptor->Utf16FileName))}");
            return;
        }

        if (!fileDescriptor->ResourceHandle->GamePath(out var gamePath) || gamePath.Length == 0)
        {
            Penumbra.Log.Error("[ResourceLoader] Failure to load file from SqPack: invalid path specified.");
            return;
        }

        // Paths starting with a '|' are handled separately to allow for special treatment.
        // They are expected to also have a closing '|'.
        if (!PathDataHandler.Split(gamePath.Path.Span, out var actualPath, out var data))
        {
            returnValue = DefaultLoadResource(gamePath.Path, fileDescriptor, priority, isSync, []);
            return;
        }

        var path = CiByteString.FromSpanUnsafe(actualPath, gamePath.Path.IsNullTerminated, gamePath.Path.IsAsciiLowerCase,
            gamePath.Path.IsAscii);
        fileDescriptor->ResourceHandle->FileNameData   = path.Path;
        fileDescriptor->ResourceHandle->FileNameLength = path.Length;
        returnValue = DefaultLoadResource(path, fileDescriptor, priority, isSync, data);
        // Return original resource handle path so that they can be loaded separately.
        fileDescriptor->ResourceHandle->FileNameData   = gamePath.Path.Path;
        fileDescriptor->ResourceHandle->FileNameLength = gamePath.Path.Length;
    }


    /// <summary> Load a resource by its path. If it is rooted, it will be loaded from the drive, otherwise from the SqPack. </summary>
    private byte DefaultLoadResource(CiByteString gamePath, SeFileDescriptor* fileDescriptor, int priority,
        bool isSync, ReadOnlySpan<byte> additionalData)
    {
        if (Utf8GamePath.IsRooted(gamePath))
        {
            // Specify that we are loading unpacked files from the drive.
            // We need to obtain the actual file path in UTF16 (Windows-Unicode) on two locations,
            // but we write a pointer to the given string instead and use the CreateFileW hook to handle it,
            // because otherwise we are limited to 260 characters.
            fileDescriptor->FileMode = FileMode.LoadUnpackedResource;

            // Ensure that the file descriptor has its wchar_t array on aligned boundary even if it has to be odd.
            var fd = stackalloc char[0x11 + 0x0B + 14];
            fileDescriptor->FileDescriptor = (byte*)fd + 1;
            CreateFileWHook.WritePtr(fd + 0x11,                      gamePath.Path, gamePath.Length);
            CreateFileWHook.WritePtr(&fileDescriptor->Utf16FileName, gamePath.Path, gamePath.Length);

            // Use the SE ReadFile function.
            var ret = _fileReadService.ReadFile(fileDescriptor, priority, isSync);
            FileLoaded?.Invoke(fileDescriptor->ResourceHandle, gamePath, ret != 0, true, additionalData);
            return ret;
        }
        else
        {
            var ret = _fileReadService.ReadDefaultSqPack(fileDescriptor, priority, isSync);
            FileLoaded?.Invoke(fileDescriptor->ResourceHandle, gamePath, ret != 0, false, additionalData);
            return ret;
        }
    }

    /// <summary>
    /// A resource with ref count 0 that gets incremented goes through GetResourceAsync again.
    /// This means, that if the path determined from that is different than the resources path,
    /// a different resource gets loaded or incremented, while the IncRef'd resource stays at 0.
    /// This causes some problems and is hopefully prevented with this.
    /// </summary>
    private readonly ThreadLocal<bool> _incMode = new(() => false, true);

    /// <inheritdoc cref="_incMode"/>
    private void IncRefProtection(ResourceHandle* handle, ref nint? returnValue)
    {
        if (handle->RefCount != 0)
            return;

        _incMode.Value = true;
        returnValue    = _resources.IncRef(handle);
        _incMode.Value = false;
    }

    /// <summary>
    /// Catch weird errors with invalid decrements of the reference count.
    /// </summary>
    private static void DecRefProtection(ResourceHandle* handle, ref byte? returnValue)
    {
        if (handle->RefCount != 0)
            return;

        try
        {
            Penumbra.Log.Error(
                $"[ResourceLoader] Caught decrease of Reference Counter for {handle->FileName()} at 0x{(ulong)handle} below 0.");
        }
        catch
        {
            // ignored
        }

        returnValue = 1;
    }

    private void ResourceDestructorHandler(ResourceHandle* handle)
    {
        _ongoingLoads.TryRemove((nint)handle, out _);
    }

    /// <summary> Compute the CRC32 hash for a given path together with potential resource parameters. </summary>
    private static int ComputeHash(CiByteString path, GetResourceParameters* pGetResParams)
    {
        if (pGetResParams == null || !pGetResParams->IsPartialRead)
            return path.Crc32;

        // When the game requests file only partially, crc32 includes that information, in format of:
        // path/to/file.ext.hex_offset.hex_size
        // ex) music/ex4/BGM_EX4_System_Title.scd.381adc.30000
        return CiByteString.Join(
            (byte)'.',
            path,
            CiByteString.FromString(pGetResParams->SegmentOffset.ToString("x"), out var s1, MetaDataComputation.None) ? s1 : CiByteString.Empty,
            CiByteString.FromString(pGetResParams->SegmentLength.ToString("x"), out var s2, MetaDataComputation.None) ? s2 : CiByteString.Empty
        ).Crc32;
    }

    /// <summary>
    /// In Debug build, compare the hashes the game computes with those Penumbra computes to notice potential changes in the CRC32 algorithm or resource parameters.
    /// </summary>
    [Conditional("DEBUG")]
    private static void CompareHash(int local, int game, Utf8GamePath path)
    {
        if (local != game)
            Penumbra.Log.Warning($"[ResourceLoader] Hash function appears to have changed. Computed {local:X8} vs Game {game:X8} for {path}.");
    }
}
