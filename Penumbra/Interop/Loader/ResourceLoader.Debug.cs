using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.STD;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Interop.Loader;

public unsafe partial class ResourceLoader
{
    // If in debug mode, this logs any resource at refcount 0 that gets decremented again, and skips the decrement instead.
    private delegate byte                         ResourceHandleDecRef( ResourceHandle* handle );
    private readonly Hook< ResourceHandleDecRef > _decRefHook;

    public delegate IntPtr ResourceHandleDestructor( ResourceHandle* handle );

    [Signature( "48 89 5C 24 ?? 57 48 83 EC ?? 48 8D 05 ?? ?? ?? ?? 48 8B D9 48 89 01 B8",
        DetourName = nameof( ResourceHandleDestructorDetour ) )]
    public static Hook< ResourceHandleDestructor >? ResourceHandleDestructorHook;

    private IntPtr ResourceHandleDestructorDetour( ResourceHandle* handle )
    {
        if( handle != null )
        {
            Penumbra.Log.Information( $"[ResourceLoader] Destructing Resource Handle {handle->FileName} at 0x{( ulong )handle:X} (Refcount {handle->RefCount})." );
        }

        return ResourceHandleDestructorHook!.Original( handle );
    }

    // A static pointer to the SE Resource Manager
    [Signature( "48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 32 C0", ScanType = ScanType.StaticAddress, UseFlags = SignatureUseFlags.Pointer )]
    public static ResourceManager** ResourceManager;

    // Gather some debugging data about penumbra-loaded objects.
    public struct DebugData
    {
        public Structs.ResourceHandle* OriginalResource;
        public Structs.ResourceHandle* ManipulatedResource;
        public Utf8GamePath            OriginalPath;
        public FullPath                ManipulatedPath;
        public ResourceCategory        Category;
        public ResolveData             ResolverInfo;
        public ResourceType            Extension;
    }

    private readonly SortedList< FullPath, DebugData > _debugList = new();

    public IReadOnlyDictionary< FullPath, DebugData > DebugList
        => _debugList;

    public void EnableDebug()
    {
        _decRefHook.Enable();
        ResourceLoaded += AddModifiedDebugInfo;
    }

    public void DisableDebug()
    {
        _decRefHook.Disable();
        ResourceLoaded -= AddModifiedDebugInfo;
    }

    private void AddModifiedDebugInfo( Structs.ResourceHandle* handle, Utf8GamePath originalPath, FullPath? manipulatedPath,
        ResolveData resolverInfo )
    {
        using var performance = Penumbra.Performance.Measure( PerformanceType.DebugTimes );

        if( manipulatedPath == null || manipulatedPath.Value.Crc64 == 0 )
        {
            return;
        }

        // Got some incomprehensible null-dereference exceptions here when hot-reloading penumbra.
        try
        {
            var crc              = ( uint )originalPath.Path.Crc32;
            var originalResource = FindResource( handle->Category, handle->FileType, crc );
            _debugList[ manipulatedPath.Value ] = new DebugData()
            {
                OriginalResource    = ( Structs.ResourceHandle* )originalResource,
                ManipulatedResource = handle,
                Category            = handle->Category,
                Extension           = handle->FileType,
                OriginalPath        = originalPath.Clone(),
                ManipulatedPath     = manipulatedPath.Value,
                ResolverInfo        = resolverInfo,
            };
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( e.ToString() );
        }
    }

    // Find a key in a StdMap.
    private static TValue* FindInMap< TKey, TValue >( StdMap< TKey, TValue >* map, in TKey key )
        where TKey : unmanaged, IComparable< TKey >
        where TValue : unmanaged
    {
        if( map == null || map->Count == 0 )
        {
            return null;
        }

        var node = map->Head->Parent;
        while( !node->IsNil )
        {
            switch( key.CompareTo( node->KeyValuePair.Item1 ) )
            {
                case 0: return &node->KeyValuePair.Item2;
                case < 0:
                    node = node->Left;
                    break;
                default:
                    node = node->Right;
                    break;
            }
        }

        return null;
    }

    // Iterate in tree-order through a map, applying action to each KeyValuePair.
    private static void IterateMap< TKey, TValue >( StdMap< TKey, TValue >* map, Action< TKey, TValue > action )
        where TKey : unmanaged
        where TValue : unmanaged
    {
        if( map == null || map->Count == 0 )
        {
            return;
        }

        for( var node = map->SmallestValue; !node->IsNil; node = node->Next() )
        {
            action( node->KeyValuePair.Item1, node->KeyValuePair.Item2 );
        }
    }


