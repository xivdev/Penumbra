using Dalamud.Game.ClientState.Objects.Types;
using Newtonsoft.Json.Linq;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.String.Classes;
using Penumbra.UI;

namespace Penumbra.Interop.ResourceTree;

internal static class ResourceTreeApiHelper
{
    public static Dictionary<ushort, Dictionary<string, HashSet<string>>> GetResourcePathDictionaries(
        IEnumerable<(ICharacter, ResourceTree)> resourceTrees)
    {
        var pathDictionaries = new Dictionary<ushort, Dictionary<string, HashSet<string>>>(4);

        foreach (var (gameObject, resourceTree) in resourceTrees)
        {
            if (pathDictionaries.ContainsKey(gameObject.ObjectIndex))
                continue;

            var pathDictionary = new Dictionary<string, HashSet<string>>();
            pathDictionaries.Add(gameObject.ObjectIndex, pathDictionary);

            CollectResourcePaths(pathDictionary, resourceTree);
        }

        return pathDictionaries;
    }

    private static void CollectResourcePaths(Dictionary<string, HashSet<string>> pathDictionary, ResourceTree resourceTree)
    {
        foreach (var node in resourceTree.FlatNodes)
        {
            if (node.PossibleGamePaths.Length == 0)
                continue;

            var fullPath = node.FullPath.ToPath();
            if (!pathDictionary.TryGetValue(fullPath, out var gamePaths))
            {
                gamePaths = [];
                pathDictionary.Add(fullPath, gamePaths);
            }

            foreach (var gamePath in node.PossibleGamePaths)
                gamePaths.Add(gamePath.ToString());
        }
    }

    public static Dictionary<ushort, GameResourceDict> GetResourcesOfType(IEnumerable<(ICharacter, ResourceTree)> resourceTrees,
        ResourceType type)
    {
        var resDictionaries = new Dictionary<ushort, GameResourceDict>(4);
        foreach (var (gameObject, resourceTree) in resourceTrees)
        {
            if (resDictionaries.ContainsKey(gameObject.ObjectIndex))
                continue;

            var resDictionary = new Dictionary<nint, (string, string, uint)>();
            resDictionaries.Add(gameObject.ObjectIndex, new GameResourceDict(resDictionary));

            foreach (var node in resourceTree.FlatNodes)
            {
                if (node.Type != type)
                    continue;
                if (resDictionary.ContainsKey(node.ResourceHandle))
                    continue;

                var fullPath = node.FullPath.ToPath();
                resDictionary.Add(node.ResourceHandle, (fullPath, node.Name ?? string.Empty, (uint)node.IconFlag.ToApiIcon()));
            }
        }

        return resDictionaries;
    }

    public static Dictionary<ushort, JObject> EncapsulateResourceTrees(IEnumerable<(ICharacter, ResourceTree)> resourceTrees)
    {
        var resDictionary = new Dictionary<ushort, JObject>(4);
        foreach (var (gameObject, resourceTree) in resourceTrees)
        {
            if (resDictionary.ContainsKey(gameObject.ObjectIndex))
                continue;

            resDictionary.Add(gameObject.ObjectIndex, GetIpcTree(resourceTree));
        }

        return resDictionary;

        static JObject GetIpcTree(ResourceTree tree)
        {
            var ret = new JObject
            {
                [nameof(ResourceTreeDto.Name)]     = tree.Name,
                [nameof(ResourceTreeDto.RaceCode)] = (ushort)tree.RaceCode,
            };
            var children = new JArray();
            foreach (var child in tree.Nodes)
                children.Add(GetIpcNode(child));
            ret[nameof(ResourceTreeDto.Nodes)] = children;
            return ret;
        }

        static JObject GetIpcNode(ResourceNode node)
        {
            var ret = new JObject
            {
                [nameof(ResourceNodeDto.Type)]           = new JValue(node.Type),
                [nameof(ResourceNodeDto.Icon)]           = new JValue(node.IconFlag.ToApiIcon()),
                [nameof(ResourceNodeDto.Name)]           = node.Name,
                [nameof(ResourceNodeDto.GamePath)]       = node.GamePath.Equals(Utf8GamePath.Empty) ? null : node.GamePath.ToString(),
                [nameof(ResourceNodeDto.ActualPath)]     = node.FullPath.ToString(),
                [nameof(ResourceNodeDto.ObjectAddress)]  = node.ObjectAddress,
                [nameof(ResourceNodeDto.ResourceHandle)] = node.ResourceHandle,
            };
            var children = new JArray();
            foreach (var child in node.Children)
                children.Add(GetIpcNode(child));
            ret[nameof(ResourceNodeDto.Children)] = children;
            return ret;
        }
    }
}
