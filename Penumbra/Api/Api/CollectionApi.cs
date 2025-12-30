using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Api.Api;

public class CollectionApi : IPenumbraApiCollection, IApiService, IDisposable
{
    private readonly CollectionManager   _collections;
    private readonly ApiHelpers          _helpers;
    private readonly CommunicatorService _communicator;

    public CollectionApi(CollectionManager collections, ApiHelpers helpers, CommunicatorService communicator)
    {
        _collections   = collections;
        _helpers       = helpers;
        _communicator  = communicator;
        _communicator.ResolvedFileChanged.Subscribe(OnResolvedFileChange, Communication.ResolvedFileChanged.Priority.ApiResolvedFile);
    }

    public void Dispose()
    {
        _communicator.ResolvedFileChanged.Unsubscribe(OnResolvedFileChange);
    }

    public event ResolvedFileChangedDelegate? ResolvedFileChanged;

    public Dictionary<Guid, string> GetCollections()
        => _collections.Storage.ToDictionary(c => c.Identity.Id, c => c.Identity.Name);

    public List<(Guid Id, string Name)> GetCollectionsByIdentifier(string identifier)
    {
        if (identifier.Length == 0)
            return [];

        var list = new List<(Guid Id, string Name)>(4);
        if (Guid.TryParse(identifier, out var guid) && _collections.Storage.ById(guid, out var collection) && collection != ModCollection.Empty)
            list.Add((collection.Identity.Id, collection.Identity.Name));
        else if (identifier.Length >= 8)
            list.AddRange(_collections.Storage.Where(c => c.Identity.Identifier.StartsWith(identifier, StringComparison.OrdinalIgnoreCase))
                .Select(c => (c.Identity.Id, c.Identity.Name)));

        list.AddRange(_collections.Storage
            .Where(c => string.Equals(c.Identity.Name, identifier, StringComparison.OrdinalIgnoreCase)
             && !list.Contains((c.Identity.Id, c.Identity.Name)))
            .Select(c => (c.Identity.Id, c.Identity.Name)));
        return list;
    }

    public Func<string, (string ModDirectory, string ModName)[]> CheckCurrentChangedItemFunc()
    {
        var weakRef = new WeakReference<CollectionManager>(_collections);
        return s =>
        {
            if (!weakRef.TryGetTarget(out var c))
                throw new ObjectDisposedException("The underlying collection storage of this IPC container was disposed.");

            if (!c.Active.Current.ChangedItems.TryGetValue(s, out var d))
                return [];

            return d.Item1.Select(m => (m is Mod mod ? mod.Identifier : string.Empty, m.Name.Text)).ToArray();
        };
    }

