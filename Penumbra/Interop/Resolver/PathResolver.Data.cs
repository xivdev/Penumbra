using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    // Keep track of created DrawObjects that are CharacterBase,
    // and use the last game object that called EnableDraw to link them.
    public delegate IntPtr CharacterBaseCreateDelegate( uint a, IntPtr b, IntPtr c, byte d );

    [Signature( "E8 ?? ?? ?? ?? 48 85 C0 74 21 C7 40" )]
    public Hook< CharacterBaseCreateDelegate >? CharacterBaseCreateHook;

    private IntPtr CharacterBaseCreateDetour( uint a, IntPtr b, IntPtr c, byte d )
    {
        using var cmp = MetaChanger.ChangeCmp( this, out var collection );
        var       ret = CharacterBaseCreateHook!.Original( a, b, c, d );
        if( LastGameObject != null )
        {
            DrawObjectToObject[ ret ] = ( collection!, LastGameObject->ObjectIndex );
        }

        return ret;
    }


    // Remove DrawObjects from the list when they are destroyed.
    public delegate void CharacterBaseDestructorDelegate( IntPtr drawBase );

    [Signature( "E8 ?? ?? ?? ?? 40 F6 C7 01 74 3A 40 F6 C7 04 75 27 48 85 DB 74 2F 48 8B 05 ?? ?? ?? ?? 48 8B D3 48 8B 48 30",
        DetourName = "CharacterBaseDestructorDetour" )]
    public Hook< CharacterBaseDestructorDelegate >? CharacterBaseDestructorHook;

    private void CharacterBaseDestructorDetour( IntPtr drawBase )
    {
        DrawObjectToObject.Remove( drawBase );
        CharacterBaseDestructorHook!.Original.Invoke( drawBase );
    }


    // EnableDraw is what creates DrawObjects for gameObjects,
    // so we always keep track of the current GameObject to be able to link it to the DrawObject.
    public delegate void EnableDrawDelegate( IntPtr gameObject, IntPtr b, IntPtr c, IntPtr d );

    [Signature( "E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 85 C9 74 ?? 33 D2 E8 ?? ?? ?? ?? 84 C0" )]
    public Hook< EnableDrawDelegate >? EnableDrawHook;

    private void EnableDrawDetour( IntPtr gameObject, IntPtr b, IntPtr c, IntPtr d )
    {
        LastGameObject = ( GameObject* )gameObject;
        EnableDrawHook!.Original.Invoke( gameObject, b, c, d );
        LastGameObject = null;
    }

    private void EnableDataHooks()
    {
        CharacterBaseCreateHook?.Enable();
        EnableDrawHook?.Enable();
        CharacterBaseDestructorHook?.Enable();
        Penumbra.CollectionManager.CollectionChanged += CheckCollections;
    }

    private void DisableDataHooks()
    {
        Penumbra.CollectionManager.CollectionChanged -= CheckCollections;
        CharacterBaseCreateHook?.Disable();
        EnableDrawHook?.Disable();
        CharacterBaseDestructorHook?.Disable();
    }

    private void DisposeDataHooks()
    {
        CharacterBaseCreateHook?.Dispose();
        EnableDrawHook?.Dispose();
        CharacterBaseDestructorHook?.Dispose();
    }


    // This map links DrawObjects directly to Actors (by ObjectTable index) and their collections.
    // It contains any DrawObjects that correspond to a human actor, even those without specific collections.
    internal readonly Dictionary< IntPtr, (ModCollection, int) > DrawObjectToObject = new();

    // This map links files to their corresponding collection, if it is non-default.
    internal readonly ConcurrentDictionary< Utf8String, ModCollection > PathCollections = new();

    internal GameObject* LastGameObject = null;

    // Check that a linked DrawObject still corresponds to the correct actor and that it still exists, otherwise remove it.
    private bool VerifyEntry( IntPtr drawObject, int gameObjectIdx, out GameObject* gameObject )
    {
        var tmp = Dalamud.Objects[ gameObjectIdx ];
        if( tmp != null )
        {
            gameObject = ( GameObject* )tmp.Address;
            if( gameObject->DrawObject == ( DrawObject* )drawObject )
            {
                return true;
            }
        }

        gameObject = null;
        DrawObjectToObject.Remove( drawObject );
        return false;
    }

    // Obtain the name of the current player, if one exists.
    private static string? GetPlayerName()
        => Dalamud.Objects[ 0 ]?.Name.ToString();

    // Obtain the name of the inspect target from its window, if it exists.
    private static string? GetInspectName()
    {
        var addon = Dalamud.GameGui.GetAddonByName( "CharacterInspect", 1 );
        if( addon == IntPtr.Zero )
        {
            return null;
        }

        var ui = ( AtkUnitBase* )addon;
        if( ui->UldManager.NodeListCount < 60 )
        {
            return null;
        }

        var text = ( AtkTextNode* )ui->UldManager.NodeList[ 60 ];
        return text != null ? text->NodeText.ToString() : null;
    }

    // Guesstimate whether an unnamed cutscene actor corresponds to the player or not,
    // and if so, return the player name.
    private static string? GetCutsceneName( GameObject* gameObject )
    {
        if( gameObject->Name[ 0 ] != 0 || gameObject->ObjectKind != ( byte )ObjectKind.Player )
        {
            return null;
        }

        var player = Dalamud.Objects[ 0 ];
        if( player == null )
        {
            return null;
        }

        var pc = ( Character* )player.Address;
        return pc->ClassJob == ( ( Character* )gameObject )->ClassJob ? player.Name.ToString() : null;
    }

    // Identify the correct collection for a GameObject by index and name.
    private static ModCollection IdentifyCollection( GameObject* gameObject )
    {
        if( gameObject == null )
        {
            return Penumbra.CollectionManager.Default;
        }

        var name = gameObject->ObjectIndex switch
            {
                240    => GetPlayerName(),  // character window
                241    => GetInspectName(), // inspect
                242    => GetPlayerName(),  // try-on
                >= 200 => GetCutsceneName( gameObject ),
                _      => null,
            }
         ?? new Utf8String( gameObject->Name ).ToString();

        return Penumbra.CollectionManager.Character( name );
    }

    // Update collections linked to Game/DrawObjects due to a change in collection configuration.
    private void CheckCollections( ModCollection.Type type, ModCollection? _1, ModCollection? _2, string? name )
    {
        if( type is not (ModCollection.Type.Character or ModCollection.Type.Default) )
        {
            return;
        }

        foreach( var (key, (_, idx)) in DrawObjectToObject.ToArray() )
        {
            if( !VerifyEntry( key, idx, out var obj ) )
            {
                DrawObjectToObject.Remove( key );
            }

            var newCollection = IdentifyCollection( obj );
            DrawObjectToObject[ key ] = ( newCollection, idx );
        }
    }

    // Use the stored information to find the GameObject and Collection linked to a DrawObject.
    private GameObject* FindParent( IntPtr drawObject, out ModCollection collection )
    {
        if( DrawObjectToObject.TryGetValue( drawObject, out var data ) )
        {
            var gameObjectIdx = data.Item2;
            if( VerifyEntry( drawObject, gameObjectIdx, out var gameObject ) )
            {
                collection = data.Item1;
                return gameObject;
            }
        }

        if( LastGameObject != null && ( LastGameObject->DrawObject == null || LastGameObject->DrawObject == ( DrawObject* )drawObject ) )
        {
            collection = IdentifyCollection( LastGameObject );
            return LastGameObject;
        }


        collection = IdentifyCollection( null );
        return null;
    }


    // Special handling for paths so that we do not store non-owned temporary strings in the dictionary.
    private void SetCollection( Utf8String path, ModCollection collection )
    {
        if( PathCollections.ContainsKey( path ) || path.IsOwned )
        {
            PathCollections[ path ] = collection;
        }
        else
        {
            PathCollections[ path.Clone() ] = collection;
        }
    }

    // Find all current DrawObjects used in the GameObject table.
    private void InitializeDrawObjects()
    {
        foreach( var gameObject in Dalamud.Objects )
        {
            var ptr = ( GameObject* )gameObject.Address;
            if( ptr->IsCharacter() && ptr->DrawObject != null )
            {
                DrawObjectToObject[ ( IntPtr )ptr->DrawObject ] = ( IdentifyCollection( ptr ), ptr->ObjectIndex );
            }
        }
    }
}