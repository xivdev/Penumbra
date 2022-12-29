using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Loader;

public unsafe partial class ResourceLoader : IDisposable
{
    // Toggle whether replacing paths is active, independently of hook and event state.
    public bool DoReplacements { get; private set; }

    // Hooks are required for everything, even events firing.
    public bool HooksEnabled { get; private set; }

    // This Logging just logs all file requests, returns and loads to the Dalamud log.
    // Events can be used to make smarter logging.
    public bool IsLoggingEnabled { get; private set; }

    public void EnableFullLogging()
    {
        if( IsLoggingEnabled )
        {
            return;
        }

        IsLoggingEnabled  =  true;
        ResourceRequested += LogPath;
        ResourceLoaded    += LogResource;
        FileLoaded        += LogLoadedFile;
        ResourceHandleDestructorHook?.Enable();
        EnableHooks();
    }

    public void DisableFullLogging()
    {
        if( !IsLoggingEnabled )
        {
            return;
        }

        IsLoggingEnabled  =  false;
        ResourceRequested -= LogPath;
        ResourceLoaded    -= LogResource;
        FileLoaded        -= LogLoadedFile;
        ResourceHandleDestructorHook?.Disable();
    }

    public void EnableReplacements()
    {
        if( DoReplacements )
        {
            return;
        }

        DoReplacements = true;
        EnableTexMdlTreatment();
        EnableHooks();
    }

    public void DisableReplacements()
    {
        if( !DoReplacements )
        {
            return;
        }

        DoReplacements = false;
        DisableTexMdlTreatment();
    }

    public void EnableHooks()
    {
        if( HooksEnabled )
        {
            return;
        }

        HooksEnabled = true;
        ReadSqPackHook.Enable();
        GetResourceSyncHook.Enable();
        GetResourceAsyncHook.Enable();
        _incRefHook.Enable();
    }

    public void DisableHooks()
    {
        if( !HooksEnabled )
        {
            return;
        }

        HooksEnabled = false;
        ReadSqPackHook.Disable();
        GetResourceSyncHook.Disable();
        GetResourceAsyncHook.Disable();
        _incRefHook.Disable();
    }

    public ResourceLoader( Penumbra _ )
    {
        SignatureHelper.Initialise( this );
        _decRefHook = Hook< ResourceHandleDecRef >.FromAddress(
            ( IntPtr )FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle.fpDecRef,
            ResourceHandleDecRefDetour );
        _incRefHook = Hook< ResourceHandleDestructor >.FromAddress(
            ( IntPtr )FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle.fpIncRef, ResourceHandleIncRefDetour );
    }

    // Event fired whenever a resource is requested.
    public delegate void ResourceRequestedDelegate( Utf8GamePath path, bool synchronous );
    public event ResourceRequestedDelegate? ResourceRequested;

    // Event fired whenever a resource is returned.
    // If the path was manipulated by penumbra, manipulatedPath will be the file path of the loaded resource.
    // resolveData is additional data returned by the current ResolvePath function which can contain the collection and associated game object.
    public delegate void ResourceLoadedDelegate( ResourceHandle* handle, Utf8GamePath originalPath, FullPath? manipulatedPath,
        ResolveData resolveData );

    public event ResourceLoadedDelegate? ResourceLoaded;


    // Event fired whenever a resource is newly loaded.
    // Success indicates the return value of the loading function (which does not imply that the resource was actually successfully loaded)
    // custom is true if the file was loaded from local files instead of the default SqPacks.
    public delegate void FileLoadedDelegate( ResourceHandle* resource, ByteString path, bool success, bool custom );
    public event FileLoadedDelegate? FileLoaded;

    // Customization point to control how path resolving is handled.
    // Resolving goes through all subscribed functions in arbitrary order until one returns true,
    // or uses default resolving if none return true.
    public delegate bool ResolvePathDelegate( Utf8GamePath path, ResourceCategory category, ResourceType type, int hash,
        out (FullPath?, ResolveData) ret );

    public event ResolvePathDelegate? ResolvePathCustomization;

    // Customize file loading for any GamePaths that start with "|".
    // Same procedure as above.
    public delegate bool ResourceLoadCustomizationDelegate( ByteString split, ByteString path, ResourceManager* resourceManager,
        SeFileDescriptor* fileDescriptor, int priority, bool isSync, out byte retValue );

    public event ResourceLoadCustomizationDelegate? ResourceLoadCustomization;

    public void Dispose()
    {
        DisableFullLogging();
        DisposeHooks();
        DisposeTexMdlTreatment();
    }
}