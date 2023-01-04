using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.Util;
using FileMode = Penumbra.Interop.Structs.FileMode;
using ResourceHandle = FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle;

namespace Penumbra.Interop.Loader;

public unsafe partial class ResourceLoader
{
    // Resources can be obtained synchronously and asynchronously. We need to change behaviour in both cases.
    // Both work basically the same, so we can reduce the main work to one function used by both hooks.

    [StructLayout( LayoutKind.Explicit )]
    public struct GetResourceParameters
    {
        [FieldOffset( 16 )]
        public uint SegmentOffset;

        [FieldOffset( 20 )]
        public uint SegmentLength;

        public bool IsPartialRead
            => SegmentLength != 0;
    }

    public delegate ResourceHandle* GetResourceSyncPrototype( ResourceManager* resourceManager, ResourceCategory* pCategoryId,
        ResourceType* pResourceType, int* pResourceHash, byte* pPath, GetResourceParameters* pGetResParams );

    [Signature( "E8 ?? ?? 00 00 48 8D 8F ?? ?? 00 00 48 89 87 ?? ?? 00 00", DetourName = "GetResourceSyncDetour" )]
    public Hook< GetResourceSyncPrototype > GetResourceSyncHook = null!;

    public delegate ResourceHandle* GetResourceAsyncPrototype( ResourceManager* resourceManager, ResourceCategory* pCategoryId,
        ResourceType* pResourceType, int* pResourceHash, byte* pPath, GetResourceParameters* pGetResParams, bool isUnknown );

    [Signature( "E8 ?? ?? ?? 00 48 8B D8 EB ?? F0 FF 83 ?? ?? 00 00", DetourName = "GetResourceAsyncDetour" )]
    public Hook< GetResourceAsyncPrototype > GetResourceAsyncHook = null!;

    private ResourceHandle* GetResourceSyncDetour( ResourceManager* resourceManager, ResourceCategory* categoryId, ResourceType* resourceType,
        int* resourceHash, byte* path, GetResourceParameters* pGetResParams )
        => GetResourceHandler( true, resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, false );

    private ResourceHandle* GetResourceAsyncDetour( ResourceManager* resourceManager, ResourceCategory* categoryId, ResourceType* resourceType,
        int* resourceHash, byte* path, GetResourceParameters* pGetResParams, bool isUnk )
        => GetResourceHandler( false, resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, isUnk );

    private ResourceHandle* CallOriginalHandler( bool isSync, ResourceManager* resourceManager, ResourceCategory* categoryId,
        ResourceType* resourceType, int* resourceHash, byte* path, GetResourceParameters* pGetResParams, bool isUnk )
        => isSync
            ? GetResourceSyncHook.Original( resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams )
            : GetResourceAsyncHook.Original( resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, isUnk );


    [Conditional( "DEBUG" )]
    private static void CompareHash( int local, int game, Utf8GamePath path )
    {
        if( local != game )
        {
            Penumbra.Log.Warning( $"Hash function appears to have changed. Computed {local:X8} vs Game {game:X8} for {path}." );
        }
    }

    private event Action< Utf8GamePath, ResourceType, FullPath?, object? >? PathResolved;

    public ResourceHandle* ResolvePathSync( ResourceCategory category, ResourceType type, ByteString path )
    {
        var hash = path.Crc32;
        return GetResourceHandler( true, *ResourceManager, &category, &type, &hash, path.Path, null, false );
    }

    internal ResourceHandle* GetResourceHandler( bool isSync, ResourceManager* resourceManager, ResourceCategory* categoryId,
        ResourceType* resourceType, int* resourceHash, byte* path, GetResourceParameters* pGetResParams, bool isUnk )
    {
        using var performance = Penumbra.Performance.Measure( PerformanceType.GetResourceHandler );

        ResourceHandle* ret;
        if( !Utf8GamePath.FromPointer( path, out var gamePath ) )
        {
            Penumbra.Log.Error( "Could not create GamePath from resource path." );
            return CallOriginalHandler( isSync, resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, isUnk );
        }

        CompareHash( ComputeHash( gamePath.Path, pGetResParams ), *resourceHash, gamePath );

        ResourceRequested?.Invoke( gamePath, isSync );

        // If no replacements are being made, we still want to be able to trigger the event.
        var (resolvedPath, data) = ResolvePath( gamePath, *categoryId, *resourceType, *resourceHash );
        PathResolved?.Invoke( gamePath, *resourceType, resolvedPath ?? ( gamePath.IsRooted() ? new FullPath( gamePath ) : null ), data );
        if( resolvedPath == null )
        {
            ret = CallOriginalHandler( isSync, resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, isUnk );
            ResourceLoaded?.Invoke( ( Structs.ResourceHandle* )ret, gamePath, null, data );
            return ret;
        }

        // Replace the hash and path with the correct one for the replacement.
        *resourceHash = ComputeHash( resolvedPath.Value.InternalName, pGetResParams );

        path = resolvedPath.Value.InternalName.Path;
        ret  = CallOriginalHandler( isSync, resourceManager, categoryId, resourceType, resourceHash, path, pGetResParams, isUnk );
        ResourceLoaded?.Invoke( ( Structs.ResourceHandle* )ret, gamePath, resolvedPath.Value, data );

        return ret;
    }


