using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.GameData.Actors;
using Penumbra.Interop.PathResolving;
using Penumbra.Services;

namespace Penumbra.Interop.ResourceTree;

public class ResourceTreeFactory
{
    private readonly IDataManager       _gameData;
    private readonly IObjectTable       _objects;
    private readonly CollectionResolver _collectionResolver;
    private readonly IdentifierService  _identifier;
    private readonly Configuration      _config;
    private readonly ActorService       _actors;

    public ResourceTreeFactory(IDataManager gameData, IObjectTable objects, CollectionResolver resolver, IdentifierService identifier,
        Configuration config, ActorService actors)
    {
        _gameData           = gameData;
        _objects            = objects;
        _collectionResolver = resolver;
        _identifier         = identifier;
        _config             = config;
        _actors             = actors;
    }

    private TreeBuildCache CreateTreeBuildCache()
        => new(_objects, _gameData, _actors);

    public IEnumerable<Dalamud.Game.ClientState.Objects.Types.Character> GetLocalPlayerRelatedCharacters()
    {
        var cache = CreateTreeBuildCache();

        return cache.Characters.Where(cache.IsLocalPlayerRelated);
    }

    public IEnumerable<(Dalamud.Game.ClientState.Objects.Types.Character Character, ResourceTree ResourceTree)> FromObjectTable(
        bool localPlayerRelatedOnly = false, bool withUIData = true, bool redactExternalPaths = true)
    {
        var cache      = CreateTreeBuildCache();
        var characters = localPlayerRelatedOnly ? cache.Characters.Where(cache.IsLocalPlayerRelated) : cache.Characters;

        foreach (var character in characters)
        {
            var tree = FromCharacter(character, cache, withUIData, redactExternalPaths);
            if (tree != null)
                yield return (character, tree);
        }
    }

    public IEnumerable<(Dalamud.Game.ClientState.Objects.Types.Character Character, ResourceTree ResourceTree)> FromCharacters(
        IEnumerable<Dalamud.Game.ClientState.Objects.Types.Character> characters,
        bool withUIData = true, bool redactExternalPaths = true)
    {
        var cache = CreateTreeBuildCache();
        foreach (var character in characters)
        {
            var tree = FromCharacter(character, cache, withUIData, redactExternalPaths);
            if (tree != null)
                yield return (character, tree);
        }
    }

    public ResourceTree? FromCharacter(Dalamud.Game.ClientState.Objects.Types.Character character, bool withUIData = true,
        bool redactExternalPaths = true)
        => FromCharacter(character, CreateTreeBuildCache(), withUIData, redactExternalPaths);

    private unsafe ResourceTree? FromCharacter(Dalamud.Game.ClientState.Objects.Types.Character character, TreeBuildCache cache,
        bool withUIData = true, bool redactExternalPaths = true)
    {
        if (!character.IsValid())
            return null;

        var gameObjStruct = (GameObject*)character.Address;
        var drawObjStruct = gameObjStruct->GetDrawObject();
        if (drawObjStruct == null)
            return null;

        var collectionResolveData = _collectionResolver.IdentifyCollection(gameObjStruct, true);
        if (!collectionResolveData.Valid)
            return null;

        var localPlayerRelated = cache.IsLocalPlayerRelated(character);
        var (name, related)    = GetCharacterName(character, cache);
        var networked          = character.ObjectId != Dalamud.Game.ClientState.Objects.Types.GameObject.InvalidGameObjectId;
        var tree = new ResourceTree(name, character.ObjectIndex, (nint)gameObjStruct, (nint)drawObjStruct, localPlayerRelated, related, networked, collectionResolveData.ModCollection.Name);
        var globalContext = new GlobalResolveContext(_config, _identifier.AwaitedService, cache, collectionResolveData.ModCollection,
            ((Character*)gameObjStruct)->CharacterData.ModelCharaId, withUIData, redactExternalPaths);
        tree.LoadResources(globalContext);
        tree.FlatNodes.UnionWith(globalContext.Nodes.Values);
        return tree;
    }

    private unsafe (string Name, bool PlayerRelated) GetCharacterName(Dalamud.Game.ClientState.Objects.Types.Character character,
        TreeBuildCache cache)
    {
        var    identifier = _actors.AwaitedService.FromObject((GameObject*)character.Address, out var owner, true, false, false);
        string name;
        bool   playerRelated;
        switch (identifier.Type)
        {
            case IdentifierType.Player:
                name          = identifier.PlayerName.ToString();
                playerRelated = true;
                break;
            case IdentifierType.Owned when cache.CharactersById.TryGetValue(owner->ObjectID, out var ownerChara):
                var ownerName = GetCharacterName(ownerChara, cache);
                name          = $"[{ownerName.Name}] {character.Name} ({identifier.Kind.ToName()})";
                playerRelated = ownerName.PlayerRelated;
                break;
            default:
                name          = $"{character.Name} ({identifier.Kind.ToName()})";
                playerRelated = false;
                break;
        }

        return (name, playerRelated);
    }
}