    // Find a resource in the resource manager by its category, extension and crc-hash
    public static ResourceHandle* FindResource( ResourceCategory cat, ResourceType ext, uint crc32 )
    {
        ref var manager = ref *ResourceManager;
        var     catIdx  = ( uint )cat >> 0x18;
        cat = ( ResourceCategory )( ushort )cat;
        var category = ( ResourceGraph.CategoryContainer* )manager->ResourceGraph->ContainerArray + ( int )cat;
        var extMap = FindInMap( ( StdMap< uint, Pointer< StdMap< uint, Pointer< ResourceHandle > > > >* )category->CategoryMaps[ catIdx ],
            ( uint )ext );
        if( extMap == null )
        {
            return null;
        }

        var ret = FindInMap( extMap->Value, crc32 );
        return ret == null ? null : ret->Value;
    }

    public delegate void ExtMapAction( ResourceCategory category, StdMap< uint, Pointer< StdMap< uint, Pointer< ResourceHandle > > > >* graph,
        int idx );

    public delegate void ResourceMapAction( uint ext, StdMap< uint, Pointer< ResourceHandle > >* graph );
    public delegate void ResourceAction( uint crc32, ResourceHandle* graph );

    // Iteration functions through the resource manager.
    public static void IterateGraphs( ExtMapAction action )
    {
        ref var manager = ref *ResourceManager;
        foreach( var resourceType in Enum.GetValues< ResourceCategory >().SkipLast( 1 ) )
        {
            var graph = ( ResourceGraph.CategoryContainer* )manager->ResourceGraph->ContainerArray + ( int )resourceType;
            for( var i = 0; i < 20; ++i )
            {
                var map = ( StdMap< uint, Pointer< StdMap< uint, Pointer< ResourceHandle > > > >* )graph->CategoryMaps[ i ];
                if( map != null )
                {
                    action( resourceType, map, i );
                }
            }
        }
    }

    public static void IterateExtMap( StdMap< uint, Pointer< StdMap< uint, Pointer< ResourceHandle > > > >* map, ResourceMapAction action )
        => IterateMap( map, ( ext, m ) => action( ext, m.Value ) );

    public static void IterateResourceMap( StdMap< uint, Pointer< ResourceHandle > >* map, ResourceAction action )
        => IterateMap( map, ( crc, r ) => action( crc, r.Value ) );

    public static void IterateResources( ResourceAction action )
    {
        IterateGraphs( ( _, extMap, _ )
            => IterateExtMap( extMap, ( _, resourceMap )
                => IterateResourceMap( resourceMap, action ) ) );
    }

    // Update the list of currently replaced resources.
    // Only used when the Replaced Resources Tab in the Debug tab is open.
    public void UpdateDebugInfo()
    {
        using var performance = Penumbra.Performance.Measure( PerformanceType.DebugTimes );
        for( var i = 0; i < _debugList.Count; ++i )
        {
            var data = _debugList.Values[ i ];
            if( data.OriginalPath.Path == null )
            {
                _debugList.RemoveAt( i-- );
                continue;
            }

            var regularResource  = FindResource( data.Category, data.Extension, ( uint )data.OriginalPath.Path.Crc32 );
            var modifiedResource = FindResource( data.Category, data.Extension, ( uint )data.ManipulatedPath.InternalName.Crc32 );
            if( modifiedResource == null )
            {
                _debugList.RemoveAt( i-- );
            }
            else if( regularResource != data.OriginalResource || modifiedResource != data.ManipulatedResource )
            {
                _debugList[ _debugList.Keys[ i ] ] = data with
                {
                    OriginalResource = ( Structs.ResourceHandle* )regularResource,
                    ManipulatedResource = ( Structs.ResourceHandle* )modifiedResource,
                };
            }
        }
    }

    // Prevent resource management weirdness.
    private byte ResourceHandleDecRefDetour( ResourceHandle* handle )
    {
        if( handle == null )
        {
            return 0;
        }

        if( handle->RefCount != 0 )
        {
            return _decRefHook.Original( handle );
        }

        Penumbra.Log.Error( $"Caught decrease of Reference Counter for {handle->FileName} at 0x{( ulong )handle:X} below 0." );
        return 1;
    }

    // Logging functions for EnableFullLogging.
    private static void LogPath( Utf8GamePath path, bool synchronous )
        => Penumbra.Log.Information( $"[ResourceLoader] Requested {path} {( synchronous ? "synchronously." : "asynchronously." )}" );

    private static void LogResource( Structs.ResourceHandle* handle, Utf8GamePath path, FullPath? manipulatedPath, ResolveData _ )
    {
        var pathString = manipulatedPath != null ? $"custom file {manipulatedPath} instead of {path}" : path.ToString();
        Penumbra.Log.Information( $"[ResourceLoader] [{handle->FileType}] Loaded {pathString} to 0x{( ulong )handle:X}. (Refcount {handle->RefCount})" );
    }

    private static void LogLoadedFile( Structs.ResourceHandle* resource, ByteString path, bool success, bool custom )
        => Penumbra.Log.Information( $"[ResourceLoader] Loading {path} from {( custom ? "local files" : "SqPack" )} into 0x{( ulong )resource:X} returned {success}." );
}