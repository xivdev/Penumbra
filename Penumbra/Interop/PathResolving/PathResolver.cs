using System;
using System.Diagnostics.CodeAnalysis;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Enums;
using Penumbra.Interop.ResourceLoading;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Interop.PathResolving;

public class PathResolver : IDisposable
{
    private readonly PerformanceTracker    _performance;
    private readonly Configuration         _config;
    private readonly CollectionManager     _collectionManager;
    private readonly TempCollectionManager _tempCollections;
    private readonly ResourceLoader        _loader;

    private readonly AnimationHookService _animationHookService;
    private readonly SubfileHelper        _subfileHelper;
    private readonly PathState            _pathState;
    private readonly MetaState            _metaState;

    public unsafe PathResolver(PerformanceTracker performance, Configuration config, CollectionManager collectionManager,
        TempCollectionManager tempCollections, ResourceLoader loader, AnimationHookService animationHookService, SubfileHelper subfileHelper,
        PathState pathState, MetaState metaState)
    {
        _performance          =  performance;
        _config               =  config;
        _collectionManager    =  collectionManager;
        _tempCollections      =  tempCollections;
        _animationHookService =  animationHookService;
        _subfileHelper        =  subfileHelper;
        _pathState            =  pathState;
        _metaState            =  metaState;
        _loader               =  loader;
        _loader.ResolvePath   =  ResolvePath;
        _loader.FileLoaded    += ImcLoadResource;
    }

    /// <summary> Obtain a temporary or permanent collection by name. </summary>
    public bool CollectionByName(string name, [NotNullWhen(true)] out ModCollection? collection)
        => _tempCollections.CollectionByName(name, out collection) || _collectionManager.Storage.ByName(name, out collection);

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
            ResourceCategory.Ui => ResolveUi(path),
            // Never allow changing scripts.
            ResourceCategory.UiScript   => (null, ResolveData.Invalid),
            ResourceCategory.GameScript => (null, ResolveData.Invalid),
            // Use actual resolving.
            ResourceCategory.Chara  => Resolve(path, resourceType),
            ResourceCategory.Shader => Resolve(path, resourceType),
            ResourceCategory.Vfx    => Resolve(path, resourceType),
            ResourceCategory.Sound  => Resolve(path, resourceType),
            // EXD Modding in general should probably be prohibited but is currently used for fan translations.
            // We prevent WebURL specifically because it technically allows launching arbitrary programs / to execute arbitrary code.
            ResourceCategory.Exd => path.Path.StartsWith("exd/weburl"u8)
                ? (null, ResolveData.Invalid)
                : DefaultResolver(path),
            // None of these files are ever associated with specific characters,
            // always use the default resolver for now,
            // except that common/font is conceptually more UI.
            ResourceCategory.Common => path.Path.StartsWith("common/font"u8)
                ? ResolveUi(path)
                : DefaultResolver(path),
            ResourceCategory.BgCommon => DefaultResolver(path),
            ResourceCategory.Bg       => DefaultResolver(path),
            ResourceCategory.Cut      => DefaultResolver(path),
            ResourceCategory.Music    => DefaultResolver(path),
            _                         => DefaultResolver(path),
        };
    }

    public (FullPath?, ResolveData) Resolve(Utf8GamePath gamePath, ResourceType type)
    {
        using var performance = _performance.Measure(PerformanceType.CharacterResolver);
        // Check if the path was marked for a specific collection,
        // or if it is a file loaded by a material, and if we are currently in a material load,
        // or if it is a face decal path and the current mod collection is set.
        // If not use the default collection.
        // We can remove paths after they have actually been loaded.
        // A potential next request will add the path anew.
        var nonDefault = _subfileHelper.HandleSubFiles(type, out var resolveData)
         || _pathState.Consume(gamePath.Path, out resolveData)
         || _animationHookService.HandleFiles(type, gamePath, out resolveData)
         || _metaState.HandleDecalFile(type, gamePath, out resolveData);
        if (!nonDefault || !resolveData.Valid)
            resolveData = _collectionManager.Active.Default.ToResolveData();

        // Resolve using character/default collection first, otherwise forced, as usual.
        var resolved = resolveData.ModCollection.ResolvePath(gamePath);

        // Since mtrl files load their files separately, we need to add the new, resolved path
        // so that the functions loading tex and shpk can find that path and use its collection.
        // We also need to handle defaulted materials against a non-default collection.
        var path = resolved == null ? gamePath.Path : resolved.Value.InternalName;
        SubfileHelper.HandleCollection(resolveData, path, nonDefault, type, resolved, out var pair);
        return pair;
    }

    public unsafe void Dispose()
    {
        _loader.ResetResolvePath();
        _loader.FileLoaded -= ImcLoadResource;
    }

    /// <summary> Use the default method of path replacement. </summary>
    private (FullPath?, ResolveData) DefaultResolver(Utf8GamePath path)
    {
        var resolved = _collectionManager.Active.Default.ResolvePath(path);
        return (resolved, _collectionManager.Active.Default.ToResolveData());
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

    /// <summary> Resolve a path from the interface collection. </summary>
    private (FullPath?, ResolveData) ResolveUi(Utf8GamePath path)
        => (_collectionManager.Active.Interface.ResolvePath(path),
            _collectionManager.Active.Interface.ToResolveData());
}
