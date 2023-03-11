using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.String;
using ResourceHandle = FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle;

namespace Penumbra.Interop.Loader;

public unsafe class ResourceHook : IDisposable
{
    public ResourceHook()
    {
        SignatureHelper.Initialise(this);
        _getResourceSyncHook.Enable();
        _getResourceAsyncHook.Enable();
        _resourceHandleDestructorHook.Enable();
    }

    public void Dispose()
    {
        _getResourceSyncHook.Dispose();
        _getResourceAsyncHook.Dispose();
    }

    #region GetResource

    /// <summary> Called before a resource is requested. </summary>
    /// <param name="category">The resource category. Should not generally be changed.</param>
    /// <param name="type">The resource type. Should not generally be changed.</param>
    /// <param name="hash">The resource hash. Should generally fit to the path.</param>
    /// <param name="path">The path of the requested resource.</param>
    /// <param name="parameters">Mainly used for SCD streaming.</param>
    /// <param name="sync">Whether to request the resource synchronously or asynchronously.</param>
    public delegate void GetResourcePreDelegate(ref ResourceCategory category, ref ResourceType type, ref int hash, ref ByteString path,
        ref GetResourceParameters parameters, ref bool sync);

    /// <summary> <inheritdoc cref="GetResourcePreDelegate"/> <para/>
    /// Subscribers should be exception-safe.</summary>
    public event GetResourcePreDelegate? GetResourcePre;

    /// <summary>
    /// The returned resource handle obtained from a resource request. Contains all the other information from the request.
    /// </summary>
    public delegate void GetResourcePostDelegate(ref ResourceHandle handle);

    /// <summary> <inheritdoc cref="GetResourcePostDelegate"/> <para/>
    /// Subscribers should be exception-safe.</summary>
    public event GetResourcePostDelegate? GetResourcePost;


    private delegate ResourceHandle* GetResourceSyncPrototype(ResourceManager* resourceManager, ResourceCategory* pCategoryId,
        ResourceType* pResourceType, int* pResourceHash, byte* pPath, GetResourceParameters* pGetResParams);

    private delegate ResourceHandle* GetResourceAsyncPrototype(ResourceManager* resourceManager, ResourceCategory* pCategoryId,
        ResourceType* pResourceType, int* pResourceHash, byte* pPath, GetResourceParameters* pGetResParams, bool isUnknown);

    [Signature(Sigs.GetResourceSync, DetourName = nameof(GetResourceSyncDetour))]
    private readonly Hook<GetResourceSyncPrototype> _getResourceSyncHook = null!;

    [Signature(Sigs.GetResourceAsync, DetourName = nameof(GetResourceAsyncDetour))]
    private readonly Hook<GetResourceAsyncPrototype> _getResourceAsyncHook = null!;

    private ResourceHandle* GetResourceSyncDetour(ResourceManager* resourceManager, ResourceCategory* categoryId, ResourceType* resourceType,
        int* resourceHash, byte* path, GetResourceParameters* pGetResParams)
        => GetResourceHandler(true, resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, false);

    private ResourceHandle* GetResourceAsyncDetour(ResourceManager* resourceManager, ResourceCategory* categoryId, ResourceType* resourceType,
        int* resourceHash, byte* path, GetResourceParameters* pGetResParams, bool isUnk)
        => GetResourceHandler(false, resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, isUnk);

    /// <summary>
    /// Resources can be obtained synchronously and asynchronously. We need to change behaviour in both cases.
    /// Both work basically the same, so we can reduce the main work to one function used by both hooks.
    /// </summary>
    private ResourceHandle* GetResourceHandler(bool isSync, ResourceManager* resourceManager, ResourceCategory* categoryId,
        ResourceType* resourceType, int* resourceHash, byte* path, GetResourceParameters* pGetResParams, bool isUnk)
    {
        var byteString = new ByteString(path);
        GetResourcePre?.Invoke(ref *categoryId, ref *resourceType, ref *resourceHash, ref byteString, ref *pGetResParams, ref isSync);
        var ret = isSync
            ? _getResourceSyncHook.Original(resourceManager, categoryId, resourceType, resourceHash, byteString.Path, pGetResParams)
            : _getResourceAsyncHook.Original(resourceManager, categoryId, resourceType, resourceHash, byteString.Path, pGetResParams, isUnk);
        GetResourcePost?.Invoke(ref *ret);
        return ret;
    }

