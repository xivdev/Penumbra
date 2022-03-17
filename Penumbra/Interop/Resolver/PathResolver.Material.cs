using System;
using System.Linq;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Penumbra.GameData.ByteString;
using Penumbra.Interop.Structs;

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
        return LoadMtrlTexHook!.Original( mtrlResourceHandle );
    }

    [Signature( "48 89 5C 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 44 0F B7 89",
        DetourName = "LoadMtrlShpkDetour" )]
    public Hook< LoadMtrlFilesDelegate >? LoadMtrlShpkHook;

    private byte LoadMtrlShpkDetour( IntPtr mtrlResourceHandle )
    {
        LoadMtrlShpkHelper( mtrlResourceHandle );
        return LoadMtrlShpkHook!.Original( mtrlResourceHandle );
    }

    // Taken from the actual hooked function. The Shader string is just concatenated to this base directory.
    private static readonly Utf8String ShaderBase = Utf8String.FromStringUnsafe( "shader/sm5/shpk", true );

    private void LoadMtrlShpkHelper( IntPtr mtrlResourceHandle )
    {
        if( mtrlResourceHandle == IntPtr.Zero )
        {
            return;
        }

        var mtrl       = ( MtrlResource* )mtrlResourceHandle;
        var shpkPath   = Utf8String.Join( ( byte )'/', ShaderBase, new Utf8String( mtrl->ShpkString ).AsciiToLower() );
        var mtrlPath   = Utf8String.FromSpanUnsafe( mtrl->Handle.FileNameSpan(), true, null, true );
        var collection = PathCollections.TryGetValue( mtrlPath, out var c ) ? c : null;
        SetCollection( shpkPath, collection );
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

        var mtrlPath   = Utf8String.FromSpanUnsafe( mtrl->Handle.FileNameSpan(), true, null, true );
        var collection = PathCollections.TryGetValue( mtrlPath, out var c ) ? c : null;
        var x          = PathCollections.ToList();
        for( var i = 0; i < mtrl->NumTex; ++i )
        {
            var texString = new Utf8String( mtrl->TexString( i ) );
            SetCollection( texString, collection );
        }
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