    // Use the default method of path replacement.
    public static (FullPath?, ResolveData) DefaultResolver( Utf8GamePath path )
    {
        var resolved = Penumbra.CollectionManager.Default.ResolvePath( path );
        return ( resolved, Penumbra.CollectionManager.Default.ToResolveData() );
    }

    // Try all resolve path subscribers or use the default replacer.
    private (FullPath?, ResolveData) ResolvePath( Utf8GamePath path, ResourceCategory category, ResourceType resourceType, int resourceHash )
    {
        if( !DoReplacements || _incMode.Value )
        {
            return ( null, ResolveData.Invalid );
        }

        path = path.ToLower();
        if( category == ResourceCategory.Ui )
        {
            var resolved = Penumbra.CollectionManager.Interface.ResolvePath( path );
            return ( resolved, Penumbra.CollectionManager.Interface.ToResolveData() );
        }

        if( ResolvePathCustomization != null )
        {
            foreach( var resolver in ResolvePathCustomization.GetInvocationList() )
            {
                if( ( ( ResolvePathDelegate )resolver ).Invoke( path, category, resourceType, resourceHash, out var ret ) )
                {
                    return ret;
                }
            }
        }

        return DefaultResolver( path );
    }


    // We need to use the ReadFile function to load local, uncompressed files instead of loading them from the SqPacks.
    public delegate byte ReadFileDelegate( ResourceManager* resourceManager, SeFileDescriptor* fileDescriptor, int priority,
        bool isSync );

    [Signature( "E8 ?? ?? ?? ?? 84 C0 0F 84 ?? 00 00 00 4C 8B C3 BA 05" )]
    public ReadFileDelegate ReadFile = null!;

    // We hook ReadSqPack to redirect rooted files to ReadFile.
    public delegate byte ReadSqPackPrototype( ResourceManager* resourceManager, SeFileDescriptor* pFileDesc, int priority, bool isSync );

    [Signature( "E8 ?? ?? ?? ?? EB 05 E8 ?? ?? ?? ?? 84 C0 0F 84 ?? 00 00 00 4C 8B C3", DetourName = nameof( ReadSqPackDetour ) )]
    public Hook< ReadSqPackPrototype > ReadSqPackHook = null!;

    private byte ReadSqPackDetour( ResourceManager* resourceManager, SeFileDescriptor* fileDescriptor, int priority, bool isSync )
    {
        using var performance = Penumbra.Performance.Measure( PerformanceType.ReadSqPack );

        if( !DoReplacements )
        {
            return ReadSqPackHook.Original( resourceManager, fileDescriptor, priority, isSync );
        }

        if( fileDescriptor == null || fileDescriptor->ResourceHandle == null )
        {
            Penumbra.Log.Error( "Failure to load file from SqPack: invalid File Descriptor." );
            return ReadSqPackHook.Original( resourceManager, fileDescriptor, priority, isSync );
        }

        if( !Utf8GamePath.FromSpan( fileDescriptor->ResourceHandle->FileNameSpan(), out var gamePath, false ) || gamePath.Length == 0 )
        {
            return ReadSqPackHook.Original( resourceManager, fileDescriptor, priority, isSync );
        }

        // Paths starting with a '|' are handled separately to allow for special treatment.
        // They are expected to also have a closing '|'.
        if( ResourceLoadCustomization == null || gamePath.Path[ 0 ] != ( byte )'|' )
        {
            return DefaultLoadResource( gamePath.Path, resourceManager, fileDescriptor, priority, isSync );
        }

        // Split the path into the special-treatment part (between the first and second '|')
        // and the actual path.
        byte ret   = 0;
        var  split = gamePath.Path.Split( ( byte )'|', 3, false );
        fileDescriptor->ResourceHandle->FileNameData   = split[ 2 ].Path;
        fileDescriptor->ResourceHandle->FileNameLength = split[ 2 ].Length;
        var funcFound = fileDescriptor->ResourceHandle->Category != ResourceCategory.Ui
         && ResourceLoadCustomization.GetInvocationList()
               .Any( f => ( ( ResourceLoadCustomizationDelegate )f )
                   .Invoke( split[ 1 ], split[ 2 ], resourceManager, fileDescriptor, priority, isSync, out ret ) );

        if( !funcFound )
        {
            ret = DefaultLoadResource( split[ 2 ], resourceManager, fileDescriptor, priority, isSync );
        }

        // Return original resource handle path so that they can be loaded separately.
        fileDescriptor->ResourceHandle->FileNameData   = gamePath.Path.Path;
        fileDescriptor->ResourceHandle->FileNameLength = gamePath.Path.Length;

        return ret;
    }