    #endregion

    private delegate IntPtr ResourceHandlePrototype(ResourceHandle* handle);

    #region IncRef

    /// <summary> Invoked before a resource handle reference count is incremented. </summary>
    /// <param name="handle">The resource handle.</param>
    /// <param name="callOriginal">Whether to call original after the event has run.</param>
    /// <param name="returnValue">The return value to use if not calling original.</param>
    public delegate void ResourceHandleIncRefDelegate(ref ResourceHandle handle, ref bool callOriginal, ref nint returnValue);

    /// <summary>
    /// <inheritdoc cref="ResourceHandleIncRefDelegate"/> <para/>
    /// Subscribers should be exception-safe.
    /// </summary>
    public event ResourceHandleIncRefDelegate? ResourceHandleIncRef;

    public nint IncRef(ref ResourceHandle handle)
    {
        fixed (ResourceHandle* ptr = &handle)
        {
            return _incRefHook.Original(ptr);
        }
    }

    private readonly Hook<ResourceHandlePrototype> _incRefHook;
    private nint ResourceHandleIncRefDetour(ResourceHandle* handle)
    {
        var callOriginal = true;
        var ret          = IntPtr.Zero;
        ResourceHandleIncRef?.Invoke(ref *handle, ref callOriginal, ref ret);
        return callOriginal ? _incRefHook.Original(handle) : ret;
    }

    #endregion

    #region DecRef

    /// <summary> Invoked before a resource handle reference count is decremented. </summary>
    /// <param name="handle">The resource handle.</param>
    /// <param name="callOriginal">Whether to call original after the event has run.</param>
    /// <param name="returnValue">The return value to use if not calling original.</param>
    public delegate void ResourceHandleDecRefDelegate(ref ResourceHandle handle, ref bool callOriginal, ref byte returnValue);

    /// <summary>
    /// <inheritdoc cref="ResourceHandleDecRefDelegate"/> <para/>
    /// Subscribers should be exception-safe.
    /// </summary>
    public event ResourceHandleDecRefDelegate? ResourceHandleDecRef;

    public byte DecRef(ref ResourceHandle handle)
    {
        fixed (ResourceHandle* ptr = &handle)
        {
            return _incRefHook.Original(ptr);
        }
    }

    private delegate byte                                ResourceHandleDecRefPrototype(ResourceHandle* handle);
    private readonly Hook<ResourceHandleDecRefPrototype> _decRefHook;
    private byte ResourceHandleDecRefDetour(ResourceHandle* handle)
    {
        var callOriginal = true;
        var ret          = byte.MinValue;
        ResourceHandleDecRef?.Invoke(ref *handle, ref callOriginal, ref ret);
        return callOriginal ? _decRefHook!.Original(handle) : ret;
    }

    #endregion

    /// <summary> Invoked before a resource handle is destructed. </summary>
    /// <param name="handle">The resource handle.</param>
    public delegate void ResourceHandleDtorDelegate(ref ResourceHandle handle);

    /// <summary>
    /// <inheritdoc cref="ResourceHandleDtorDelegate"/> <para/>
    /// Subscribers should be exception-safe.
    /// </summary>
    public event ResourceHandleDtorDelegate? ResourceHandleDestructor;

    [Signature(Sigs.ResourceHandleDestructor, DetourName = nameof(ResourceHandleDestructorDetour))]
    private readonly Hook<ResourceHandlePrototype> _resourceHandleDestructorHook = null!;

    private nint ResourceHandleDestructorDetour(ResourceHandle* handle)
    {
        ResourceHandleDestructor?.Invoke(ref *handle);
        return _resourceHandleDestructorHook!.Original(handle);
    }

    #endregion
}
