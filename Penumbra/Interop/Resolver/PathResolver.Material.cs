using System;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Mods;

namespace Penumbra.Interop.Resolver;

// Materials do contain their own paths to textures and shader packages.
// Those are loaded synchronously.
// Thus, we need to ensure the correct files are loaded when a material is loaded.
public unsafe partial class PathResolver
{
    public delegate byte LoadMtrlFilesDelegate( IntPtr mtrlResourceHandle );

    [Signature( "4C 8B DC 49 89 5B ?? 49 89 73 ?? 55 57 41 55", DetourName = "LoadMtrlTexDetour" )]
    public Hook< LoadMtrlFilesDelegate >? LoadMtrlTexHook;

    private byte LoadMtrlTexDetour( IntPtr mtrlResourceHandle )
    {
        LoadMtrlHelper( mtrlResourceHandle );
        var ret = LoadMtrlTexHook!.Original( mtrlResourceHandle );
        _mtrlCollection = null;
        return ret;
    }

    [Signature( "48 89 5C 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 44 0F B7 89",
        DetourName = "LoadMtrlShpkDetour" )]
    public Hook< LoadMtrlFilesDelegate >? LoadMtrlShpkHook;

    private byte LoadMtrlShpkDetour( IntPtr mtrlResourceHandle )
    {
        LoadMtrlHelper( mtrlResourceHandle );
        var ret = LoadMtrlShpkHook!.Original( mtrlResourceHandle );
        _mtrlCollection = null;
        return ret;
    }

    private ModCollection2? _mtrlCollection;

    private void LoadMtrlHelper( IntPtr mtrlResourceHandle )
    {
        if( mtrlResourceHandle == IntPtr.Zero )
        {
            return;
        }

        var mtrl     = ( MtrlResource* )mtrlResourceHandle;
        var mtrlPath = Utf8String.FromSpanUnsafe( mtrl->Handle.FileNameSpan(), true, null, true );
        _mtrlCollection = PathCollections.TryGetValue( mtrlPath, out var c ) ? c : null;
    }

    // Check specifically for shpk and tex files whether we are currently in a material load.
    private bool HandleMaterialSubFiles( ResourceType type, out ModCollection2? collection )
    {
        if( _mtrlCollection != null && type is ResourceType.Tex or ResourceType.Shpk )
        {
            collection = _mtrlCollection;
            return true;
        }

        collection = null;
        return false;
    }

    // We need to set the correct collection for the actual material path that is loaded
    // before actually loading the file.
    private bool MtrlLoadHandler( Utf8String split, Utf8String path, ResourceManager* resourceManager,
        SeFileDescriptor* fileDescriptor, int priority, bool isSync, out byte ret )
    {
        ret = 0;
        if( fileDescriptor->ResourceHandle->FileType != ResourceType.Mtrl )
        {
            return false;
        }

        if( Penumbra.CollectionManager.ByName( split.ToString(), out var collection ) )
        {
            PluginLog.Verbose( "Using MtrlLoadHandler with collection {$Split:l} for path {$Path:l}.", split, path );
            SetCollection( path, collection );
        }
        else
        {
            PluginLog.Verbose( "Using MtrlLoadHandler with no collection for path {$Path:l}.", path );
        }


        ret = Penumbra.ResourceLoader.DefaultLoadResource( path, resourceManager, fileDescriptor, priority, isSync );
        PathCollections.TryRemove( path, out _ );
        return true;
    }

    // Materials need to be set per collection so they can load their textures independently from each other.
    private static void HandleMtrlCollection( ModCollection2 collection, string path, bool nonDefault, ResourceType type, FullPath? resolved,
        out (FullPath?, object?) data )
    {
        if( nonDefault && type == ResourceType.Mtrl )
        {
            var fullPath = new FullPath( $"|{collection.Name}|{path}" );
            data = ( fullPath, collection );
        }
        else
        {
            data = ( resolved, collection );
        }
    }

    private void EnableMtrlHooks()
    {
        LoadMtrlShpkHook?.Enable();
        LoadMtrlTexHook?.Enable();
        Penumbra.ResourceLoader.ResourceLoadCustomization += MtrlLoadHandler;
    }

    private void DisableMtrlHooks()
    {
        LoadMtrlShpkHook?.Disable();
        LoadMtrlTexHook?.Disable();
        Penumbra.ResourceLoader.ResourceLoadCustomization -= MtrlLoadHandler;
    }

    private void DisposeMtrlHooks()
    {
        LoadMtrlShpkHook?.Dispose();
        LoadMtrlTexHook?.Dispose();
    }
}