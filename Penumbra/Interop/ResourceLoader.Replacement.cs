using System;
using System.Diagnostics;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.GameData.ByteString;
using Penumbra.Interop.Structs;
using FileMode = Penumbra.Interop.Structs.FileMode;
using ResourceHandle = FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle;

namespace Penumbra.Interop;

public unsafe partial class ResourceLoader
{
    // Resources can be obtained synchronously and asynchronously. We need to change behaviour in both cases.
    // Both work basically the same, so we can reduce the main work to one function used by both hooks.
    public delegate ResourceHandle* GetResourceSyncPrototype( ResourceManager* resourceManager, ResourceCategory* pCategoryId,
        uint* pResourceType, int* pResourceHash, byte* pPath, void* pUnknown );

    [Signature( "E8 ?? ?? 00 00 48 8D 8F ?? ?? 00 00 48 89 87 ?? ?? 00 00", DetourName = "GetResourceSyncDetour" )]
    public Hook< GetResourceSyncPrototype > GetResourceSyncHook = null!;

    public delegate ResourceHandle* GetResourceAsyncPrototype( ResourceManager* resourceManager, ResourceCategory* pCategoryId,
        uint* pResourceType, int* pResourceHash, byte* pPath, void* pUnknown, bool isUnknown );

    [Signature( "E8 ?? ?? ?? 00 48 8B D8 EB ?? F0 FF 83 ?? ?? 00 00", DetourName = "GetResourceAsyncDetour" )]
    public Hook< GetResourceAsyncPrototype > GetResourceAsyncHook = null!;

    private ResourceHandle* GetResourceSyncDetour( ResourceManager* resourceManager, ResourceCategory* categoryId, uint* resourceType,
        int* resourceHash, byte* path, void* unk )
        => GetResourceHandler( true, resourceManager, categoryId, resourceType, resourceHash, path, unk, false );

    private ResourceHandle* GetResourceAsyncDetour( ResourceManager* resourceManager, ResourceCategory* categoryId, uint* resourceType,
        int* resourceHash, byte* path, void* unk, bool isUnk )
        => GetResourceHandler( false, resourceManager, categoryId, resourceType, resourceHash, path, unk, isUnk );

    private ResourceHandle* CallOriginalHandler( bool isSync, ResourceManager* resourceManager, ResourceCategory* categoryId,
        uint* resourceType, int* resourceHash, byte* path, void* unk, bool isUnk )
        => isSync
            ? GetResourceSyncHook.Original( resourceManager, categoryId, resourceType, resourceHash, path, unk )
            : GetResourceAsyncHook.Original( resourceManager, categoryId, resourceType, resourceHash, path, unk, isUnk );


    [Conditional( "DEBUG" )]
    private static void CompareHash( int local, int game, Utf8GamePath path )
    {
        if( local != game )
        {
            PluginLog.Warning( "Hash function appears to have changed. {Hash1:X8} vs {Hash2:X8} for {Path}.", game, local, path );
        }
    }

    private event Action< Utf8GamePath, FullPath?, object? >? PathResolved;

    private ResourceHandle* GetResourceHandler( bool isSync, ResourceManager* resourceManager, ResourceCategory* categoryId, uint* resourceType,
        int* resourceHash, byte* path, void* unk, bool isUnk )
    {
        if( !Utf8GamePath.FromPointer( path, out var gamePath ) )
        {
            PluginLog.Error( "Could not create GamePath from resource path." );
            return CallOriginalHandler( isSync, resourceManager, categoryId, resourceType, resourceHash, path, unk, isUnk );
        }

        CompareHash( gamePath.Path.Crc32, *resourceHash, gamePath );

        ResourceRequested?.Invoke( gamePath, isSync );

        // If no replacements are being made, we still want to be able to trigger the event.
        var (resolvedPath, data) = DoReplacements ? ResolvePath( gamePath.ToLower() ) : ( null, null );
        PathResolved?.Invoke( gamePath, resolvedPath, data );
        if( resolvedPath == null )
        {
            var retUnmodified = CallOriginalHandler( isSync, resourceManager, categoryId, resourceType, resourceHash, path, unk, isUnk );
            ResourceLoaded?.Invoke( retUnmodified, gamePath, null, data );
            return retUnmodified;
        }

        // Replace the hash and path with the correct one for the replacement.
        *resourceHash = resolvedPath.Value.InternalName.Crc32;
        path          = resolvedPath.Value.InternalName.Path;
        var retModified = CallOriginalHandler( isSync, resourceManager, categoryId, resourceType, resourceHash, path, unk, isUnk );
        ResourceLoaded?.Invoke( retModified, gamePath, resolvedPath.Value, data );
        return retModified;
    }


