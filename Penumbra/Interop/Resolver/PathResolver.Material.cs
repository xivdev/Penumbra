using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Penumbra.GameData.ByteString;
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
        LoadMtrlTexHelper( mtrlResourceHandle );
        var ret = LoadMtrlTexHook!.Original( mtrlResourceHandle );
        _mtrlCollection = null;
        return ret;
    }

    [Signature( "48 89 5C 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 44 0F B7 89",
        DetourName = "LoadMtrlShpkDetour" )]
    public Hook< LoadMtrlFilesDelegate >? LoadMtrlShpkHook;

    private byte LoadMtrlShpkDetour( IntPtr mtrlResourceHandle )
    {
        LoadMtrlShpkHelper( mtrlResourceHandle );
        var ret = LoadMtrlShpkHook!.Original( mtrlResourceHandle );
        _mtrlCollection = null;
        return ret;
    }

    private ModCollection? _mtrlCollection;

    private void LoadMtrlShpkHelper( IntPtr mtrlResourceHandle )
    {
        if( mtrlResourceHandle == IntPtr.Zero )
        {
            return;
        }

        var mtrl     = ( MtrlResource* )mtrlResourceHandle;
        var mtrlPath = Utf8String.FromSpanUnsafe( mtrl->Handle.FileNameSpan(), true, null, true );
        _mtrlCollection = PathCollections.TryGetValue( mtrlPath, out var c ) ? c : null;
    }

    private void LoadMtrlTexHelper( IntPtr mtrlResourceHandle )
    {
        if( mtrlResourceHandle == IntPtr.Zero )
        {
            return;
        }

        var mtrl = ( MtrlResource* )mtrlResourceHandle;
        if( mtrl->NumTex == 0 )
        {
            return;
        }

        var mtrlPath = Utf8String.FromSpanUnsafe( mtrl->Handle.FileNameSpan(), true, null, true );
        _mtrlCollection = PathCollections.TryGetValue( mtrlPath, out var c ) ? c : null;
    }

    // Check specifically for shpk and tex files whether we are currently in a material load.
    private bool HandleMaterialSubFiles( Utf8GamePath gamePath, out ModCollection? collection )
    {
        if( _mtrlCollection != null && ( gamePath.Path.EndsWith( 't', 'e', 'x' ) || gamePath.Path.EndsWith( 's', 'h', 'p', 'k' ) ) )
        {
            collection = _mtrlCollection;
            return true;
        }

        collection = null;
        return false;
    }

    private void EnableMtrlHooks()
    {
        LoadMtrlShpkHook?.Enable();
        LoadMtrlTexHook?.Enable();
    }

    private void DisableMtrlHooks()
    {
        LoadMtrlShpkHook?.Disable();
        LoadMtrlTexHook?.Disable();
    }

    private void DisposeMtrlHooks()
    {
        LoadMtrlShpkHook?.Dispose();
        LoadMtrlTexHook?.Dispose();
    }
}