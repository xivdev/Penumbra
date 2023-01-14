using System;
using System.Collections;
using System.Linq;
using Dalamud.Data;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using Penumbra.Collections;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.Util;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using CustomizeData = Penumbra.GameData.Structs.CustomizeData;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    private static ResolveData IdentifyMahjong( GameObject* gameObject )
    {
        static bool SearchPlayer( Character* character, int idx, out ActorIdentifier id )
        {
            var other = ( Character* )Dalamud.Objects.GetObjectAddress( idx );
            if( other == null || !CustomizeData.Equals( ( CustomizeData* )character->CustomizeData, ( CustomizeData* )other->CustomizeData ) )
            {
                id = ActorIdentifier.Invalid;
                return false;
            }

            id = Penumbra.Actors.FromObject( &other->GameObject, out _, false, true );
            return true;
        }

        static ActorIdentifier SearchPlayers( Character* gameObject, int idx1, int idx2, int idx3 )
            => SearchPlayer( gameObject, idx1, out var id ) || SearchPlayer( gameObject, idx2, out id ) || SearchPlayer( gameObject, idx3, out id )
                ? id
                : ActorIdentifier.Invalid;

        var identifier = gameObject->ObjectIndex switch
        {
            0   => Penumbra.Actors.GetCurrentPlayer(),
            2   => Penumbra.Actors.FromObject( gameObject, out _, false, true ),
            4   => Penumbra.Actors.FromObject( gameObject, out _, false, true ),
            6   => Penumbra.Actors.FromObject( gameObject, out _, false, true ),
            240 => Penumbra.Actors.GetCurrentPlayer(),
            241 => SearchPlayers( ( Character* )gameObject, 2, 4, 6 ),
            242 => SearchPlayers( ( Character* )gameObject, 4, 2, 6 ),
            243 => SearchPlayers( ( Character* )gameObject, 6, 2, 4 ),
            _   => ActorIdentifier.Invalid,
        };

        var collection = ( identifier.IsValid ? CollectionByIdentifier( identifier ) : null )
         ?? CheckYourself( identifier, gameObject )
         ?? CollectionByAttributes( gameObject )
         ?? Penumbra.CollectionManager.Default;

        return IdentifiedCache.Set( collection, identifier, gameObject );
    }


    // Identify the correct collection for a GameObject by index and name.
    public static ResolveData IdentifyCollection( GameObject* gameObject, bool useCache )
    {
        using var performance = Penumbra.Performance.Measure( PerformanceType.IdentifyCollection );

        if( gameObject == null )
        {
            return new ResolveData( Penumbra.CollectionManager.Default );
        }

        try
        {
            if( useCache && IdentifiedCache.TryGetValue( gameObject, out var data ) )
            {
                return data;
            }

            // Login screen. Names are populated after actors are drawn,
            // so it is not possible to fetch names from the ui list.
            // Actors are also not named. So use Yourself > Players > Racial > Default.
            if( !Dalamud.ClientState.IsLoggedIn )
            {
                var collection2 = Penumbra.CollectionManager.ByType( CollectionType.Yourself )
                 ?? CollectionByAttributes( gameObject )
                 ?? Penumbra.CollectionManager.Default;
                return IdentifiedCache.Set( collection2, ActorIdentifier.Invalid, gameObject );
            }

            // Mahjong special case.
            if( Dalamud.ClientState.TerritoryType == 831 )
            {
                return IdentifyMahjong( gameObject );
            }

            // Aesthetician. The relevant actor is yourself, so use player collection when possible.
            if( Dalamud.GameGui.GetAddonByName( "ScreenLog", 1 ) == IntPtr.Zero )
            {
                var player = Penumbra.Actors.GetCurrentPlayer();
                var collection2 = ( player.IsValid ? CollectionByIdentifier( player ) : null )
                 ?? Penumbra.CollectionManager.ByType( CollectionType.Yourself )
                 ?? CollectionByAttributes( gameObject )
                 ?? Penumbra.CollectionManager.Default;
                return IdentifiedCache.Set( collection2, ActorIdentifier.Invalid, gameObject );
            }

            var identifier = Penumbra.Actors.FromObject( gameObject, out var owner, true, false );
            if( Penumbra.Config.UseNoModsInInspect && identifier.Type == IdentifierType.Special && identifier.Special == ScreenActor.ExamineScreen )
            {
                return IdentifiedCache.Set( ModCollection.Empty, identifier, gameObject );
            }

            identifier = Penumbra.CollectionManager.Individuals.ConvertSpecialIdentifier( identifier );
            var collection = CollectionByIdentifier( identifier )
             ?? CheckYourself( identifier, gameObject )
             ?? CollectionByAttributes( gameObject )
             ?? CheckOwnedCollection( identifier, owner )
             ?? Penumbra.CollectionManager.Default;

            return IdentifiedCache.Set( collection, identifier, gameObject );
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
        using var performance = Penumbra.Performance.Measure( PerformanceType.IdentifyCollection );
        var       gameObject  = ( GameObject* )Dalamud.Objects.GetObjectAddress( 0 );
        if( gameObject == null )
        {
            return Penumbra.CollectionManager.ByType( CollectionType.Yourself )
             ?? Penumbra.CollectionManager.Default;
        }

        var player = Penumbra.Actors.GetCurrentPlayer();
        return CollectionByIdentifier( player )
         ?? CheckYourself( player, gameObject )
         ?? CollectionByAttributes( gameObject )
         ?? Penumbra.CollectionManager.Default;
    }

    // Check both temporary and permanent character collections. Temporary first.
    private static ModCollection? CollectionByIdentifier( ActorIdentifier identifier )
        => Penumbra.TempMods.Collections.TryGetCollection( identifier, out var collection )
         || Penumbra.CollectionManager.Individuals.TryGetCollection( identifier, out collection )
                ? collection
                : null;


    // Check for the Yourself collection.
    private static ModCollection? CheckYourself( ActorIdentifier identifier, GameObject* actor )
    {
        if( actor->ObjectIndex                            == 0
        || Cutscenes.GetParentIndex( actor->ObjectIndex ) == 0
        || identifier.Equals( Penumbra.Actors.GetCurrentPlayer() ) )
        {
            return Penumbra.CollectionManager.ByType( CollectionType.Yourself );
        }

        return null;
    }

    // Check special collections given the actor.
    private static ModCollection? CollectionByAttributes( GameObject* actor )
    {
        if( !actor->IsCharacter() )
        {
            return null;
        }

        // Only handle human models.
        var character = ( Character* )actor;
        if( character->ModelCharaId >= 0 && character->ModelCharaId < ValidHumanModels.Count && ValidHumanModels[ character->ModelCharaId ] )
        {
            var race   = ( SubRace )character->CustomizeData[ 4 ];
            var gender = ( Gender )( character->CustomizeData[ 1 ] + 1 );
            var isNpc  = actor->ObjectKind != ( byte )ObjectKind.Player;

            var type       = CollectionTypeExtensions.FromParts( race, gender, isNpc );
            var collection = Penumbra.CollectionManager.ByType( type );
            collection ??= Penumbra.CollectionManager.ByType( CollectionTypeExtensions.FromParts( gender, isNpc ) );
            return collection;
        }

        return null;
    }

    // Get the collection applying to the owner if it is available.
    private static ModCollection? CheckOwnedCollection( ActorIdentifier identifier, GameObject* owner )
    {
        if( identifier.Type != IdentifierType.Owned || !Penumbra.Config.UseOwnerNameForCharacterCollection || owner == null )
        {
            return null;
        }

        var id = Penumbra.Actors.CreateIndividualUnchecked( IdentifierType.Player, identifier.PlayerName, identifier.HomeWorld, ObjectKind.None, uint.MaxValue );
        return CheckYourself( id, owner )
         ?? CollectionByAttributes( owner );
    }

    /// <summary>
    /// Go through all ModelChara rows and return a bitfield of those that resolve to human models.
    /// </summary>
    private static BitArray GetValidHumanModels( DataManager gameData )
    {
        var sheet = gameData.GetExcelSheet< ModelChara >()!;
        var ret   = new BitArray( ( int )sheet.RowCount, false );
        foreach( var (_, idx) in sheet.WithIndex().Where( p => p.Value.Type == ( byte )CharacterBase.ModelType.Human ) )
        {
            ret[ idx ] = true;
        }

        return ret;
    }
}