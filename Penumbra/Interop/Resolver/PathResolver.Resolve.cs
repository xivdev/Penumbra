using System;
using System.Runtime.CompilerServices;
using Penumbra.GameData.ByteString;
using Penumbra.Mods;

namespace Penumbra.Interop.Resolver;

// The actual resolve detours are basically all the same.
public unsafe partial class PathResolver
{
    private IntPtr ResolveDecalDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveDecalPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveEidDetour( IntPtr drawObject, IntPtr path, IntPtr unk3 )
        => ResolvePathDetour( drawObject, ResolveEidPathHook!.Original( drawObject, path, unk3 ) );

    private IntPtr ResolveImcDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveImcPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveMPapDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, uint unk5 )
        => ResolvePathDetour( drawObject, ResolveMPapPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );

    private IntPtr ResolveMdlDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint modelType )
    {
        using var eqdp = MetaChanger.ChangeEqdp( this, drawObject, modelType );
        return ResolvePathDetour( drawObject, ResolveMdlPathHook!.Original( drawObject, path, unk3, modelType ) );
    }

    private IntPtr ResolveMtrlDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
        => ResolvePathDetour( drawObject, ResolveMtrlPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );

    private IntPtr ResolvePapDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
    {
        using var est = MetaChanger.ChangeEst( this, drawObject );
        return ResolvePathDetour( drawObject, ResolvePapPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );
    }

    private IntPtr ResolvePhybDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
    {
        using var est = MetaChanger.ChangeEst( this, drawObject );
        return ResolvePathDetour( drawObject, ResolvePhybPathHook!.Original( drawObject, path, unk3, unk4 ) );
    }

    private IntPtr ResolveSklbDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
    {
        using var est = MetaChanger.ChangeEst( this, drawObject );
        return ResolvePathDetour( drawObject, ResolveSklbPathHook!.Original( drawObject, path, unk3, unk4 ) );
    }

    private IntPtr ResolveSkpDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
    {
        using var est = MetaChanger.ChangeEst( this, drawObject );
        return ResolvePathDetour( drawObject, ResolveSkpPathHook!.Original( drawObject, path, unk3, unk4 ) );
    }

    private IntPtr ResolveTmbDetour( IntPtr drawObject, IntPtr path, IntPtr unk3 )
        => ResolvePathDetour( drawObject, ResolveTmbPathHook!.Original( drawObject, path, unk3 ) );

    private IntPtr ResolveVfxDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
        => ResolvePathDetour( drawObject, ResolveVfxPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );


    [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
    private IntPtr ResolvePathDetour( IntPtr drawObject, IntPtr path )
        => ResolvePathDetour( FindParent( drawObject, out var collection ) == null
            ? Penumbra.ModManager.Collections.DefaultCollection
            : collection, path );


    // Just add or remove the resolved path.
    [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
    private IntPtr ResolvePathDetour( ModCollection collection, IntPtr path )
    {
        if( path == IntPtr.Zero )
        {
            return path;
        }

        var gamePath = new Utf8String( ( byte* )path );
        if( collection == Penumbra.ModManager.Collections.DefaultCollection )
        {
            SetCollection( gamePath, null );
            return path;
        }

        SetCollection( gamePath, collection );
        return path;
    }
}