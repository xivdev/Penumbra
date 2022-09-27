using System;
using System.Diagnostics.CodeAnalysis;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using CustomizeData = Penumbra.GameData.Structs.CustomizeData;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    [Signature( "0F B7 0D ?? ?? ?? ?? C7 85", ScanType = ScanType.StaticAddress )]
    private static ushort* _inspectTitleId = null!;

    // Obtain the name of the current player, if one exists.
    private static string? GetPlayerName()
        => Dalamud.Objects[ 0 ]?.Name.ToString();

    // Obtain the name of the inspect target from its window, if it exists.
    private static string? GetInspectName()
    {
        if( !Penumbra.Config.UseCharacterCollectionInInspect )
        {
            return null;
        }

        var addon = Dalamud.GameGui.GetAddonByName( "CharacterInspect", 1 );
        if( addon == IntPtr.Zero )
        {
            return null;
        }

        var ui     = ( AtkUnitBase* )addon;
        var nodeId = Dalamud.GameData.GetExcelSheet< Title >()?.GetRow( *_inspectTitleId )?.IsPrefix == true ? 7u : 6u;

        var text = ( AtkTextNode* )ui->UldManager.SearchNodeById( nodeId );
        return text != null && text->AtkResNode.Type == NodeType.Text ? text->NodeText.ToString() : null;
    }

    // Obtain the name displayed in the Character Card from the agent.
    private static string? GetCardName()
    {
        // TODO: Update to ClientStructs when merged.
        if( !Penumbra.Config.UseCharacterCollectionsInCards )
        {
            return null;
        }

        var agent = AgentCharaCard.Instance();
        if( agent == null )
        {
            return null;
        }

        var data = *( byte** )( ( byte* )agent + 0x28 );
        if( data == null )
        {
            return null;
        }

        var block = data + 0x7A;
        return new Utf8String( block ).ToString();
    }

    // Obtain the name of the player character if the glamour plate edit window is open.
    private static string? GetGlamourName()
    {
        if( !Penumbra.Config.UseCharacterCollectionInTryOn )
        {
            return null;
        }

        var addon = Dalamud.GameGui.GetAddonByName( "MiragePrismMiragePlate", 1 );
        return addon == IntPtr.Zero ? null : GetPlayerName();
    }

    // Guesstimate whether an unnamed cutscene actor corresponds to the player or not,
    // and if so, return the player name.
    private static string? GetCutsceneName( GameObject* gameObject )
    {
        if( gameObject->Name[ 0 ] != 0 || gameObject->ObjectKind != ( byte )ObjectKind.Player )
        {
            return null;
        }

        var parent = Cutscenes[ gameObject->ObjectIndex ];
        if( parent != null )
        {
            return parent.Name.ToString();
        }

        // should not really happen but keep it in as a emergency case.
        var player = Dalamud.Objects[ 0 ];
        if( player == null )
        {
            return null;
        }

        var customize1 = ( CustomizeData* )( ( Character* )gameObject )->CustomizeData;
        var customize2 = ( CustomizeData* )( ( Character* )player.Address )->CustomizeData;
        return customize1->Equals( *customize2 ) ? player.Name.ToString() : null;
    }

    // Identify the owner of a companion, mount or monster and apply the corresponding collection.
    // Companions and mounts get set to the actor before them in the table if it exists.
    // Monsters with a owner use that owner if it exists.
    private static string? GetOwnerName( GameObject* gameObject )
    {
        if( !Penumbra.Config.UseOwnerNameForCharacterCollection )
        {
            return null;
        }

        GameObject* owner = null;
        if( ( ObjectKind )gameObject->GetObjectKind() is ObjectKind.Companion or ObjectKind.MountType && gameObject->ObjectIndex > 0 )
        {
            owner = ( GameObject* )Dalamud.Objects[ gameObject->ObjectIndex - 1 ]?.Address;
        }
        else if( gameObject->OwnerID != 0xE0000000 )
        {
            owner = ( GameObject* )( Dalamud.Objects.SearchById( gameObject->OwnerID )?.Address ?? IntPtr.Zero );
        }

        if( owner != null )
        {
            return new Utf8String( owner->Name ).ToString();
        }

        return null;
    }

    // Identify the correct collection for a GameObject by index and name.
    private static ResolveData IdentifyCollection( GameObject* gameObject )
    {
        if( gameObject == null )
        {
            return new ResolveData( Penumbra.CollectionManager.Default );
        }

        try
        {
            // Login screen. Names are populated after actors are drawn,
            // so it is not possible to fetch names from the ui list.
            // Actors are also not named. So use Yourself > Players > Racial > Default.
            if( !Dalamud.ClientState.IsLoggedIn )
            {
                var collection = Penumbra.CollectionManager.ByType( CollectionType.Yourself )
                 ?? ( CollectionByActor( string.Empty, gameObject, out var c ) ? c : Penumbra.CollectionManager.Default );
                return collection.ToResolveData( gameObject );
            }
            else
            {
                // Housing Retainers
                if( Penumbra.Config.UseDefaultCollectionForRetainers
                && gameObject->ObjectKind == ( byte )ObjectKind.EventNpc
                && gameObject->DataID is 1011832 or 1011021 ) // cf. "E8 ?? ?? ?? ?? 0F B6 F8 88 45", male or female retainer
                {
                    return Penumbra.CollectionManager.Default.ToResolveData( gameObject );
                }

                string? actorName = null;
                if( Penumbra.Config.PreferNamedCollectionsOverOwners )
                {
                    // Early return if we prefer the actors own name over its owner.
                    actorName = new Utf8String( gameObject->Name ).ToString();
                    if( actorName.Length > 0
                    && CollectionByActorName( actorName, out var actorCollection ) )
                    {
                        return actorCollection.ToResolveData( gameObject );
                    }
                }

                // All these special cases are relevant for an empty name, so never collide with the above setting.
                // Only OwnerName can be applied to something with a non-empty name, and that is the specific case we want to handle.
                var actualName = gameObject->ObjectIndex switch
                    {
                        240 => Penumbra.Config.UseCharacterCollectionInMainWindow ? GetPlayerName() : null, // character window
                        241 => GetInspectName() ?? GetCardName() ?? GetGlamourName(), // inspect, character card, glamour plate editor.
                        242 => Penumbra.Config.UseCharacterCollectionInTryOn ? GetPlayerName() : null, // try-on
                        243 => Penumbra.Config.UseCharacterCollectionInTryOn ? GetPlayerName() : null, // dye preview
                        244 => Penumbra.Config.UseCharacterCollectionsInCards ? GetPlayerName() : null, // portrait list and editor
                        >= CutsceneCharacters.CutsceneStartIdx and < CutsceneCharacters.CutsceneEndIdx => GetCutsceneName( gameObject ),
                        _ => null,
                    }
                 ?? GetOwnerName( gameObject ) ?? actorName ?? new Utf8String( gameObject->Name ).ToString();

                // First check temporary character collections, then the own configuration, then special collections.
                var collection = CollectionByActorName( actualName, out var c )
                    ? c
                    : CollectionByActor( actualName, gameObject, out c )
                        ? c
                        : Penumbra.CollectionManager.Default;
                return collection.ToResolveData( gameObject );
            }
        }
        catch( Exception e )
        {
            Penumbra.Log.Error( $"Error identifying collection:\n{e}" );
            return Penumbra.CollectionManager.Default.ToResolveData( gameObject );
        }
    }

    // Get the collection applying to the current player character
    // or the default collection if no player exists.
    public static ModCollection PlayerCollection()
    {
        var player = Dalamud.ClientState.LocalPlayer;
        if( player == null )
        {
            return Penumbra.CollectionManager.Default;
        }

        var name = player.Name.TextValue;
        if( CollectionByActorName( name, out var c ) )
        {
            return c;
        }

        if( CollectionByActor( name, ( GameObject* )player.Address, out c ) )
        {
            return c;
        }

        return Penumbra.CollectionManager.Default;
    }

    // Check both temporary and permanent character collections. Temporary first.
    private static bool CollectionByActorName( string name, [NotNullWhen( true )] out ModCollection? collection )
        => Penumbra.TempMods.Collections.TryGetValue( name, out collection )
         || Penumbra.CollectionManager.Characters.TryGetValue( name, out collection );

    // Check special collections given the actor.
    private static bool CollectionByActor( string name, GameObject* actor, [NotNullWhen( true )] out ModCollection? collection )
    {
        collection = null;
        // Check for the Yourself collection.
        if( actor->ObjectIndex                            == 0
        || Cutscenes.GetParentIndex( actor->ObjectIndex ) == 0
        || name                                           == Dalamud.ClientState.LocalPlayer?.Name.ToString() )
        {
            collection = Penumbra.CollectionManager.ByType( CollectionType.Yourself );
            if( collection != null )
            {
                return true;
            }
        }

        if( actor->IsCharacter() )
        {
            var character = ( Character* )actor;
            // Only handle human models.
            if( character->ModelCharaId == 0 )
            {
                var race   = ( SubRace )character->CustomizeData[ 4 ];
                var gender = ( Gender )( character->CustomizeData[ 1 ] + 1 );
                var isNpc  = actor->ObjectKind != ( byte )ObjectKind.Player;

                var type = CollectionTypeExtensions.FromParts( race, gender, isNpc );
                collection =   Penumbra.CollectionManager.ByType( type );
                collection ??= Penumbra.CollectionManager.ByType( CollectionTypeExtensions.FromParts( gender, isNpc ) );
                if( collection != null )
                {
                    return true;
                }
            }
        }

        return false;
    }
}