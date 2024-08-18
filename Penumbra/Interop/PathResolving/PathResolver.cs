using FFXIVClientStructs.FFXIV.Client.System.Resource;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Interop.Processing;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Interop.PathResolving;

public class PathResolver : IDisposable, IService
{
    private readonly PerformanceTracker _performance;
    private readonly Configuration _config;
    private readonly CollectionManager _collectionManager;
    private readonly ResourceLoader _loader;

    private readonly SubfileHelper _subfileHelper;
    private readonly PathState _pathState;
    private readonly MetaState _metaState;
    private readonly GameState _gameState;
    private readonly CollectionResolver _collectionResolver;
    private readonly GamePathPreProcessService _preprocessor;

    public PathResolver(PerformanceTracker performance, Configuration config, CollectionManager collectionManager, ResourceLoader loader,
        SubfileHelper subfileHelper, PathState pathState, MetaState metaState, CollectionResolver collectionResolver, GameState gameState,
        GamePathPreProcessService preprocessor)
    {
        _performance = performance;
        _config = config;
        _collectionManager = collectionManager;
        _subfileHelper = subfileHelper;
        _pathState = pathState;
        _metaState = metaState;
        _gameState = gameState;
        _preprocessor = preprocessor;
        _collectionResolver = collectionResolver;
        _loader = loader;
        _loader.ResolvePath = ResolvePath;
    }

    /// <summary> Try to resolve the given game path to the replaced path. </summary>
    public (FullPath?, ResolveData) ResolvePath(Utf8GamePath path, ResourceCategory category, ResourceType resourceType)
    {
        // Check if mods are enabled or if we are in a inc-ref at 0 reference count situation.
        if (!_config.EnableMods)
            return (null, ResolveData.Invalid);

        //TODO @Star - check for state validater where applicable, otherwise will break execution.

        // Do not allow manipulating layers to prevent very obvious cheating and softlocks.
        //if (resourceType is ResourceType.Lvb or ResourceType.Lgb or ResourceType.Sgb)
        //return (null, ResolveData.Invalid);

        return category switch
        {
            // Only Interface collection.
            ResourceCategory.Ui => ResolveUi(path),
            // Never allow changing scripts.
            ResourceCategory.UiScript => (null, ResolveData.Invalid),
            ResourceCategory.GameScript => (null, ResolveData.Invalid),
            // Use actual resolving.
            ResourceCategory.Chara => Resolve(path, resourceType),
            ResourceCategory.Shader => ResolveShader(path, resourceType),
            ResourceCategory.Vfx => Resolve(path, resourceType),
            ResourceCategory.Sound => Resolve(path, resourceType),
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
            ResourceCategory.Bg => DefaultResolver(path),
            ResourceCategory.Cut => DefaultResolver(path),
            ResourceCategory.Music => DefaultResolver(path),
            _ => DefaultResolver(path),
        };
    }

    /// <remarks> Replacing the characterstockings.shpk or the characterocclusion.shpk files currently causes crashes, so we just entirely prevent that. </remarks>
    private (FullPath?, ResolveData) ResolveShader(Utf8GamePath gamePath, ResourceType type)
    {
        if (type is not ResourceType.Shpk)
            return Resolve(gamePath, type);

        if (gamePath.Path.EndsWith("occlusion.shpk"u8)
         || gamePath.Path.EndsWith("stockings.shpk"u8))
            return (null, ResolveData.Invalid);

        return Resolve(gamePath, type);
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
         || _gameState.HandleFiles(_collectionResolver, type, gamePath, out resolveData)
         || _metaState.HandleDecalFile(type, gamePath, out resolveData);
        if (!nonDefault || !resolveData.Valid)
            resolveData = _collectionManager.Active.Default.ToResolveData();

        // Resolve using character/default collection first, otherwise forced, as usual.
        var resolved = resolveData.ModCollection.ResolvePath(gamePath);

        // Since mtrl files load their files separately, we need to add the new, resolved path
        // so that the functions loading tex and shpk can find that path and use its collection.
        // We also need to handle defaulted materials against a non-default collection.
        var path = resolved == null ? gamePath.Path : resolved.Value.InternalName;
        return _preprocessor.PreProcess(resolveData, path, nonDefault, type, resolved, gamePath);
    }

    public void Dispose()
    {
        _loader.ResetResolvePath();
    }

    /// <summary> Use the default method of path replacement. </summary>
    private (FullPath?, ResolveData) DefaultResolver(Utf8GamePath path)
    {
        var resolved = _collectionManager.Active.Default.ResolvePath(path);
        return (resolved, _collectionManager.Active.Default.ToResolveData());
    }

    /// <summary> Resolve a path from the interface collection. </summary>
    private (FullPath?, ResolveData) ResolveUi(Utf8GamePath path)
        => (_collectionManager.Active.Interface.ResolvePath(path),
            _collectionManager.Active.Interface.ToResolveData());
}
