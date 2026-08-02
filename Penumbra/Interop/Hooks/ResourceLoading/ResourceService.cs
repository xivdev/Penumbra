using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.GameData;
using Penumbra.Interop.SafeHandles;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using CSResourceHandle = FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public sealed unsafe class ResourceService : IDisposable, IRequiredService
{
    private readonly HookManager               _hooks;
    private readonly ResourceManagerService    _resourceManager;
    private readonly ThreadLocal<Utf8GamePath> _currentGetResourcePath = new(() => Utf8GamePath.Empty);

    public ResourceService(ResourceManagerService resourceManager, HookManager hooks)
    {
        _resourceManager = resourceManager;
        _hooks           = hooks;
        _incRefHook = _hooks.CreateHook<ResourceHandlePrototype>("ResourceHandle.IncRef", (nint)CSResourceHandle.MemberFunctionPointers.IncRef,
            ResourceHandleIncRefDetour, !HookOverrides.Instance.ResourceLoading.IncRef)!;
        _decRefHook = _hooks.CreateHook<ResourceHandleDecRefPrototype>("ResourceHandle.DecRef",
            (nint)CSResourceHandle.MemberFunctionPointers.DecRef, ResourceHandleDecRefDetour, !HookOverrides.Instance.ResourceLoading.DecRef)!;
        _getResourceSyncHook = _hooks.CreateHook<GetResourceSyncPrototype>("GetResourceSync", Sigs.GetResourceSync, GetResourceSyncDetour,
            !HookOverrides.Instance.ResourceLoading.GetResourceSync)!;
        _getResourceAsyncHook = _hooks.CreateHook<GetResourceAsyncPrototype>("GetResourceAsync", Sigs.GetResourceAsync, GetResourceAsyncDetour,
            !HookOverrides.Instance.ResourceLoading.GetResourceAsync)!;
        _updateResourceStateHook = _hooks.CreateHook<UpdateResourceStatePrototype>("UpdateResourceState", Sigs.UpdateResourceState,
            UpdateResourceStateDetour, !HookOverrides.Instance.ResourceLoading.UpdateResourceState)!;
    }

    public ResourceHandle* GetResource(ResourceCategory category, ResourceType type, CiByteString path)
    {
        var hash = path.Crc32;
        return GetResourceHandler(true, ResourceManager.Instance(),
            &category,                  &type, &hash, path.Path, null, 0, null, 0);
    }

    public SafeResourceHandle GetSafeResource(ResourceCategory category, ResourceType type, CiByteString path)
        => new((CSResourceHandle*)GetResource(category, type, path), false);

    public void Dispose()
    {
        _hooks.DisposeHook("ResourceHandle.IncRef");
        _hooks.DisposeHook("ResourceHandle.DecRef");
        _hooks.DisposeHook("GetResourceSync");
        _hooks.DisposeHook("GetResourceAsync");
        _hooks.DisposeHook("UpdateResourceState");
        _currentGetResourcePath.Dispose();
    }

    #region GetResource

    /// <summary> Called before a resource is requested. </summary>
    /// <param name="category">The resource category. Should not generally be changed.</param>
    /// <param name="type">The resource type. Should not generally be changed.</param>
    /// <param name="hash">The resource hash. Should generally fit to the path.</param>
    /// <param name="path">The path of the requested resource.</param>
    /// <param name="parameters">Mainly used for SCD streaming, can be null.</param>
    /// <param name="sync">Whether to request the resource synchronously or asynchronously.</param>
    /// <param name="returnValue">The returned resource handle. If this is not null, calling original will be skipped. </param>
    public delegate void GetResourcePreDelegate(ref ResourceCategory category, ref ResourceType type, ref int hash, ref Utf8GamePath path,
        Utf8GamePath original, GetResourceParameters* parameters, ref bool sync, ref ResourceHandle* returnValue);

    /// <summary> <inheritdoc cref="GetResourcePreDelegate"/> <para/>
    /// Subscribers should be exception-safe.</summary>
    public event GetResourcePreDelegate? ResourceRequested;

    private delegate ResourceHandle* GetResourceSyncPrototype(ResourceManager* resourceManager, ResourceCategory* pCategoryId,
        ResourceType* pResourceType, int* pResourceHash, byte* pPath, GetResourceParameters* pGetResParams, byte* file, uint line);

    private delegate ResourceHandle* GetResourceAsyncPrototype(ResourceManager* resourceManager, ResourceCategory* pCategoryId,
        ResourceType* pResourceType, int* pResourceHash, byte* pPath, GetResourceParameters* pGetResParams, byte hasHandleLock, byte* file,
        uint line);

    private readonly Task<Hook<GetResourceSyncPrototype>>  _getResourceSyncHook;
    private readonly Task<Hook<GetResourceAsyncPrototype>> _getResourceAsyncHook;

    private ResourceHandle* GetResourceSyncDetour(ResourceManager* resourceManager, ResourceCategory* categoryId, ResourceType* resourceType,
        int* resourceHash, byte* path, GetResourceParameters* pGetResParams, byte* file, uint line)
        => GetResourceHandler(true, resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, 0, file, line);

    private ResourceHandle* GetResourceAsyncDetour(ResourceManager* resourceManager, ResourceCategory* categoryId, ResourceType* resourceType,
        int* resourceHash, byte* path, GetResourceParameters* pGetResParams, byte hasHandleLock, byte* file, uint line)
        => GetResourceHandler(false, resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, hasHandleLock, file, line);

    /// <summary>
    /// Resources can be obtained synchronously and asynchronously. We need to change behaviour in both cases.
    /// Both work basically the same, so we can reduce the main work to one function used by both hooks.
    /// </summary>
    private ResourceHandle* GetResourceHandler(bool isSync, ResourceManager* resourceManager, ResourceCategory* categoryId,
        ResourceType* resourceType, int* resourceHash, byte* path, GetResourceParameters* pGetResParams, byte hasHandleLock, byte* file,
        uint line)
    {
        if (!Utf8GamePath.FromPointer(path, MetaDataComputation.CiCrc32, out var gamePath))
        {
            Penumbra.Log.Error("[ResourceService] Could not create GamePath from resource path.");
            return isSync
                ? _getResourceSyncHook.Result.Original(resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, file, line)
                : _getResourceAsyncHook.Result.Original(resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams,
                    hasHandleLock,
                    file,
                    line);
        }

        if (gamePath.IsEmpty)
        {
            Penumbra.Log.Error(
                $"[ResourceService] Empty resource path requested with category {*categoryId}, type {*resourceType}, hash {*resourceHash}.");
            return null;
        }

        var             original    = gamePath;
        ResourceHandle* returnValue = null;
        ResourceRequested?.Invoke(ref *categoryId, ref *resourceType, ref *resourceHash, ref gamePath, original, pGetResParams, ref isSync,
            ref returnValue);
        if (returnValue != null)
            return returnValue;

        return GetOriginalResource(isSync, *categoryId, *resourceType, *resourceHash, gamePath.Path, original, pGetResParams, hasHandleLock,
            file,
            line);
    }

    /// <summary> Call the original GetResource function. </summary>
    public ResourceHandle* GetOriginalResource(bool sync, ResourceCategory categoryId, ResourceType type, int hash, CiByteString path,
        Utf8GamePath original, GetResourceParameters* resourceParameters = null, byte hasHandleLock = 0, byte* file = null, uint line = 0)
    {
        var previous = _currentGetResourcePath.Value;
        try
        {
            _currentGetResourcePath.Value = original;
            return sync
                ? _getResourceSyncHook.Result.OriginalDisposeSafe(_resourceManager.ResourceManager, &categoryId, &type, &hash, path.Path,
                    resourceParameters, file, line)
                : _getResourceAsyncHook.Result.OriginalDisposeSafe(_resourceManager.ResourceManager, &categoryId, &type, &hash, path.Path,
                    resourceParameters, hasHandleLock, file, line);
        }
        finally
        {
            _currentGetResourcePath.Value = previous;
        }
    }

    #endregion

    private delegate nint ResourceHandlePrototype(ResourceHandle* handle);

    #region UpdateResourceState

    /// <summary> Invoked before a resource state is updated. </summary>
    /// <param name="handle">The resource handle.</param>
    /// <param name="syncOriginal">The original game path of the resource, if loaded synchronously.</param>
    public delegate void ResourceStateUpdatingDelegate(ResourceHandle* handle, Utf8GamePath syncOriginal);

    /// <summary> Invoked after a resource state is updated. </summary>
    /// <param name="handle">The resource handle.</param>
    /// <param name="syncOriginal">The original game path of the resource, if loaded synchronously.</param>
    /// <param name="previousState">The previous state of the resource.</param>
    /// <param name="returnValue">The return value to use.</param>
    public delegate void ResourceStateUpdatedDelegate(ResourceHandle* handle, Utf8GamePath syncOriginal,
        (byte UnkState, LoadState LoadState) previousState, ref uint returnValue);

    /// <summary>
    /// <inheritdoc cref="ResourceStateUpdatingDelegate"/> <para/>
    /// Subscribers should be exception-safe.
    /// </summary>
    public event ResourceStateUpdatingDelegate? ResourceStateUpdating;

    /// <summary>
    /// <inheritdoc cref="ResourceStateUpdatedDelegate"/> <para/>
    /// Subscribers should be exception-safe.
    /// </summary>
    public event ResourceStateUpdatedDelegate? ResourceStateUpdated;

    private delegate uint                                     UpdateResourceStatePrototype(ResourceHandle* handle, byte offFileThread);
    private readonly Task<Hook<UpdateResourceStatePrototype>> _updateResourceStateHook;

    private uint UpdateResourceStateDetour(ResourceHandle* handle, byte offFileThread)
    {
        var previousState = (handle->UnkState, handle->LoadState);
        var syncOriginal  = _currentGetResourcePath.IsValueCreated ? _currentGetResourcePath.Value : Utf8GamePath.Empty;
        ResourceStateUpdating?.Invoke(handle, syncOriginal);
        var ret = _updateResourceStateHook.Result.OriginalDisposeSafe(handle, offFileThread);
        ResourceStateUpdated?.Invoke(handle, syncOriginal, previousState, ref ret);
        return ret;
    }

    #endregion

    #region IncRef

    /// <summary> Invoked before a resource handle reference count is incremented. </summary>
    /// <param name="handle">The resource handle.</param>
    /// <param name="returnValue">The return value to use, setting this value will skip calling original.</param>
    public delegate void ResourceHandleIncRefDelegate(ResourceHandle* handle, ref nint? returnValue);

    /// <summary>
    /// <inheritdoc cref="ResourceHandleIncRefDelegate"/> <para/>
    /// Subscribers should be exception-safe.
    /// </summary>
    public event ResourceHandleIncRefDelegate? ResourceHandleIncRef;

    /// <summary>
    /// Call the game function that increases the reference counter of a resource handle.
    /// </summary>
    public nint IncRef(ResourceHandle* handle)
        => _incRefHook.Result.OriginalDisposeSafe(handle);

    private readonly Task<Hook<ResourceHandlePrototype>> _incRefHook;

    private nint ResourceHandleIncRefDetour(ResourceHandle* handle)
    {
        nint? ret = null;
        ResourceHandleIncRef?.Invoke(handle, ref ret);
        return ret ?? _incRefHook.Result.OriginalDisposeSafe(handle);
    }

    #endregion

    #region DecRef

    /// <summary> Invoked before a resource handle reference count is decremented. </summary>
    /// <param name="handle">The resource handle.</param>
    /// <param name="returnValue">The return value to use, setting this value will skip calling original.</param>
    public delegate void ResourceHandleDecRefDelegate(ResourceHandle* handle, ref byte? returnValue);

    /// <summary>
    /// <inheritdoc cref="ResourceHandleDecRefDelegate"/> <para/>
    /// Subscribers should be exception-safe.
    /// </summary>
    public event ResourceHandleDecRefDelegate? ResourceHandleDecRef;

    /// <summary>
    /// Call the original game function that decreases the reference counter of a resource handle.
    /// </summary>
    public byte DecRef(ResourceHandle* handle)
        => _decRefHook.Result.OriginalDisposeSafe(handle);

    private delegate byte                                      ResourceHandleDecRefPrototype(ResourceHandle* handle);
    private readonly Task<Hook<ResourceHandleDecRefPrototype>> _decRefHook;

    private byte ResourceHandleDecRefDetour(ResourceHandle* handle)
    {
        byte? ret = null;
        ResourceHandleDecRef?.Invoke(handle, ref ret);
        return ret ?? _decRefHook.Result.OriginalDisposeSafe(handle);
    }

    #endregion
}
