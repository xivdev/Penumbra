using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
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
    public bool Enabled { get; private set; }

    private readonly ResourceLoader _loader;
    private static readonly CutsceneCharacters Cutscenes = new();
    private static readonly DrawObjectState DrawObjects = new();
    private readonly AnimationState _animations;
    private readonly PathState _paths;
    private readonly MetaState _meta;
    private readonly MaterialState _materials;

    public unsafe PathResolver( ResourceLoader loader )
    {
        SignatureHelper.Initialise( this );
        _loader = loader;
        _animations = new AnimationState( DrawObjects );
        _paths = new PathState( this );
        _meta = new MetaState( this, _paths.HumanVTable );
        _materials = new MaterialState( _paths );
    }

    // The modified resolver that handles game path resolving.
    private bool CharacterResolver( Utf8GamePath gamePath, ResourceCategory _1, ResourceType type, int _2, out (IntPtr, FullPath?, object?) data )
    {
        // Check if the path was marked for a specific collection,
        // or if it is a file loaded by a material, and if we are currently in a material load,
        // or if it is a face decal path and the current mod collection is set.
        // If not use the default collection.
        // We can remove paths after they have actually been loaded.
        // A potential next request will add the path anew.
        var nonDefault = _materials.HandleSubFiles( type, out var collection )
         || _paths.Consume( gamePath.Path, out collection )
         || _animations.HandleFiles( type, gamePath, out collection )
         || DrawObjects.HandleDecalFile( type, gamePath, out collection );
        if( !nonDefault || collection.Item2 == null )
        {
            collection = (IntPtr.Zero, Penumbra.CollectionManager.Default);
        }

        // Resolve using character/default collection first, otherwise forced, as usual.
        var resolved = collection.Item2.ResolvePath( gamePath );

        // Since mtrl files load their files separately, we need to add the new, resolved path
        // so that the functions loading tex and shpk can find that path and use its collection.
        // We also need to handle defaulted materials against a non-default collection.
        var path = resolved == null ? gamePath.Path.ToString() : resolved.Value.FullName;
        _materials.HandleCollection( collection, path, nonDefault, type, resolved, out data );
        return true;
    }

    public void Enable()
    {
        if( Enabled )
        {
            return;
        }

        Enabled = true;
        Cutscenes.Enable();
        DrawObjects.Enable();
        _animations.Enable();
        _paths.Enable();
        _meta.Enable();
        _materials.Enable();

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
        _animations.Disable();
        DrawObjects.Disable();
        Cutscenes.Disable();
        _paths.Disable();
        _meta.Disable();
        _materials.Disable();

        _loader.ResolvePathCustomization -= CharacterResolver;
        PluginLog.Debug( "Character Path Resolver disabled." );
    }

    public void Dispose()
    {
        Disable();
        _paths.Dispose();
        _animations.Dispose();
        DrawObjects.Dispose();
        Cutscenes.Dispose();
        _meta.Dispose();
        _materials.Dispose();
    }

    public static unsafe (IntPtr, ModCollection) IdentifyDrawObject( IntPtr drawObject )
    {
        var parent = FindParent( drawObject, out var collection );
        return (( IntPtr )parent, collection.Item2);
    }

    public int CutsceneActor( int idx )
        => Cutscenes.GetParentIndex( idx );

    // Use the stored information to find the GameObject and Collection linked to a DrawObject.
    public static unsafe GameObject* FindParent( IntPtr drawObject, out (IntPtr, ModCollection) collection )
    {
        if( DrawObjects.TryGetValue( drawObject, out var data, out var gameObject ) )
        {
            collection = (drawObject, data.Item1);
            return gameObject;
        }

        if( DrawObjects.LastGameObject != null
        && ( DrawObjects.LastGameObject->DrawObject == null || DrawObjects.LastGameObject->DrawObject == ( DrawObject* )drawObject ) )
        {
            collection = IdentifyCollection( DrawObjects.LastGameObject );
            return DrawObjects.LastGameObject;
        }

        collection = IdentifyCollection( null );
        return null;
    }

    private static unsafe (IntPtr, ModCollection?) GetCollection( IntPtr drawObject )
    {
        var parent = FindParent( drawObject, out var collection );
        if( parent == null || collection.Item2 == Penumbra.CollectionManager.Default )
        {
            return (drawObject, null);
        }

        return collection.Item2.HasCache ? collection : (drawObject, null);
    }

    internal IEnumerable<KeyValuePair<Utf8String, ModCollection?>> PathCollections
        => _paths.Paths.Select( p => new KeyValuePair<Utf8String, ModCollection?>( p.Key, p.Value.Item2 ) );

    internal IEnumerable<KeyValuePair<IntPtr, (ModCollection, int)>> DrawObjectMap
        => DrawObjects.DrawObjects;

    internal IEnumerable<KeyValuePair<int, global::Dalamud.Game.ClientState.Objects.Types.GameObject>> CutsceneActors
        => Cutscenes.Actors;
}