    public Dictionary<string, string> GetResolvedFilesForCollection(Guid collectionId)
    {
        try
        {
            if (!_collections.Storage.ById(collectionId, out var collection))
                collection = ModCollection.Empty;

            if (collection.HasCache)
                return collection.ResolvedFiles.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value.Path.ToString());

            Penumbra.Log.Warning($"Collection {collectionId} does not exist or is not loaded.");
            return [];
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not obtain Resolved Files for {collectionId}:\n{e}");
            throw;
        }
    }

    public Dictionary<string, object?> GetChangedItemsForCollection(Guid collectionId)
    {
        try
        {
            if (!_collections.Storage.ById(collectionId, out var collection))
                collection = ModCollection.Empty;

            if (collection.HasCache)
                return collection.ChangedItems.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Item2?.ToInternalObject());

            Penumbra.Log.Warning($"Collection {collectionId} does not exist or is not loaded.");
            return [];
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not obtain Changed Items for {collectionId}:\n{e}");
            throw;
        }
    }

    public (Guid Id, string Name)? GetCollection(ApiCollectionType type)
    {
        if (!Enum.IsDefined(type))
            return null;

        var collection = _collections.Active.ByType((CollectionType)type);
        return collection == null ? null : (collection.Identity.Id, collection.Identity.Name);
    }

    internal (Guid Id, string Name)? GetCollection(byte type)
        => GetCollection((ApiCollectionType)type);

    public (bool ObjectValid, bool IndividualSet, (Guid Id, string Name) EffectiveCollection) GetCollectionForObject(int gameObjectIdx)
    {
        var id = _helpers.AssociatedIdentifier(gameObjectIdx);
        if (!id.IsValid)
            return (false, false, (_collections.Active.Default.Identity.Id, _collections.Active.Default.Identity.Name));

        if (_collections.Active.Individuals.TryGetValue(id, out var collection))
            return (true, true, (collection.Identity.Id, collection.Identity.Name));

        _helpers.AssociatedCollection(gameObjectIdx, out collection);
        return (true, false, (collection.Identity.Id, collection.Identity.Name));
    }

    public Guid[] GetCollectionByName(string name)
        => _collections.Storage.Where(c => string.Equals(name, c.Identity.Name, StringComparison.OrdinalIgnoreCase)).Select(c => c.Identity.Id)
            .ToArray();

    public (PenumbraApiEc, (Guid Id, string Name)? OldCollection) SetCollection(ApiCollectionType type, Guid? collectionId,
        bool allowCreateNew, bool allowDelete)
    {
        if (!Enum.IsDefined(type))
            return (PenumbraApiEc.InvalidArgument, null);

        var oldCollection = _collections.Active.ByType((CollectionType)type);
        var old           = oldCollection != null ? (oldCollection.Identity.Id, oldCollection.Identity.Name) : new ValueTuple<Guid, string>?();
        if (collectionId == null)
        {
            if (old == null)
                return (PenumbraApiEc.NothingChanged, old);

            if (!allowDelete || type is ApiCollectionType.Current or ApiCollectionType.Default or ApiCollectionType.Interface)
                return (PenumbraApiEc.AssignmentDeletionDisallowed, old);

            _collections.Active.RemoveSpecialCollection((CollectionType)type);
            return (PenumbraApiEc.Success, old);
        }

        if (!_collections.Storage.ById(collectionId.Value, out var collection))
            return (PenumbraApiEc.CollectionMissing, old);

        if (old == null)
        {
            if (!allowCreateNew)
                return (PenumbraApiEc.AssignmentCreationDisallowed, old);

            _collections.Active.CreateSpecialCollection((CollectionType)type);
        }
        else if (old.Value.Item1 == collection.Identity.Id)
        {
            return (PenumbraApiEc.NothingChanged, old);
        }

        _collections.Active.SetCollection(collection, (CollectionType)type);
        return (PenumbraApiEc.Success, old);
    }

    public (PenumbraApiEc, (Guid Id, string Name)? OldCollection) SetCollectionForObject(int gameObjectIdx, Guid? collectionId,
        bool allowCreateNew, bool allowDelete)
    {
        var id = _helpers.AssociatedIdentifier(gameObjectIdx);
        if (!id.IsValid)
            return (PenumbraApiEc.InvalidIdentifier, (_collections.Active.Default.Identity.Id, _collections.Active.Default.Identity.Name));

        var oldCollection = _collections.Active.Individuals.TryGetValue(id, out var c) ? c : null;
        var old           = oldCollection != null ? (oldCollection.Identity.Id, oldCollection.Identity.Name) : new ValueTuple<Guid, string>?();
        if (collectionId == null)
        {
            if (old == null)
                return (PenumbraApiEc.NothingChanged, old);

            if (!allowDelete)
                return (PenumbraApiEc.AssignmentDeletionDisallowed, old);

            var idx = _collections.Active.Individuals.Index(id);
            _collections.Active.RemoveIndividualCollection(idx);
            return (PenumbraApiEc.Success, old);
        }

        if (!_collections.Storage.ById(collectionId.Value, out var collection))
            return (PenumbraApiEc.CollectionMissing, old);

        if (old == null)
        {
            if (!allowCreateNew)
                return (PenumbraApiEc.AssignmentCreationDisallowed, old);

            var ids = _collections.Active.Individuals.GetGroup(id);
            _collections.Active.CreateIndividualCollection(ids);
        }
        else if (old.Value.Item1 == collection.Identity.Id)
        {
            return (PenumbraApiEc.NothingChanged, old);
        }

        _collections.Active.SetCollection(collection, CollectionType.Individual, _collections.Active.Individuals.Index(id));
        return (PenumbraApiEc.Success, old);
    }

    private void OnResolvedFileChange(ModCollection collection, ResolvedFileChange type, Utf8GamePath gamePath, FullPath newPath, FullPath oldPath, IMod? mod)
    {
        // Penumbra.Log.Debug($"[API]{{{collection.Identity.Name}}} | {{{mod}}} | {type} Redirect of [ {gamePath} ] ([ {oldPath} ] -> [ {newPath} ])");
        ResolvedFileChanged?.Invoke(type, collection.Identity.Id, mod?.Name ?? string.Empty, gamePath.ToString(), oldPath.ToString(), newPath.ToString());
    }
}
