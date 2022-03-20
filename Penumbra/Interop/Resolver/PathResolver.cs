using System;
using Dalamud.Utility.Signatures;
using Penumbra.GameData.ByteString;
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

    // Keep track of the last path resolver to be able to restore it.
    private Func< Utf8GamePath, (FullPath?, object?) > _oldResolver = null!;

    public PathResolver( ResourceLoader loader )
    {
        _loader = loader;
        SignatureHelper.Initialise( this );
        SetupHumanHooks();
        SetupWeaponHooks();
        SetupMetaHooks();
        Enable();
    }

    // The modified resolver that handles game path resolving.
    private (FullPath?, object?) CharacterResolver( Utf8GamePath gamePath )
    {
        // Check if the path was marked for a specific collection,
        // or if it is a file loaded by a material, and if we are currently in a material load.
        // If not use the default collection.
        var nonDefault = HandleMaterialSubFiles( gamePath, out var collection ) || PathCollections.TryGetValue( gamePath.Path, out collection );
        if( !nonDefault )
        {
            collection = Penumbra.ModManager.Collections.DefaultCollection;
        }
        else
        {
            // We can remove paths after they have actually been loaded.
            // A potential next request will add the path anew.
            PathCollections.Remove( gamePath.Path );
        }

        // Resolve using character/default collection first, otherwise forced, as usual.
        var resolved = collection!.ResolveSwappedOrReplacementPath( gamePath );
        if( resolved == null )
        {
            resolved = Penumbra.ModManager.Collections.ForcedCollection.ResolveSwappedOrReplacementPath( gamePath );
            if( resolved == null )
            {
                // We also need to handle defaulted materials against a non-default collection.
                if( nonDefault && gamePath.Path.EndsWith( 'm', 't', 'r', 'l' ) )
                {
                    SetCollection( gamePath.Path, collection );
                }

                return ( null, collection );
            }

            collection = Penumbra.ModManager.Collections.ForcedCollection;
        }

        // Since mtrl files load their files separately, we need to add the new, resolved path
        // so that the functions loading tex and shpk can find that path and use its collection.
        if( nonDefault && resolved.Value.Extension == ".mtrl" )
        {
            SetCollection( resolved.Value.InternalName, nonDefault ? collection : null );
        }

        return ( resolved, collection );
    }

    public void Enable()
    {
        InitializeDrawObjects();

        EnableHumanHooks();
        EnableWeaponHooks();
        EnableMtrlHooks();
        EnableDataHooks();
        EnableMetaHooks();

        _oldResolver        = _loader.ResolvePath;
        _loader.ResolvePath = CharacterResolver;
    }

    public void Disable()
    {
        DisableHumanHooks();
        DisableWeaponHooks();
        DisableMtrlHooks();
        DisableDataHooks();
        DisableMetaHooks();

        _loader.ResolvePath = _oldResolver;
    }

    public void Dispose()
    {
        Disable();
        DisposeHumanHooks();
        DisposeWeaponHooks();
        DisposeMtrlHooks();
        DisposeDataHooks();
        DisposeMetaHooks();
    }
}