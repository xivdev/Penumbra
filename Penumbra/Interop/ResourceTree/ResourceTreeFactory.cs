using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.GameData.Actors;
using Penumbra.Interop.PathResolving;
using Penumbra.Services;
using Penumbra.String.Classes;

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
        return cache.GetLocalPlayerRelatedCharacters();
    }

    public IEnumerable<(Dalamud.Game.ClientState.Objects.Types.Character Character, ResourceTree ResourceTree)> FromObjectTable(
        Flags flags)
    {
        var cache      = CreateTreeBuildCache();
        var characters = (flags & Flags.LocalPlayerRelatedOnly) != 0 ? cache.GetLocalPlayerRelatedCharacters() : cache.GetCharacters();

        foreach (var character in characters)
        {
            var tree = FromCharacter(character, cache, flags);
            if (tree != null)
                yield return (character, tree);
        }
    }

    public IEnumerable<(Dalamud.Game.ClientState.Objects.Types.Character Character, ResourceTree ResourceTree)> FromCharacters(
        IEnumerable<Dalamud.Game.ClientState.Objects.Types.Character> characters, Flags flags)
    {
        var cache = CreateTreeBuildCache();
        foreach (var character in characters)
        {
            var tree = FromCharacter(character, cache, flags);
            if (tree != null)
                yield return (character, tree);
        }
    }

    public ResourceTree? FromCharacter(Dalamud.Game.ClientState.Objects.Types.Character character, Flags flags)
        => FromCharacter(character, CreateTreeBuildCache(), flags);

    private unsafe ResourceTree? FromCharacter(Dalamud.Game.ClientState.Objects.Types.Character character, TreeBuildCache cache, Flags flags)
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
        var (name, related) = GetCharacterName(character, cache);
        var networked = character.ObjectId != Dalamud.Game.ClientState.Objects.Types.GameObject.InvalidGameObjectId;
        var tree = new ResourceTree(name, character.ObjectIndex, (nint)gameObjStruct, (nint)drawObjStruct, localPlayerRelated, related,
            networked, collectionResolveData.ModCollection.Name);
        var globalContext = new GlobalResolveContext(_identifier.AwaitedService, cache,
            ((Character*)gameObjStruct)->CharacterData.ModelCharaId, (flags & Flags.WithUiData) != 0);
        tree.LoadResources(globalContext);
        tree.FlatNodes.UnionWith(globalContext.Nodes.Values);
        tree.ProcessPostfix((node, _) => tree.FlatNodes.Add(node));

        ResolveGamePaths(tree, collectionResolveData.ModCollection);
        if (globalContext.WithUiData)
            ResolveUiData(tree);
        FilterFullPaths(tree, (flags & Flags.RedactExternalPaths) != 0 ? _config.ModDirectory : null);
        Cleanup(tree);

        return tree;
    }

    private static void ResolveGamePaths(ResourceTree tree, ModCollection collection)
    {
        var forwardDictionary = new Dictionary<Utf8GamePath, FullPath?>();
        var reverseDictionary = new Dictionary<string, HashSet<Utf8GamePath>>();
        foreach (var node in tree.FlatNodes)
        {
            if (node.PossibleGamePaths.Length == 0 && !node.FullPath.InternalName.IsEmpty)
                reverseDictionary.TryAdd(node.FullPath.ToPath(), null!);
            else if (node.FullPath.InternalName.IsEmpty && node.PossibleGamePaths.Length == 1)
                forwardDictionary.TryAdd(node.GamePath, null);
        }

        foreach (var key in forwardDictionary.Keys)
            forwardDictionary[key] = collection.ResolvePath(key);

        var reverseResolvedArray = collection.ReverseResolvePaths(reverseDictionary.Keys);
        foreach (var (key, set) in reverseDictionary.Keys.Zip(reverseResolvedArray))
            reverseDictionary[key] = set;

        foreach (var node in tree.FlatNodes)
        {
            if (node.PossibleGamePaths.Length == 0 && !node.FullPath.InternalName.IsEmpty)
            {
                if (!reverseDictionary.TryGetValue(node.FullPath.ToPath(), out var resolvedSet))
                    continue;

                IReadOnlyCollection<Utf8GamePath> resolvedList = resolvedSet;
                if (resolvedList.Count > 1)
                {
                    var filteredList = node.ResolveContext!.FilterGamePaths(resolvedList);
                    if (filteredList.Count > 0)
                        resolvedList = filteredList;
                }

                if (resolvedList.Count != 1)
                {
                    Penumbra.Log.Debug(
                        $"Found {resolvedList.Count} game paths while reverse-resolving {node.FullPath} in {collection.Name}:");
                    foreach (var gamePath in resolvedList)
                        Penumbra.Log.Debug($"Game path: {gamePath}");
                }

                node.PossibleGamePaths = resolvedList.ToArray();
            }
            else if (node.FullPath.InternalName.IsEmpty && node.PossibleGamePaths.Length == 1)
            {
                if (forwardDictionary.TryGetValue(node.GamePath, out var resolved))
                    node.FullPath = resolved ?? new FullPath(node.GamePath);
            }
        }
    }

    private static void ResolveUiData(ResourceTree tree)
    {
        foreach (var node in tree.FlatNodes)
        {
            if (node.Name != null || node.PossibleGamePaths.Length == 0)
                continue;

            var gamePath = node.PossibleGamePaths[0];
            node.SetUiData(node.Type switch
            {
                ResourceType.Imc => node.ResolveContext!.GuessModelUIData(gamePath).PrependName("IMC: "),
                ResourceType.Mdl => node.ResolveContext!.GuessModelUIData(gamePath),
                _                => node.ResolveContext!.GuessUIDataFromPath(gamePath),
            });
        }

        tree.ProcessPostfix((node, parent) =>
        {
            if (node.Name == parent?.Name)
                node.Name = null;
        });
    }

    private static void FilterFullPaths(ResourceTree tree, string? onlyWithinPath)
    {
        static bool ShallKeepPath(FullPath fullPath, string? onlyWithinPath)
        {
            if (!fullPath.IsRooted)
                return true;

            if (onlyWithinPath != null)
            {
                var relPath = Path.GetRelativePath(onlyWithinPath, fullPath.FullName);
                if (relPath != "." && (relPath.StartsWith('.') || Path.IsPathRooted(relPath)))
                    return false;
            }

            return fullPath.Exists;
        }

        foreach (var node in tree.FlatNodes)
        {
            if (!ShallKeepPath(node.FullPath, onlyWithinPath))
                node.FullPath = FullPath.Empty;
        }
    }

    private static void Cleanup(ResourceTree tree)
    {
        foreach (var node in tree.FlatNodes)
        {
            node.Name ??= node.FallbackName;

            node.FallbackName   = null;
            node.ResolveContext = null;
        }
    }

    private unsafe (string Name, bool PlayerRelated) GetCharacterName(Dalamud.Game.ClientState.Objects.Types.Character character,
        TreeBuildCache cache)
    {
        var identifier = _actors.AwaitedService.FromObject((GameObject*)character.Address, out var owner, true, false, false);
        switch (identifier.Type)
        {
            case IdentifierType.Player: return (identifier.PlayerName.ToString(), true);
            case IdentifierType.Owned:
                var ownerChara = _objects.CreateObjectReference((nint)owner) as Dalamud.Game.ClientState.Objects.Types.Character;
                if (ownerChara != null)
                {
                    var ownerName = GetCharacterName(ownerChara, cache);
                    return ($"[{ownerName.Name}] {character.Name} ({identifier.Kind.ToName()})", ownerName.PlayerRelated);
                }

                break;
        }

        return ($"{character.Name} ({identifier.Kind.ToName()})", false);
    }

    [Flags]
    public enum Flags
    {
        RedactExternalPaths    = 1,
        WithUiData             = 2,
        LocalPlayerRelatedOnly = 4,
        WithOwnership          = 8,
    }
}
