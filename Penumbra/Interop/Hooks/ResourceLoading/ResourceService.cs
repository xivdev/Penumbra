using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Api.Enums;
using Penumbra.GameData;
using Penumbra.Interop.SafeHandles;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using CSResourceHandle = FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public unsafe class ResourceService : IDisposable, Luna.IRequiredService
{
    private readonly ResourceManagerService _resourceManager;

    private readonly ThreadLocal<Utf8GamePath> _currentGetResourcePath = new(() => Utf8GamePath.Empty);

    public ResourceService(ResourceManagerService resourceManager, IGameInteropProvider interop)
    {
        _resourceManager = resourceManager;
        interop.InitializeFromAttributes(this);
        _incRefHook = interop.HookFromAddress<ResourceHandlePrototype>(
            (nint)CSResourceHandle.MemberFunctionPointers.IncRef,
            ResourceHandleIncRefDetour);
        _decRefHook = interop.HookFromAddress<ResourceHandleDecRefPrototype>(
            (nint)CSResourceHandle.MemberFunctionPointers.DecRef,
            ResourceHandleDecRefDetour);
        if (!HookOverrides.Instance.ResourceLoading.GetResourceSync)
            _getResourceSyncHook.Enable();
        if (!HookOverrides.Instance.ResourceLoading.GetResourceAsync)
            _getResourceAsyncHook.Enable();
        if (!HookOverrides.Instance.ResourceLoading.UpdateResourceState)
            _updateResourceStateHook.Enable();
        if (!HookOverrides.Instance.ResourceLoading.IncRef)
            _incRefHook.Enable();
        if (!HookOverrides.Instance.ResourceLoading.DecRef)
            _decRefHook.Enable();
    }

    public ResourceHandle* GetResource(ResourceCategory category, ResourceType type, CiByteString path)
    {
        var hash = path.Crc32;
        return GetResourceHandler(true, (ResourceManager*)_resourceManager.ResourceManagerAddress,
            &category,                  &type, &hash, path.Path, null, 0, 0, 0);
    }

    public SafeResourceHandle GetSafeResource(ResourceCategory category, ResourceType type, CiByteString path)
        => new((CSResourceHandle*)GetResource(category, type, path), false);

    public void Dispose()
    {
        _getResourceSyncHook.Dispose();
        _getResourceAsyncHook.Dispose();
        _updateResourceStateHook.Dispose();
        _incRefHook.Dispose();
        _decRefHook.Dispose();
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
        ResourceType* pResourceType, int* pResourceHash, byte* pPath, GetResourceParameters* pGetResParams, nint unk7, uint unk8);

    private delegate ResourceHandle* GetResourceAsyncPrototype(ResourceManager* resourceManager, ResourceCategory* pCategoryId,
        ResourceType* pResourceType, int* pResourceHash, byte* pPath, GetResourceParameters* pGetResParams, byte isUnknown, nint unk8,
        uint unk9);

    [Signature(Sigs.GetResourceSync, DetourName = nameof(GetResourceSyncDetour))]
    private readonly Hook<GetResourceSyncPrototype> _getResourceSyncHook = null!;

    [Signature(Sigs.GetResourceAsync, DetourName = nameof(GetResourceAsyncDetour))]
    private readonly Hook<GetResourceAsyncPrototype> _getResourceAsyncHook = null!;

    private ResourceHandle* GetResourceSyncDetour(ResourceManager* resourceManager, ResourceCategory* categoryId, ResourceType* resourceType,
        int* resourceHash, byte* path, GetResourceParameters* pGetResParams, nint unk8, uint unk9)
        => GetResourceHandler(true, resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, 0, unk8, unk9);

    private ResourceHandle* GetResourceAsyncDetour(ResourceManager* resourceManager, ResourceCategory* categoryId, ResourceType* resourceType,
        int* resourceHash, byte* path, GetResourceParameters* pGetResParams, byte isUnk, nint unk8, uint unk9)
        => GetResourceHandler(false, resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, isUnk, unk8, unk9);

    /// <summary>
    /// Resources can be obtained synchronously and asynchronously. We need to change behaviour in both cases.
    /// Both work basically the same, so we can reduce the main work to one function used by both hooks.
    /// </summary>
    private ResourceHandle* GetResourceHandler(bool isSync, ResourceManager* resourceManager, ResourceCategory* categoryId,
        ResourceType* resourceType, int* resourceHash, byte* path, GetResourceParameters* pGetResParams, byte isUnk, nint unk8, uint unk9)
    {
        if (!Utf8GamePath.FromPointer(path, MetaDataComputation.CiCrc32, out var gamePath))
        {
            Penumbra.Log.Error("[ResourceService] Could not create GamePath from resource path.");
            return isSync
                ? _getResourceSyncHook.Original(resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, unk8, unk9)
                : _getResourceAsyncHook.Original(resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, isUnk, unk8,
                    unk9);
        }

        if (gamePath.IsEmpty)
        {
            Penumbra.Log.Error($"[ResourceService] Empty resource path requested with category {*categoryId}, type {*resourceType}, hash {*resourceHash}.");
            return null;
        }

        var             original    = gamePath;
        ResourceHandle* returnValue = null;
        ResourceRequested?.Invoke(ref *categoryId, ref *resourceType, ref *resourceHash, ref gamePath, original, pGetResParams, ref isSync,
            ref returnValue);
        if (returnValue != null)
            return returnValue;

        return GetOriginalResource(isSync, *categoryId, *resourceType, *resourceHash, gamePath.Path, original, pGetResParams, isUnk, unk8,
            unk9);
    }

    /// <summary> Call the original GetResource function. </summary>
    public ResourceHandle* GetOriginalResource(bool sync, ResourceCategory categoryId, ResourceType type, int hash, CiByteString path,
        Utf8GamePath original,
        GetResourceParameters* resourceParameters = null, byte unk = 0, nint unk8 = 0, uint unk9 = 0)
    {
        var previous = _currentGetResourcePath.Value;
        try
        {
            _currentGetResourcePath.Value = original;
            return sync
                ? _getResourceSyncHook.OriginalDisposeSafe(_resourceManager.ResourceManager, &categoryId, &type, &hash, path.Path,
                    resourceParameters, unk8, unk9)
                : _getResourceAsyncHook.OriginalDisposeSafe(_resourceManager.ResourceManager, &categoryId, &type, &hash, path.Path,
                    resourceParameters, unk, unk8, unk9);
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

    private delegate uint UpdateResourceStatePrototype(ResourceHandle* handle, byte offFileThread);

    [Signature(Sigs.UpdateResourceState, DetourName = nameof(UpdateResourceStateDetour))]
    private readonly Hook<UpdateResourceStatePrototype> _updateResourceStateHook = null!;

    private uint UpdateResourceStateDetour(ResourceHandle* handle, byte offFileThread)
    {
        var previousState = (handle->UnkState, handle->LoadState);
        var syncOriginal  = _currentGetResourcePath.IsValueCreated ? _currentGetResourcePath.Value : Utf8GamePath.Empty;
        ResourceStateUpdating?.Invoke(handle, syncOriginal);
        var ret = _updateResourceStateHook.OriginalDisposeSafe(handle, offFileThread);
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
        => _incRefHook.OriginalDisposeSafe(handle);

    private readonly Hook<ResourceHandlePrototype> _incRefHook;

    private nint ResourceHandleIncRefDetour(ResourceHandle* handle)
    {
        nint? ret = null;
        ResourceHandleIncRef?.Invoke(handle, ref ret);
        return ret ?? _incRefHook.OriginalDisposeSafe(handle);
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
        => _decRefHook.OriginalDisposeSafe(handle);

    private delegate byte                                ResourceHandleDecRefPrototype(ResourceHandle* handle);
    private readonly Hook<ResourceHandleDecRefPrototype> _decRefHook;

    private byte ResourceHandleDecRefDetour(ResourceHandle* handle)
    {
        byte? ret = null;
        ResourceHandleDecRef?.Invoke(handle, ref ret);
        return ret ?? _decRefHook.OriginalDisposeSafe(handle);
    }

    #endregion
}
