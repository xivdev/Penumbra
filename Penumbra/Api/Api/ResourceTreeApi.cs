using Dalamud.Game.ClientState.Objects.Types;
using Newtonsoft.Json.Linq;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.GameData.Interop;
using Penumbra.Interop.ResourceTree;

namespace Penumbra.Api.Api;

public class ResourceTreeApi(ResourceTreeFactory resourceTreeFactory, ObjectManager objects) : IPenumbraApiResourceTree, IApiService
{
    public Dictionary<string, HashSet<string>>?[] GetGameObjectResourcePaths(params ushort[] gameObjects)
    {
        var characters       = gameObjects.Select(index => objects.GetDalamudObject((int)index)).OfType<ICharacter>();
        var resourceTrees    = resourceTreeFactory.FromCharacters(characters, 0);
        var pathDictionaries = ResourceTreeApiHelper.GetResourcePathDictionaries(resourceTrees);

        return Array.ConvertAll(gameObjects, obj => pathDictionaries.GetValueOrDefault(obj));
    }

    public Dictionary<ushort, Dictionary<string, HashSet<string>>> GetPlayerResourcePaths()
    {
        var resourceTrees = resourceTreeFactory.FromObjectTable(ResourceTreeFactory.Flags.LocalPlayerRelatedOnly);
        return ResourceTreeApiHelper.GetResourcePathDictionaries(resourceTrees);
    }

    public GameResourceDict?[] GetGameObjectResourcesOfType(ResourceType type, bool withUiData,
        params ushort[] gameObjects)
    {
        var characters      = gameObjects.Select(index => objects.GetDalamudObject((int)index)).OfType<ICharacter>();
        var resourceTrees   = resourceTreeFactory.FromCharacters(characters, withUiData ? ResourceTreeFactory.Flags.WithUiData : 0);
        var resDictionaries = ResourceTreeApiHelper.GetResourcesOfType(resourceTrees, type);

        return Array.ConvertAll(gameObjects, obj => resDictionaries.GetValueOrDefault(obj));
    }

    public Dictionary<ushort, GameResourceDict> GetPlayerResourcesOfType(ResourceType type,
        bool withUiData)
    {
        var resourceTrees = resourceTreeFactory.FromObjectTable(ResourceTreeFactory.Flags.LocalPlayerRelatedOnly
          | (withUiData ? ResourceTreeFactory.Flags.WithUiData : 0));
        return ResourceTreeApiHelper.GetResourcesOfType(resourceTrees, type);
    }

    public JObject?[] GetGameObjectResourceTrees(bool withUiData, params ushort[] gameObjects)
    {
        var characters    = gameObjects.Select(index => objects.GetDalamudObject((int)index)).OfType<ICharacter>();
        var resourceTrees = resourceTreeFactory.FromCharacters(characters, withUiData ? ResourceTreeFactory.Flags.WithUiData : 0);
        var resDictionary = ResourceTreeApiHelper.EncapsulateResourceTrees(resourceTrees);

        return Array.ConvertAll(gameObjects, obj => resDictionary.GetValueOrDefault(obj));
    }

    public Dictionary<ushort, JObject> GetPlayerResourceTrees(bool withUiData)
    {
        var resourceTrees = resourceTreeFactory.FromObjectTable(ResourceTreeFactory.Flags.LocalPlayerRelatedOnly
          | (withUiData ? ResourceTreeFactory.Flags.WithUiData : 0));
        var resDictionary = ResourceTreeApiHelper.EncapsulateResourceTrees(resourceTrees);

        return resDictionary;
    }
}
