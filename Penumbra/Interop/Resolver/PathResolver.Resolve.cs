using System;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;

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

    // Monsters
    private IntPtr ResolveMonsterDecalDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveMonsterDecalPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveMonsterEidDetour( IntPtr drawObject, IntPtr path, IntPtr unk3 )
        => ResolvePathDetour( drawObject, ResolveMonsterEidPathHook!.Original( drawObject, path, unk3 ) );

    private IntPtr ResolveMonsterImcDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveMonsterImcPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveMonsterMPapDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, uint unk5 )
        => ResolvePathDetour( drawObject, ResolveMonsterMPapPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );

    private IntPtr ResolveMonsterMdlDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint modelType )
        => ResolvePathDetour( drawObject, ResolveMonsterMdlPathHook!.Original( drawObject, path, unk3, modelType ) );

    private IntPtr ResolveMonsterMtrlDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
        => ResolvePathDetour( drawObject, ResolveMonsterMtrlPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );

    private IntPtr ResolveMonsterPapDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
        => ResolvePathDetour( drawObject, ResolveMonsterPapPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );

    private IntPtr ResolveMonsterPhybDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveMonsterPhybPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveMonsterSklbDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveMonsterSklbPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveMonsterSkpDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveMonsterSkpPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveMonsterTmbDetour( IntPtr drawObject, IntPtr path, IntPtr unk3 )
        => ResolvePathDetour( drawObject, ResolveMonsterTmbPathHook!.Original( drawObject, path, unk3 ) );

    private IntPtr ResolveMonsterVfxDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
        => ResolvePathDetour( drawObject, ResolveMonsterVfxPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );

    // Demihumans
    private IntPtr ResolveDemiDecalDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveDemiDecalPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveDemiEidDetour( IntPtr drawObject, IntPtr path, IntPtr unk3 )
        => ResolvePathDetour( drawObject, ResolveDemiEidPathHook!.Original( drawObject, path, unk3 ) );

    private IntPtr ResolveDemiImcDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveDemiImcPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveDemiMPapDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, uint unk5 )
        => ResolvePathDetour( drawObject, ResolveDemiMPapPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );

    private IntPtr ResolveDemiMdlDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint modelType )
        => ResolvePathDetour( drawObject, ResolveDemiMdlPathHook!.Original( drawObject, path, unk3, modelType ) );

    private IntPtr ResolveDemiMtrlDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
        => ResolvePathDetour( drawObject, ResolveDemiMtrlPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );

    private IntPtr ResolveDemiPapDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
        => ResolvePathDetour( drawObject, ResolveDemiPapPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );

    private IntPtr ResolveDemiPhybDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveDemiPhybPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveDemiSklbDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveDemiSklbPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveDemiSkpDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 )
        => ResolvePathDetour( drawObject, ResolveDemiSkpPathHook!.Original( drawObject, path, unk3, unk4 ) );

    private IntPtr ResolveDemiTmbDetour( IntPtr drawObject, IntPtr path, IntPtr unk3 )
        => ResolvePathDetour( drawObject, ResolveDemiTmbPathHook!.Original( drawObject, path, unk3 ) );

    private IntPtr ResolveDemiVfxDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 )
        => ResolvePathDetour( drawObject, ResolveDemiVfxPathHook!.Original( drawObject, path, unk3, unk4, unk5 ) );


    // Implementation
    [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
    private IntPtr ResolvePathDetour( IntPtr drawObject, IntPtr path )
        => ResolvePathDetour( FindParent( drawObject, out var collection ) == null
            ? Penumbra.CollectionManager.Default
            : collection, path );

    // Weapons have the characters DrawObject as a parent,
    // but that may not be set yet when creating a new object, so we have to do the same detour
    // as for Human DrawObjects that are just being created.
    [MethodImpl( MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization )]
    private IntPtr ResolveWeaponPathDetour( IntPtr drawObject, IntPtr path )
    {
        var parent = FindParent( drawObject, out var collection );
        if( parent != null )
        {
            return ResolvePathDetour( collection, path );
        }

        var parentObject = ( ( DrawObject* )drawObject )->Object.ParentObject;
        if( parentObject == null && LastGameObject != null )
        {
            var c2 = IdentifyCollection( LastGameObject );
            DrawObjectToObject[ drawObject ] = ( c2, LastGameObject->ObjectIndex );
            return ResolvePathDetour( c2, path );
        }

        parent = FindParent( ( IntPtr )parentObject, out collection );
        return ResolvePathDetour( parent == null
            ? Penumbra.CollectionManager.Default
            : collection, path );
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
        SetCollection( gamePath, collection );
        return path;
    }
}