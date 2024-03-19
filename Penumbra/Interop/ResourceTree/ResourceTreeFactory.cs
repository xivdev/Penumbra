using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.Api.Enums;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;
using Penumbra.String.Classes;

namespace Penumbra.Interop.ResourceTree;

public class ResourceTreeFactory(
    IDataManager gameData,
    ObjectManager objects,
    CollectionResolver resolver,
    ObjectIdentification identifier,
    Configuration config,
    ActorManager actors,
    PathState pathState)
{
    private TreeBuildCache CreateTreeBuildCache()
        => new(objects, gameData, actors);

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

        var collectionResolveData = resolver.IdentifyCollection(gameObjStruct, true);
        if (!collectionResolveData.Valid)
            return null;

        var localPlayerRelated = cache.IsLocalPlayerRelated(character);
        var (name, related) = GetCharacterName(character, cache);
        var networked = character.ObjectId != Dalamud.Game.ClientState.Objects.Types.GameObject.InvalidGameObjectId;
        var tree = new ResourceTree(name, character.ObjectIndex, (nint)gameObjStruct, (nint)drawObjStruct, localPlayerRelated, related,
            networked, collectionResolveData.ModCollection.Name);
        var globalContext = new GlobalResolveContext(identifier, collectionResolveData.ModCollection,
            cache, (flags & Flags.WithUiData) != 0);
        using (var _ = pathState.EnterInternalResolve())
        {
            tree.LoadResources(globalContext);
        }

        tree.FlatNodes.UnionWith(globalContext.Nodes.Values);
        tree.ProcessPostfix((node, _) => tree.FlatNodes.Add(node));

        // This is currently unneeded as we can resolve all paths by querying the draw object:
        // ResolveGamePaths(tree, collectionResolveData.ModCollection);
        if (globalContext.WithUiData)
            ResolveUiData(tree);
        FilterFullPaths(tree, (flags & Flags.RedactExternalPaths) != 0 ? config.ModDirectory : null);
        Cleanup(tree);

        return tree;
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
                ResourceType.Imc => node.ResolveContext!.GuessModelUiData(gamePath).PrependName("IMC: "),
                ResourceType.Mdl => node.ResolveContext!.GuessModelUiData(gamePath),
                _                => node.ResolveContext!.GuessUiDataFromPath(gamePath),
            });
        }

        tree.ProcessPostfix((node, parent) =>
        {
            if (node.Name == parent?.Name)
                node.Name = null;

            if (parent != null)
                parent.DescendentIcons |= node.Icon | node.DescendentIcons;
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
        var identifier = actors.FromObject((GameObject*)character.Address, out var owner, true, false, false);
        switch (identifier.Type)
        {
            case IdentifierType.Player: return (identifier.PlayerName.ToString(), true);
            case IdentifierType.Owned:
                var ownerChara = objects.Objects.CreateObjectReference(owner) as Dalamud.Game.ClientState.Objects.Types.Character;
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
