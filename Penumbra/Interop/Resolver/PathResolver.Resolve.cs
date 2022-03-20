using System;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.GameData.ByteString;
using Penumbra.Mods;

namespace Penumbra.Interop.Resolver;

// The actual resolve detours are basically all the same.
public unsafe partial class PathResolver
{
    // Humans
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


    // Weapons
    private IntPtr ResolveWeaponDecalDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolveWeaponPathDetour( drawObject, ResolveWeaponDecalPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveWeaponEidDetour( IntPtr drawObject, IntPtr path, IntPtr unk3 )
        => ResolveWeaponPathDetour( drawObject, ResolveWeaponEidPathHook!.Original( drawObject, path, unk3 ) );

    private IntPtr ResolveWeaponImcDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolveWeaponPathDetour( drawObject, ResolveWeaponImcPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveWeaponMPapDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, uint unk5 )
        => ResolveWeaponPathDetour( drawObject, ResolveWeaponMPapPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );

    private IntPtr ResolveWeaponMdlDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint modelType )
        => ResolveWeaponPathDetour( drawObject, ResolveWeaponMdlPathHook!.Original( drawObject, path, unk3, modelType ) );

    private IntPtr ResolveWeaponMtrlDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
        => ResolveWeaponPathDetour( drawObject, ResolveWeaponMtrlPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );

    private IntPtr ResolveWeaponPapDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
        => ResolveWeaponPathDetour( drawObject, ResolveWeaponPapPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );

    private IntPtr ResolveWeaponPhybDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolveWeaponPathDetour( drawObject, ResolveWeaponPhybPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveWeaponSklbDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolveWeaponPathDetour( drawObject, ResolveWeaponSklbPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveWeaponSkpDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolveWeaponPathDetour( drawObject, ResolveWeaponSkpPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveWeaponTmbDetour( IntPtr drawObject, IntPtr path, IntPtr unk3 )
        => ResolveWeaponPathDetour( drawObject, ResolveWeaponTmbPathHook!.Original( drawObject, path, unk3 ) );

    private IntPtr ResolveWeaponVfxDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
        => ResolveWeaponPathDetour( drawObject, ResolveWeaponVfxPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );


    // Implementation
    [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
    private IntPtr ResolvePathDetour( IntPtr drawObject, IntPtr path )
        => ResolvePathDetour( FindParent( drawObject, out var collection ) == null
            ? Penumbra.ModManager.Collections.DefaultCollection
            : collection, path );

    // Weapons have the characters DrawObject as a parent,
    // but that may not be set yet when creating a new object, so we have to do the same detour
    // as for Human DrawObjects that are just being created.
    [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
    private IntPtr ResolveWeaponPathDetour( IntPtr drawObject, IntPtr path )
    {
        var parentObject = ( ( DrawObject* )drawObject )->Object.ParentObject;
        if( parentObject == null && LastGameObject != null )
        {
            var collection = IdentifyCollection( LastGameObject );
            return ResolvePathDetour( collection, path );
        }
        else
        {
            var parent = FindParent( ( IntPtr )parentObject, out var collection );
            return ResolvePathDetour( parent == null
                ? Penumbra.ModManager.Collections.DefaultCollection
                : collection, path );
        }
    }

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