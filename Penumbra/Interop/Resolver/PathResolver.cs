using System;
using System.Diagnostics.CodeAnalysis;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Loader;

namespace Penumbra.Interop.Resolver;

// The Path Resolver handles character collections.
// It will hook any path resolving functions for humans,
// as well as DrawObject creation.
// It links draw objects to actors, and actors to character collections,
// to resolve paths for character collections.
public partial class PathResolver : IDisposable
{
    private readonly ResourceLoader _loader;
    public bool Enabled { get; private set; }

    public PathResolver( ResourceLoader loader )
    {
        _loader = loader;
        SignatureHelper.Initialise( this );
        SetupHumanHooks();
        SetupWeaponHooks();
        SetupMonsterHooks();
        SetupDemiHooks();
        SetupMetaHooks();
    }

    // The modified resolver that handles game path resolving.
    private bool CharacterResolver( Utf8GamePath gamePath, ResourceCategory _1, ResourceType type, int _2, out (FullPath?, object?) data )
    {
        // Check if the path was marked for a specific collection,
        // or if it is a file loaded by a material, and if we are currently in a material load,
        // or if it is a face decal path and the current mod collection is set.
        // If not use the default collection.
        // We can remove paths after they have actually been loaded.
        // A potential next request will add the path anew.
        var nonDefault = HandleMaterialSubFiles( type, out var collection )
         || PathCollections.TryRemove( gamePath.Path, out collection )
         || HandleAnimationFile( type, gamePath, out collection )
         || HandleDecalFile( type, gamePath, out collection );
        if( !nonDefault || collection == null )
        {
            collection = Penumbra.CollectionManager.Default;
        }

        // Resolve using character/default collection first, otherwise forced, as usual.
        var resolved = collection.ResolvePath( gamePath );

        // Since mtrl files load their files separately, we need to add the new, resolved path
        // so that the functions loading tex and shpk can find that path and use its collection.
        // We also need to handle defaulted materials against a non-default collection.
        var path = resolved == null ? gamePath.Path.ToString() : resolved.Value.FullName;
        HandleMtrlCollection( collection, path, nonDefault, type, resolved, out data );
        return true;
    }

    private bool HandleDecalFile( ResourceType type, Utf8GamePath gamePath, [NotNullWhen( true )] out ModCollection? collection )
    {
        if( type                  == ResourceType.Tex
        && _lastCreatedCollection != null
        && gamePath.Path.Substring( "chara/common/texture/".Length ).StartsWith( 'd', 'e', 'c', 'a', 'l', '_', 'f', 'a', 'c', 'e' ) )
        {
            collection = _lastCreatedCollection;
            return true;
        }

        collection = null;
        return false;
    }

    private bool HandleAnimationFile( ResourceType type, Utf8GamePath _, [NotNullWhen( true )] out ModCollection? collection )
    {
        switch( type )
        {
            case ResourceType.Tmb:
            case ResourceType.Pap:
            case ResourceType.Scd:
                if( _animationLoadCollection != null )
                {
                    collection = _animationLoadCollection;
                    return true;
                }

                break;
            case ResourceType.Avfx:
                _lastAvfxCollection = _animationLoadCollection ?? Penumbra.CollectionManager.Default;
                if( _animationLoadCollection != null )
                {
                    collection = _animationLoadCollection;
                    return true;
                }

                break;
            case ResourceType.Atex:
                if( _lastAvfxCollection != null )
                {
                    collection = _lastAvfxCollection;
                    return true;
                }

                if( _animationLoadCollection != null )
                {
                    collection = _animationLoadCollection;
                    return true;
                }

                break;
        }

        collection = null;
        return false;
    }

    public void Enable()
    {
        if( Enabled )
        {
            return;
        }

        Enabled = true;
        InitializeDrawObjects();

        EnableHumanHooks();
        EnableWeaponHooks();
        EnableMonsterHooks();
        EnableDemiHooks();
        EnableMtrlHooks();
        EnableDataHooks();
        EnableMetaHooks();

        _loader.ResolvePathCustomization += CharacterResolver;
        PluginLog.Debug( "Character Path Resolver enabled." );
    }

    public void Disable()
    {
        if( !Enabled )
        {
            return;
        }

        Enabled = false;
        DisableHumanHooks();
        DisableWeaponHooks();
        DisableMonsterHooks();
        DisableDemiHooks();
        DisableMtrlHooks();
        DisableDataHooks();
        DisableMetaHooks();

        DrawObjectToObject.Clear();
        PathCollections.Clear();

        _loader.ResolvePathCustomization -= CharacterResolver;
        PluginLog.Debug( "Character Path Resolver disabled." );
    }

    public void Dispose()
    {
        Disable();
        DisposeHumanHooks();
        DisposeWeaponHooks();
        DisposeMonsterHooks();
        DisposeDemiHooks();
        DisposeMtrlHooks();
        DisposeDataHooks();
        DisposeMetaHooks();
    }

    public unsafe (IntPtr, ModCollection) IdentifyDrawObject( IntPtr drawObject )
    {
        var parent = FindParent( drawObject, out var collection );
        return ( ( IntPtr )parent, collection );
    }
}