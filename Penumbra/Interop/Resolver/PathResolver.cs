using System;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Loader;
using Penumbra.Mods;

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
        SetupMetaHooks();
        Penumbra.CollectionManager.CollectionChanged += OnCollectionChange;
    }

    // The modified resolver that handles game path resolving.
    private bool CharacterResolver( Utf8GamePath gamePath, ResourceCategory _1, ResourceType type, int _2, out (FullPath?, object?) data )
    {
        // Check if the path was marked for a specific collection,
        // or if it is a file loaded by a material, and if we are currently in a material load.
        // If not use the default collection.
        // We can remove paths after they have actually been loaded.
        // A potential next request will add the path anew.
        var nonDefault = HandleMaterialSubFiles( type, out var collection ) || PathCollections.TryRemove( gamePath.Path, out collection );
        if( !nonDefault )
        {
            collection = Penumbra.CollectionManager.Default;
        }

        // Resolve using character/default collection first, otherwise forced, as usual.
        var resolved = collection!.ResolvePath( gamePath );

        // Since mtrl files load their files separately, we need to add the new, resolved path
        // so that the functions loading tex and shpk can find that path and use its collection.
        // We also need to handle defaulted materials against a non-default collection.
        var path = resolved == null ? gamePath.Path.ToString() : resolved.Value.FullName;
        HandleMtrlCollection( collection, path, nonDefault, type, resolved, out data );
        return true;
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
        EnableMtrlHooks();
        EnableDataHooks();
        EnableMetaHooks();

        _loader.ResolvePathCustomization += CharacterResolver;
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
        DisableMtrlHooks();
        DisableDataHooks();
        DisableMetaHooks();

        DrawObjectToObject.Clear();
        PathCollections.Clear();

        _loader.ResolvePathCustomization -= CharacterResolver;
    }

    public void Dispose()
    {
        Disable();
        DisposeHumanHooks();
        DisposeWeaponHooks();
        DisposeMtrlHooks();
        DisposeDataHooks();
        DisposeMetaHooks();
        Penumbra.CollectionManager.CollectionChanged -= OnCollectionChange;
    }

    private void OnCollectionChange( ModCollection2? _1, ModCollection2? _2, CollectionType type, string? characterName )
    {
        if( type != CollectionType.Character )
        {
            return;
        }

        if( Penumbra.CollectionManager.HasCharacterCollections )
        {
            Enable();
        }
        else
        {
            Disable();
        }
    }
}