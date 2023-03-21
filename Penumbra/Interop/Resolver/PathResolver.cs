using System;
using System.Collections;
using System.Collections.Generic;
using Dalamud.Game.ClientState;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Loader;
using Penumbra.Interop.Services;
using Penumbra.Services;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.Interop.Resolver;

//public class PathResolver2 : IDisposable
//{
//    public readonly CutsceneService           Cutscenes;
//    public readonly IdentifiedCollectionCache Identified;
//
//    public PathResolver(StartTracker timer, CutsceneService cutscenes, IdentifiedCollectionCache identified)
//    {
//        using var t = timer.Measure(StartTimeType.PathResolver);
//        Cutscenes  = cutscenes;
//        Identified = identified;
//    }
//}


// The Path Resolver handles character collections.
// It will hook any path resolving functions for humans,
// as well as DrawObject creation.
// It links draw objects to actors, and actors to character collections,
// to resolve paths for character collections.
public partial class PathResolver : IDisposable
{
    public bool Enabled { get; private set; }

    private readonly        CommunicatorService       _communicator;
    private readonly        ResourceLoader            _loader;
    private static readonly CutsceneService        Cutscenes   = new(DalamudServices.SObjects, Penumbra.GameEvents); // TODO
    private static          DrawObjectState           _drawObjects = null!;                                             // TODO
    private static readonly BitArray                  ValidHumanModels;
    internal static         IdentifiedCollectionCache IdentifiedCache = null!; // TODO
    private readonly        AnimationState            _animations;
    private readonly        PathState                 _paths;
    private readonly        MetaState                 _meta;
    private readonly        SubfileHelper             _subFiles;

    static PathResolver()
        => ValidHumanModels = GetValidHumanModels(DalamudServices.SGameData);

    public unsafe PathResolver(IdentifiedCollectionCache cache, StartTracker timer, ClientState clientState, CommunicatorService communicator, GameEventManager events, ResourceLoader loader)
    {
        using var tApi = timer.Measure(StartTimeType.PathResolver);
        _communicator   = communicator;
        IdentifiedCache = cache;
        SignatureHelper.Initialise(this);
        _drawObjects = new DrawObjectState(_communicator);
        _loader     = loader;
        _animations = new AnimationState(_drawObjects);
        _paths      = new PathState(this);
        _meta       = new MetaState(_paths.HumanVTable);
        _subFiles   = new SubfileHelper(_loader, Penumbra.GameEvents);
        Enable();
    }

    // The modified resolver that handles game path resolving.
    public (FullPath?, ResolveData) CharacterResolver(Utf8GamePath gamePath, ResourceType type)
    {
        using var performance = Penumbra.Performance.Measure(PerformanceType.CharacterResolver);
        // Check if the path was marked for a specific collection,
        // or if it is a file loaded by a material, and if we are currently in a material load,
        // or if it is a face decal path and the current mod collection is set.
        // If not use the default collection.
        // We can remove paths after they have actually been loaded.
        // A potential next request will add the path anew.
        var nonDefault = _subFiles.HandleSubFiles(type, out var resolveData)
         || _paths.Consume(gamePath.Path, out resolveData)
         || _animations.HandleFiles(type, gamePath, out resolveData)
         || _drawObjects.HandleDecalFile(type, gamePath, out resolveData);
        if (!nonDefault || !resolveData.Valid)
            resolveData = Penumbra.CollectionManager.Default.ToResolveData();

        // Resolve using character/default collection first, otherwise forced, as usual.
        var resolved = resolveData.ModCollection.ResolvePath(gamePath);

        // Since mtrl files load their files separately, we need to add the new, resolved path
        // so that the functions loading tex and shpk can find that path and use its collection.
        // We also need to handle defaulted materials against a non-default collection.
        var path = resolved == null ? gamePath.Path : resolved.Value.InternalName;
        SubfileHelper.HandleCollection(resolveData, path, nonDefault, type, resolved, out var pair);
        return pair;
    }

    public void Enable()
    {
        if (Enabled)
            return;

        Enabled = true;
        Cutscenes.Enable();
        _drawObjects.Enable();
        IdentifiedCache.Enable();
        _animations.Enable();
        _paths.Enable();
        _meta.Enable();
        _subFiles.Enable();

        Penumbra.Log.Debug("Character Path Resolver enabled.");
    }

    public void Disable()
    {
        if (!Enabled)
            return;

        Enabled = false;
        _animations.Disable();
        _drawObjects.Disable();
        Cutscenes.Disable();
        IdentifiedCache.Disable();
        _paths.Disable();
        _meta.Disable();
        _subFiles.Disable();

        Penumbra.Log.Debug("Character Path Resolver disabled.");
    }

    public void Dispose()
    {
        Disable();
        _paths.Dispose();
        _animations.Dispose();
        _drawObjects.Dispose();
        Cutscenes.Dispose();
        IdentifiedCache.Dispose();
        _meta.Dispose();
        _subFiles.Dispose();
    }

    public static unsafe (IntPtr, ResolveData) IdentifyDrawObject(IntPtr drawObject)
    {
        var parent = FindParent(drawObject, out var resolveData);
        return ((IntPtr)parent, resolveData);
    }

    public int CutsceneActor(int idx)
        => Cutscenes.GetParentIndex(idx);

    // Use the stored information to find the GameObject and Collection linked to a DrawObject.
    public static unsafe GameObject* FindParent(IntPtr drawObject, out ResolveData resolveData)
    {
        if (_drawObjects.TryGetValue(drawObject, out var data, out var gameObject))
        {
            resolveData = data.Item1;
            return gameObject;
        }

        if (_drawObjects.LastGameObject != null
         && (_drawObjects.LastGameObject->DrawObject == null || _drawObjects.LastGameObject->DrawObject == (DrawObject*)drawObject))
        {
            resolveData = IdentifyCollection(_drawObjects.LastGameObject, true);
            return _drawObjects.LastGameObject;
        }

        resolveData = IdentifyCollection(null, true);
        return null;
    }

    private static unsafe ResolveData GetResolveData(IntPtr drawObject)
    {
        var _ = FindParent(drawObject, out var resolveData);
        return resolveData;
    }

    internal IEnumerable<KeyValuePair<ByteString, ResolveData>> PathCollections
        => _paths.Paths;

    internal IEnumerable<KeyValuePair<IntPtr, (ResolveData, int)>> DrawObjectMap
        => _drawObjects.DrawObjects;

    internal IEnumerable<KeyValuePair<int, global::Dalamud.Game.ClientState.Objects.Types.GameObject>> CutsceneActors
        => Cutscenes.Actors;

    internal IEnumerable<KeyValuePair<IntPtr, ResolveData>> ResourceCollections
        => _subFiles;

    internal int SubfileCount
        => _subFiles.Count;

    internal ResolveData CurrentMtrlData
        => _subFiles.MtrlData;

    internal ResolveData CurrentAvfxData
        => _subFiles.AvfxData;

    internal ResolveData LastGameObjectData
        => _drawObjects.LastCreatedCollection;

    internal unsafe nint LastGameObject
        => (nint)_drawObjects.LastGameObject;
}
