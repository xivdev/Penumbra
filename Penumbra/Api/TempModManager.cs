using Penumbra.Collections;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.Services;
using Penumbra.String.Classes;
using Penumbra.Collections.Manager;
using Penumbra.Communication;

namespace Penumbra.Api;

public enum RedirectResult
{
    Success                 = 0,
    IdenticalFileRegistered = 1,
    NotRegistered           = 2,
    FilteredGamePath        = 3,
}

public class TempModManager : IDisposable
{
    private readonly CommunicatorService _communicator;

    private readonly Dictionary<ModCollection, List<TemporaryMod>> _mods                  = new();
    private readonly List<TemporaryMod>                            _modsForAllCollections = new();

    public TempModManager(CommunicatorService communicator)
    {
        _communicator = communicator;
        _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.TempModManager);
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
    }

    public IReadOnlyDictionary<ModCollection, List<TemporaryMod>> Mods
        => _mods;

    public IReadOnlyList<TemporaryMod> ModsForAllCollections
        => _modsForAllCollections;

    public RedirectResult Register(string tag, ModCollection? collection, Dictionary<Utf8GamePath, FullPath> dict,
        HashSet<MetaManipulation> manips, int priority)
    {
        var mod = GetOrCreateMod(tag, collection, priority, out var created);
        Penumbra.Log.Verbose($"{(created ? "Created" : "Changed")} temporary Mod {mod.Name}.");
        mod.SetAll(dict, manips);
        ApplyModChange(mod, collection, created, false);
        return RedirectResult.Success;
    }

    public RedirectResult Unregister(string tag, ModCollection? collection, int? priority)
    {
        Penumbra.Log.Verbose($"Removing temporary mod with tag {tag}...");
        var list = collection == null ? _modsForAllCollections : _mods.TryGetValue(collection, out var l) ? l : null;
        if (list == null)
            return RedirectResult.NotRegistered;

        var removed = list.RemoveAll(m =>
        {
            if (m.Name != tag || priority != null && m.Priority != priority.Value)
                return false;

            ApplyModChange(m, collection, false, true);
            return true;
        });

        if (removed == 0)
            return RedirectResult.NotRegistered;

        if (list.Count == 0 && collection != null)
            _mods.Remove(collection);

        return RedirectResult.Success;
    }

    // Apply any new changes to the temporary mod.
    private void ApplyModChange(TemporaryMod mod, ModCollection? collection, bool created, bool removed)
    {
        if (collection != null)
        {
            if (removed)
            {
                Penumbra.Log.Verbose($"Removing temporary Mod {mod.Name} from {collection.AnonymizedName}.");
                collection.Remove(mod);
            }
            else
            {
                Penumbra.Log.Verbose($"Adding {(created ? "new " : string.Empty)}temporary Mod {mod.Name} to {collection.AnonymizedName}.");
                collection.Apply(mod, created);
            }
        }
        else
        {
            Penumbra.Log.Verbose($"Triggering global mod change for {(created ? "new " : string.Empty)}temporary Mod {mod.Name}.");
            _communicator.TemporaryGlobalModChange.Invoke(mod, created, removed);
        }
    }

    /// <summary>
    /// Apply a mod change to a set of collections.
    /// </summary>
    public static void OnGlobalModChange(IEnumerable<ModCollection> collections, TemporaryMod mod, bool created, bool removed)
    {
        if (removed)
            foreach (var c in collections)
                c.Remove(mod);
        else
            foreach (var c in collections)
                c.Apply(mod, created);
    }

    // Find or create a mod with the given tag as name and the given priority, for the given collection (or all collections).
    // Returns the found or created mod and whether it was newly created.
    private TemporaryMod GetOrCreateMod(string tag, ModCollection? collection, int priority, out bool created)
    {
        List<TemporaryMod> list;
        if (collection == null)
        {
            list = _modsForAllCollections;
        }
        else if (_mods.TryGetValue(collection, out var l))
        {
            list = l;
        }
        else
        {
            list = new List<TemporaryMod>();
            _mods.Add(collection, list);
        }

        var mod = list.Find(m => m.Priority == priority && m.Name == tag);
        if (mod == null)
        {
            mod = new TemporaryMod()
            {
                Name     = tag,
                Priority = priority,
            };
            list.Add(mod);
            created = true;
        }
        else
        {
            created = false;
        }

        return mod;
    }

    private void OnCollectionChange(CollectionType collectionType, ModCollection? oldCollection, ModCollection? newCollection,
        string _)
    {
        if (collectionType is CollectionType.Temporary or CollectionType.Inactive && newCollection == null && oldCollection != null)
            _mods.Remove(oldCollection);
    }
}
