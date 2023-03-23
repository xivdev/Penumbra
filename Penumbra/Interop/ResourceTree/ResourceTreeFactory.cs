using System.Collections.Generic;
using System.Linq;
using Dalamud.Data;
using Dalamud.Game.ClientState.Objects;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.GameData;
using Penumbra.Interop.Resolver;
using Penumbra.Services;

namespace Penumbra.Interop.ResourceTree;

public class ResourceTreeFactory
{
    private readonly DataManager        _gameData;
    private readonly ObjectTable        _objects;
    private readonly CollectionResolver _collectionResolver;
    private readonly IdentifierService  _identifier;
    private readonly Configuration      _config;

    public ResourceTreeFactory(DataManager gameData, ObjectTable objects, CollectionResolver resolver, IdentifierService identifier,
        Configuration config)
    {
        _gameData           = gameData;
        _objects            = objects;
        _collectionResolver = resolver;
        _identifier         = identifier;
        _config             = config;
    }

    public ResourceTree[] FromObjectTable(bool withNames = true)
    {
        var cache = new FileCache(_gameData);

        return _objects
            .OfType<Dalamud.Game.ClientState.Objects.Types.Character>()
            .Select(c => FromCharacter(c, cache, withNames))
            .OfType<ResourceTree>()
            .ToArray();
    }

    public IEnumerable<(Dalamud.Game.ClientState.Objects.Types.Character Character, ResourceTree ResourceTree)> FromCharacters(
        IEnumerable<Dalamud.Game.ClientState.Objects.Types.Character> characters,
        bool withNames = true)
    {
        var cache = new FileCache(_gameData);
        foreach (var character in characters)
        {
            var tree = FromCharacter(character, cache, withNames);
            if (tree != null)
                yield return (character, tree);
        }
    }

    public ResourceTree? FromCharacter(Dalamud.Game.ClientState.Objects.Types.Character character, bool withNames = true)
        => FromCharacter(character, new FileCache(_gameData), withNames);

    private unsafe ResourceTree? FromCharacter(Dalamud.Game.ClientState.Objects.Types.Character character, FileCache cache,
        bool withNames = true)
    {
        var gameObjStruct = (GameObject*)character.Address;
        if (gameObjStruct->GetDrawObject() == null)
            return null;

        var collectionResolveData = _collectionResolver.IdentifyCollection(gameObjStruct, true);
        if (!collectionResolveData.Valid)
            return null;

        var tree = new ResourceTree(character.Name.ToString(), (nint)gameObjStruct, collectionResolveData.ModCollection.Name);
        var globalContext = new GlobalResolveContext(_config, _identifier.AwaitedService, cache, collectionResolveData.ModCollection,
            ((Character*)gameObjStruct)->ModelCharaId,
            withNames);
        tree.LoadResources(globalContext);
        return tree;
    }
}
