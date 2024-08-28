using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Interop;
using Penumbra.Mods.Settings;
using Penumbra.String.Classes;

namespace Penumbra.Api.Api;

public class TemporaryApi(
    TempCollectionManager tempCollections,
    ObjectManager objects,
    ActorManager actors,
    CollectionManager collectionManager,
    TempModManager tempMods) : IPenumbraApiTemporary, IApiService
{
    public Guid CreateTemporaryCollection(string name)
        => tempCollections.CreateTemporaryCollection(name);

    public PenumbraApiEc DeleteTemporaryCollection(Guid collectionId)
        => tempCollections.RemoveTemporaryCollection(collectionId)
            ? PenumbraApiEc.Success
            : PenumbraApiEc.CollectionMissing;

    public PenumbraApiEc AssignTemporaryCollection(Guid collectionId, int actorIndex, bool forceAssignment)
    {
        var args = ApiHelpers.Args("CollectionId", collectionId, "ActorIndex", actorIndex, "Forced", forceAssignment);
        if (actorIndex < 0 || actorIndex >= objects.TotalCount)
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        var identifier = actors.FromObject(objects[actorIndex], out _, false, false, true);
        if (!identifier.IsValid)
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        if (!tempCollections.CollectionById(collectionId, out var collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        if (forceAssignment)
        {
            if (tempCollections.Collections.ContainsKey(identifier) && !tempCollections.Collections.Delete(identifier))
                return ApiHelpers.Return(PenumbraApiEc.AssignmentDeletionFailed, args);
        }
        else if (tempCollections.Collections.ContainsKey(identifier)
              || collectionManager.Active.Individuals.ContainsKey(identifier))
        {
            return ApiHelpers.Return(PenumbraApiEc.CharacterCollectionExists, args);
        }

        var group = tempCollections.Collections.GetGroup(identifier);
        var ret = tempCollections.AddIdentifier(collection, group)
            ? PenumbraApiEc.Success
            : PenumbraApiEc.UnknownError;
        return ApiHelpers.Return(ret, args);
    }

    public PenumbraApiEc AddTemporaryModAll(string tag, Dictionary<string, string> paths, string manipString, int priority)
    {
        var args = ApiHelpers.Args("Tag", tag, "#Paths", paths.Count, "ManipString", manipString, "Priority", priority);
        if (!ConvertPaths(paths, out var p))
            return ApiHelpers.Return(PenumbraApiEc.InvalidGamePath, args);

        if (!MetaApi.ConvertManips(manipString, out var m))
            return ApiHelpers.Return(PenumbraApiEc.InvalidManipulation, args);

        var ret = tempMods.Register(tag, null, p, m, new ModPriority(priority)) switch
        {
            RedirectResult.Success => PenumbraApiEc.Success,
            _                      => PenumbraApiEc.UnknownError,
        };
        return ApiHelpers.Return(ret, args);
    }

    public PenumbraApiEc AddTemporaryMod(string tag, Guid collectionId, Dictionary<string, string> paths, string manipString, int priority)
    {
        var args = ApiHelpers.Args("Tag", tag, "CollectionId", collectionId, "#Paths", paths.Count, "ManipString",
            manipString, "Priority", priority);

        if (collectionId == Guid.Empty)
            return ApiHelpers.Return(PenumbraApiEc.InvalidArgument, args);

        if (!tempCollections.CollectionById(collectionId, out var collection)
         && !collectionManager.Storage.ById(collectionId, out collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        if (!ConvertPaths(paths, out var p))
            return ApiHelpers.Return(PenumbraApiEc.InvalidGamePath, args);

        if (!MetaApi.ConvertManips(manipString, out var m))
            return ApiHelpers.Return(PenumbraApiEc.InvalidManipulation, args);

        var ret = tempMods.Register(tag, collection, p, m, new ModPriority(priority)) switch
        {
            RedirectResult.Success => PenumbraApiEc.Success,
            _                      => PenumbraApiEc.UnknownError,
        };
        return ApiHelpers.Return(ret, args);
    }

    public PenumbraApiEc RemoveTemporaryModAll(string tag, int priority)
    {
        var ret = tempMods.Unregister(tag, null, new ModPriority(priority)) switch
        {
            RedirectResult.Success       => PenumbraApiEc.Success,
            RedirectResult.NotRegistered => PenumbraApiEc.NothingChanged,
            _                            => PenumbraApiEc.UnknownError,
        };
        return ApiHelpers.Return(ret, ApiHelpers.Args("Tag", tag, "Priority", priority));
    }

    public PenumbraApiEc RemoveTemporaryMod(string tag, Guid collectionId, int priority)
    {
        var args = ApiHelpers.Args("Tag", tag, "CollectionId", collectionId, "Priority", priority);

        if (!tempCollections.CollectionById(collectionId, out var collection)
         && !collectionManager.Storage.ById(collectionId, out collection))
            return ApiHelpers.Return(PenumbraApiEc.CollectionMissing, args);

        var ret = tempMods.Unregister(tag, collection, new ModPriority(priority)) switch
        {
            RedirectResult.Success       => PenumbraApiEc.Success,
            RedirectResult.NotRegistered => PenumbraApiEc.NothingChanged,
            _                            => PenumbraApiEc.UnknownError,
        };
        return ApiHelpers.Return(ret, args);
    }

    /// <summary>
    /// Convert a dictionary of strings to a dictionary of game paths to full paths.
    /// Only returns true if all paths can successfully be converted and added.
    /// </summary>
    private static bool ConvertPaths(IReadOnlyDictionary<string, string> redirections,
        [NotNullWhen(true)] out Dictionary<Utf8GamePath, FullPath>? paths)
    {
        paths = new Dictionary<Utf8GamePath, FullPath>(redirections.Count);
        foreach (var (gString, fString) in redirections)
        {
            if (!Utf8GamePath.FromString(gString, out var path))
            {
                paths = null;
                return false;
            }

            var fullPath = new FullPath(fString);
            if (!paths.TryAdd(path, fullPath))
            {
                paths = null;
                return false;
            }
        }

        return true;
    }
}