    // Load the resource from an SqPack and trigger the FileLoaded event.
    private byte DefaultResourceLoad( ByteString path, ResourceManager* resourceManager,
        SeFileDescriptor* fileDescriptor, int priority, bool isSync )
    {
        var ret = Penumbra.ResourceLoader.ReadSqPackHook.Original( resourceManager, fileDescriptor, priority, isSync );
        FileLoaded?.Invoke( fileDescriptor->ResourceHandle, path, ret != 0, false );
        return ret;
    }

    // Load the resource from a path on the users hard drives.
    private byte DefaultRootedResourceLoad( ByteString gamePath, ResourceManager* resourceManager,
        SeFileDescriptor* fileDescriptor, int priority, bool isSync )
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
            var c = ( char )gamePath.Path[ i ];
            ( &fileDescriptor->Utf16FileName )[ i ] = c;
            fdPtr[ i ]                              = c;
        }

        ( &fileDescriptor->Utf16FileName )[ gamePath.Length ] = '\0';
        fdPtr[ gamePath.Length ]                              = '\0';

        // Use the SE ReadFile function.
        var ret = ReadFile( resourceManager, fileDescriptor, priority, isSync );
        FileLoaded?.Invoke( fileDescriptor->ResourceHandle, gamePath, ret != 0, true );
        return ret;
    }

    // Load a resource by its path. If it is rooted, it will be loaded from the drive, otherwise from the SqPack.
    internal byte DefaultLoadResource( ByteString gamePath, ResourceManager* resourceManager, SeFileDescriptor* fileDescriptor, int priority,
        bool isSync )
        => Utf8GamePath.IsRooted( gamePath )
            ? DefaultRootedResourceLoad( gamePath, resourceManager, fileDescriptor, priority, isSync )
            : DefaultResourceLoad( gamePath, resourceManager, fileDescriptor, priority, isSync );

    private void DisposeHooks()
    {
        DisableHooks();
        ReadSqPackHook.Dispose();
        GetResourceSyncHook.Dispose();
        GetResourceAsyncHook.Dispose();
        ResourceHandleDestructorHook?.Dispose();
        _incRefHook.Dispose();
    }

    private static int ComputeHash( ByteString path, GetResourceParameters* pGetResParams )
    {
        if( pGetResParams == null || !pGetResParams->IsPartialRead )
        {
            return path.Crc32;
        }

        // When the game requests file only partially, crc32 includes that information, in format of:
        // path/to/file.ext.hex_offset.hex_size
        // ex) music/ex4/BGM_EX4_System_Title.scd.381adc.30000
        return ByteString.Join(
            ( byte )'.',
            path,
            ByteString.FromStringUnsafe( pGetResParams->SegmentOffset.ToString( "x" ), true ),
            ByteString.FromStringUnsafe( pGetResParams->SegmentLength.ToString( "x" ), true )
        ).Crc32;
    }


    // A resource with ref count 0 that gets incremented goes through GetResourceAsync again.
    // This means, that if the path determined from that is different than the resources path,
    // a different resource gets loaded or incremented, while the IncRef'd resource stays at 0.
    // This causes some problems and is hopefully prevented with this.
    private readonly ThreadLocal< bool >              _incMode = new();
    private readonly Hook< ResourceHandleDestructor > _incRefHook;

    private IntPtr ResourceHandleIncRefDetour( ResourceHandle* handle )
    {
        if( handle->RefCount > 0 )
        {
            return _incRefHook.Original( handle );
        }

        _incMode.Value = true;
        var ret = _incRefHook.Original( handle );
        _incMode.Value = false;
        return ret;
    }
}