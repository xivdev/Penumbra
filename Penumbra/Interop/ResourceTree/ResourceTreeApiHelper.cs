using Dalamud.Game.ClientState.Objects.Types;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.String.Classes;
using Penumbra.UI;

namespace Penumbra.Interop.ResourceTree;

internal static class ResourceTreeApiHelper
{
    public static Dictionary<ushort, IReadOnlyDictionary<string, string[]>> GetResourcePathDictionaries(IEnumerable<(Character, ResourceTree)> resourceTrees)
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

        return pathDictionaries.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyDictionary<string, string[]>)pair.Value.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()).AsReadOnly());
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
                gamePaths = new();
                pathDictionary.Add(fullPath, gamePaths);
            }

            foreach (var gamePath in node.PossibleGamePaths)
                gamePaths.Add(gamePath.ToString());
        }
    }

    public static Dictionary<ushort, IReadOnlyDictionary<nint, (string, string, ChangedItemIcon)>> GetResourcesOfType(IEnumerable<(Character, ResourceTree)> resourceTrees,
        ResourceType type)
    {
        var resDictionaries = new Dictionary<ushort, Dictionary<nint, (string, string, ChangedItemIcon)>>(4);
        foreach (var (gameObject, resourceTree) in resourceTrees)
        {
            if (resDictionaries.ContainsKey(gameObject.ObjectIndex))
                continue;

            var resDictionary = new Dictionary<nint, (string, string, ChangedItemIcon)>();
            resDictionaries.Add(gameObject.ObjectIndex, resDictionary);

            foreach (var node in resourceTree.FlatNodes)
            {
                if (node.Type != type)
                    continue;
                if (resDictionary.ContainsKey(node.ResourceHandle))
                    continue;

                var fullPath = node.FullPath.ToPath();
                resDictionary.Add(node.ResourceHandle, (fullPath, node.Name ?? string.Empty, ChangedItemDrawer.ToApiIcon(node.Icon)));
            }
        }

        return resDictionaries.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyDictionary<nint, (string, string, ChangedItemIcon)>)pair.Value.AsReadOnly());
    }

    public static Dictionary<ushort, Ipc.ResourceTree> EncapsulateResourceTrees(IEnumerable<(Character, ResourceTree)> resourceTrees)
    {
        static Ipc.ResourceNode GetIpcNode(ResourceNode node) =>
            new()
            {
                Type = node.Type,
                Icon = ChangedItemDrawer.ToApiIcon(node.Icon),
                Name = node.Name,
                GamePath = node.GamePath.Equals(Utf8GamePath.Empty) ? null : node.GamePath.ToString(),
                ActualPath = node.FullPath.ToString(),
                ObjectAddress = node.ObjectAddress,
                ResourceHandle = node.ResourceHandle,
                Children = node.Children.Select(GetIpcNode).ToList(),
            };

        static Ipc.ResourceTree GetIpcTree(ResourceTree tree) =>
            new()
            {
                Name = tree.Name,
                RaceCode = (ushort)tree.RaceCode,
                Nodes = tree.Nodes.Select(GetIpcNode).ToList(),
            };

        var resDictionary = new Dictionary<ushort, Ipc.ResourceTree>(4);
        foreach (var (gameObject, resourceTree) in resourceTrees)
        {
            if (resDictionary.ContainsKey(gameObject.ObjectIndex))
                continue;

            resDictionary.Add(gameObject.ObjectIndex, GetIpcTree(resourceTree));
        }

        return resDictionary;
    }
}