    // We need to use the ReadFile function to load local, uncompressed files instead of loading them from the SqPacks.
    public delegate byte ReadFileDelegate( ResourceManager* resourceManager, SeFileDescriptor* fileDescriptor, int priority,
        bool isSync );

    [Signature( "E8 ?? ?? ?? ?? 84 C0 0F 84 ?? 00 00 00 4C 8B C3 BA 05" )]
    public ReadFileDelegate ReadFile = null!;

    // We hook ReadSqPack to redirect rooted files to ReadFile.
    public delegate byte ReadSqPackPrototype( ResourceManager* resourceManager, SeFileDescriptor* pFileDesc, int priority, bool isSync );

    [Signature( "E8 ?? ?? ?? ?? EB 05 E8 ?? ?? ?? ?? 84 C0 0F 84 ?? 00 00 00 4C 8B C3", DetourName = "ReadSqPackDetour" )]
    public Hook< ReadSqPackPrototype > ReadSqPackHook = null!;

    private byte ReadSqPackDetour( ResourceManager* resourceManager, SeFileDescriptor* fileDescriptor, int priority, bool isSync )
    {
        if( !DoReplacements )
        {
            return ReadSqPackHook.Original( resourceManager, fileDescriptor, priority, isSync );
        }

        if( fileDescriptor == null || fileDescriptor->ResourceHandle == null )
        {
            PluginLog.Error( "Failure to load file from SqPack: invalid File Descriptor." );
            return ReadSqPackHook.Original( resourceManager, fileDescriptor, priority, isSync );
        }

        var  valid = Utf8GamePath.FromSpan( fileDescriptor->ResourceHandle->FileNameSpan(), out var gamePath, false );
        byte ret;
        // The internal buffer size does not allow for more than 260 characters.
        // We use the IsRooted check to signify paths replaced by us pointing to the local filesystem instead of an SqPack.
        if( !valid || !gamePath.IsRooted() )
        {
            if( valid && ResourceLoadCustomization != null && gamePath.Path[ 0 ] == ( byte )'|' )
            {
                ret = ResourceLoadCustomization.Invoke( gamePath, resourceManager, fileDescriptor, priority, isSync );
            }
            else
            {
                ret = ReadSqPackHook.Original( resourceManager, fileDescriptor, priority, isSync );
                FileLoaded?.Invoke( gamePath.Path, ret != 0, false );
            }
        }
        else
        {
            // Specify that we are loading unpacked files from the drive.
            // We need to copy the actual file path in UTF16 (Windows-Unicode) on two locations,
            // but since we only allow ASCII in the game paths, this is just a matter of upcasting.
            fileDescriptor->FileMode = FileMode.LoadUnpackedResource;

            var fd = stackalloc byte[0x20 + 2 * gamePath.Length + 0x16];
            fileDescriptor->FileDescriptor = fd;
            var fdPtr = ( char* )( fd + 0x21 );
            for( var i = 0; i < gamePath.Length; ++i )
            {
                ( &fileDescriptor->Utf16FileName )[ i ] = ( char )gamePath.Path[ i ];
                fdPtr[ i ]                              = ( char )gamePath.Path[ i ];
            }

            ( &fileDescriptor->Utf16FileName )[ gamePath.Length ] = '\0';
            fdPtr[ gamePath.Length ]                              = '\0';

            // Use the SE ReadFile function.
            ret = ReadFile( resourceManager, fileDescriptor, priority, isSync );
            FileLoaded?.Invoke( gamePath.Path, ret != 0, true );
        }

        return ret;
    }

    // Customize file loading for any GamePaths that start with "|".
    public delegate byte ResourceLoadCustomizationDelegate( Utf8GamePath gamePath, ResourceManager* resourceManager,
        SeFileDescriptor* fileDescriptor, int priority, bool isSync );

    public ResourceLoadCustomizationDelegate? ResourceLoadCustomization;


    // Use the default method of path replacement.
    public static (FullPath?, object?) DefaultReplacer( Utf8GamePath path )
    {
        var resolved = Penumbra.ModManager.ResolveSwappedOrReplacementPath( path );
        return ( resolved, null );
    }

    private void DisposeHooks()
    {
        DisableHooks();
        ReadSqPackHook.Dispose();
        GetResourceSyncHook.Dispose();
        GetResourceAsyncHook.Dispose();
    }
}