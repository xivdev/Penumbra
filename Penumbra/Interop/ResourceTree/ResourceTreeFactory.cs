using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.Api.Enums;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta;
using Penumbra.Mods.Manager;
using Penumbra.String.Classes;

namespace Penumbra.Interop.ResourceTree;

public class ResourceTreeFactory(
    IDataManager gameData,
    ObjectManager objects,
    MetaFileManager metaFileManager,
    CollectionResolver resolver,
    ObjectIdentification objectIdentifier,
    Configuration config,
    ActorManager actors,
    PathState pathState,
    IFramework framework,
    ModManager modManager) : Luna.IService
{
    private static readonly string ParentDirectoryPrefix = $"..{Path.DirectorySeparatorChar}";

    private TreeBuildCache CreateTreeBuildCache()
        => new(framework.IsInFrameworkUpdateThread ? objects : null, gameData, actors);

    private TreeBuildCache CreateTreeBuildCache(Flags flags)
        => !framework.IsInFrameworkUpdateThread && flags.HasFlag(Flags.PopulateObjectTableData)
            ? framework.RunOnFrameworkThread(CreateTreeBuildCache).Result
            : CreateTreeBuildCache();

    public IEnumerable<ICharacter> GetLocalPlayerRelatedCharacters()
        => framework.RunOnFrameworkThread(() =>
        {
            var cache = CreateTreeBuildCache();
            return cache.GetLocalPlayerRelatedCharacters();
        }).Result;

    public IEnumerable<(ICharacter Character, ResourceTree ResourceTree)> FromObjectTable(
        Flags flags)
    {
        var (cache, characters) = framework.RunOnFrameworkThread(() =>
        {
            var cache = CreateTreeBuildCache();
            var characters = ((flags & Flags.LocalPlayerRelatedOnly) != 0 ? cache.GetLocalPlayerRelatedCharacters() : cache.GetCharacters()).ToArray();
            return (cache, characters);
        }).Result;

        foreach (var character in characters)
        {
            var tree = FromCharacter(character, cache, flags);
            if (tree != null)
                yield return (character, tree);
        }
    }

    public IEnumerable<(ICharacter Character, ResourceTree ResourceTree)> FromCharacters(
        IEnumerable<ICharacter> characters, Flags flags)
    {
        var cache = CreateTreeBuildCache(flags);
        foreach (var character in characters)
        {
            var tree = FromCharacter(character, cache, flags);
            if (tree != null)
                yield return (character, tree);
        }
    }

    public ResourceTree? FromCharacter(ICharacter character, Flags flags)
        => FromCharacter(character, CreateTreeBuildCache(flags), flags);

    private unsafe ResourceTree? FromCharacter(ICharacter character, TreeBuildCache cache, Flags flags)
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
        var (name, anonymizedName, related) = GetCharacterName((GameObject*)character.Address);
        var networked = character.EntityId != 0xE0000000;
        var tree = new ResourceTree(name, anonymizedName, character.ObjectIndex, (nint)gameObjStruct, (nint)drawObjStruct, localPlayerRelated, related,
            networked, collectionResolveData.ModCollection.Identity.Name, collectionResolveData.ModCollection.Identity.AnonymizedName);
        var globalContext = new GlobalResolveContext(metaFileManager, objectIdentifier, collectionResolveData.ModCollection,
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
        {
            ResolveUiData(tree);
            ResolveModData(tree);
        }
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
        });
    }

    private void ResolveModData(ResourceTree tree)
    {
        foreach (var node in tree.FlatNodes)
        {
            if (node.FullPath.IsRooted && modManager.TryIdentifyPath(node.FullPath.FullName, out var mod, out var relativePath))
            {
                node.ModName         = mod.Name;
                node.Mod.SetTarget(mod);
                node.ModRelativePath = relativePath;
            }
        }
    }

    private static void FilterFullPaths(ResourceTree tree, string? onlyWithinPath)
    {
        foreach (var node in tree.FlatNodes)
        {
            node.FullPathStatus = GetPathStatus(node.FullPath, onlyWithinPath);
            if (node.FullPathStatus != ResourceNode.PathStatus.Valid)
                node.FullPath = FullPath.Empty;
        }

        return;

        static ResourceNode.PathStatus GetPathStatus(FullPath fullPath, string? onlyWithinPath)
        {
            if (!fullPath.IsRooted)
                return ResourceNode.PathStatus.Valid;

            if (onlyWithinPath != null)
            {
                var relPath = Path.GetRelativePath(onlyWithinPath, fullPath.FullName);
                if (relPath == ".." || relPath.StartsWith(ParentDirectoryPrefix) || Path.IsPathRooted(relPath))
                    return ResourceNode.PathStatus.External;
            }

            return fullPath.Exists
                ? ResourceNode.PathStatus.Valid
                : ResourceNode.PathStatus.NonExistent;
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

    private unsafe (string Name, string AnonymizedName, bool PlayerRelated) GetCharacterName(GameObject* character)
    {
        var identifier = actors.FromObject(character, out var owner, true, false, false);
        var identifierStr = identifier.ToString();
        return (identifierStr, identifier.Incognito(identifierStr), IsPlayerRelated(identifier, owner));
    }

    private unsafe bool IsPlayerRelated(GameObject* character)
    {
        if (character is null)
            return false;

        var identifier = actors.FromObject(character, out var owner, true, false, false);
        return IsPlayerRelated(identifier, owner);
    }

    private unsafe bool IsPlayerRelated(ActorIdentifier identifier, Actor owner)
        => identifier.Type switch
        {
            IdentifierType.Player => true,
            IdentifierType.Owned  => IsPlayerRelated(owner.AsObject),
            _                     => false,
        };

    [Flags]
    public enum Flags
    {
        RedactExternalPaths     = 1,
        WithUiData              = 2,
        LocalPlayerRelatedOnly  = 4,
        WithOwnership           = 8,
        PopulateObjectTableData = 16,
    }
}
