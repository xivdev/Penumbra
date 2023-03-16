using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Api;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Resolver;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Loader;

public class CharacterResolver : IDisposable
{
    private readonly Configuration         _config;
    private readonly ModCollection.Manager _collectionManager;
    private readonly TempCollectionManager _tempCollections;
    private readonly ResourceLoader        _loader;
    private readonly PathResolver          _pathResolver;

    public unsafe CharacterResolver(Configuration config, ModCollection.Manager collectionManager, TempCollectionManager tempCollections,
        ResourceLoader loader, PathResolver pathResolver)
    {
        _config            = config;
        _collectionManager = collectionManager;
        _tempCollections   = tempCollections;
        _loader            = loader;
        _pathResolver      = pathResolver;

        _loader.ResolvePath =  ResolvePath;
        _loader.FileLoaded  += ImcLoadResource;
    }

    /// <summary> Obtain a temporary or permanent collection by name. </summary>
    public bool CollectionByName(string name, [NotNullWhen(true)] out ModCollection? collection)
        => _tempCollections.CollectionByName(name, out collection) || _collectionManager.ByName(name, out collection);

    /// <summary> Try to resolve the given game path to the replaced path. </summary>
    public (FullPath?, ResolveData) ResolvePath(Utf8GamePath path, ResourceCategory category, ResourceType resourceType)
    {
        // Check if mods are enabled or if we are in a inc-ref at 0 reference count situation.
        if (!_config.EnableMods)
            return (null, ResolveData.Invalid);

        path = path.ToLower();
        return category switch
        {
            // Only Interface collection.
            ResourceCategory.Ui => (_collectionManager.Interface.ResolvePath(path),
                _collectionManager.Interface.ToResolveData()),
            // Never allow changing scripts.
            ResourceCategory.UiScript   => (null, ResolveData.Invalid),
            ResourceCategory.GameScript => (null, ResolveData.Invalid),
            // Use actual resolving.
            ResourceCategory.Chara  => _pathResolver.CharacterResolver(path, resourceType),
            ResourceCategory.Shader => _pathResolver.CharacterResolver(path, resourceType),
            ResourceCategory.Vfx    => _pathResolver.CharacterResolver(path, resourceType),
            ResourceCategory.Sound  => _pathResolver.CharacterResolver(path, resourceType),
            // None of these files are ever associated with specific characters,
            // always use the default resolver for now.
            ResourceCategory.Common   => DefaultResolver(path),
            ResourceCategory.BgCommon => DefaultResolver(path),
            ResourceCategory.Bg       => DefaultResolver(path),
            ResourceCategory.Cut      => DefaultResolver(path),
            ResourceCategory.Exd      => DefaultResolver(path),
            ResourceCategory.Music    => DefaultResolver(path),
            _                         => DefaultResolver(path),
        };
    }

    // TODO
    public unsafe void Dispose()
    {
        _loader.ResetResolvePath();
        _loader.FileLoaded -= ImcLoadResource;
        _pathResolver.Dispose();
    }

    // Use the default method of path replacement.
    private (FullPath?, ResolveData) DefaultResolver(Utf8GamePath path)
    {
        var resolved = _collectionManager.Default.ResolvePath(path);
        return (resolved, _collectionManager.Default.ToResolveData());
    }

    /// <summary> After loading an IMC file, replace its contents with the modded IMC file. </summary>
    private unsafe void ImcLoadResource(ResourceHandle* resource, ByteString path, bool returnValue, bool custom, ByteString additionalData)
    {
        if (resource->FileType != ResourceType.Imc)
            return;

        var lastUnderscore = additionalData.LastIndexOf((byte)'_');
        var name           = lastUnderscore == -1 ? additionalData.ToString() : additionalData.Substring(0, lastUnderscore).ToString();
        if (Utf8GamePath.FromByteString(path, out var gamePath)
         && CollectionByName(name, out var collection)
         && collection.HasCache
         && collection.GetImcFile(gamePath, out var file))
        {
            file.Replace(resource);
            Penumbra.Log.Verbose(
                $"[ResourceLoader] Loaded {gamePath} from file and replaced with IMC from collection {collection.AnonymizedName}.");
        }
    }
}
