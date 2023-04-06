using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Penumbra.Api;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.Collections.Manager;

public class CollectionCacheManager : IDisposable, IReadOnlyDictionary<ModCollection, ModCollectionCache>
{
    private readonly ActiveCollections   _active;
    private readonly CommunicatorService _communicator;

    private readonly Dictionary<ModCollection, ModCollectionCache> _cache = new();

    public int Count
        => _cache.Count;

    public IEnumerator<KeyValuePair<ModCollection, ModCollectionCache>> GetEnumerator()
        => _cache.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public bool ContainsKey(ModCollection key)
        => _cache.ContainsKey(key);

    public bool TryGetValue(ModCollection key, [NotNullWhen(true)] out ModCollectionCache? value)
        => _cache.TryGetValue(key, out value);

    public ModCollectionCache this[ModCollection key]
        => _cache[key];

    public IEnumerable<ModCollection> Keys
        => _cache.Keys;

    public IEnumerable<ModCollectionCache> Values
        => _cache.Values;

    public IEnumerable<ModCollection> Active
        => _cache.Keys.Where(c => c.Index > ModCollection.Empty.Index);

    public CollectionCacheManager(ActiveCollections active, CommunicatorService communicator)
    {
        _active       = active;
        _communicator = communicator;

        _communicator.CollectionChange.Subscribe(OnCollectionChange);
        _communicator.ModPathChanged.Subscribe(OnModChangeAddition, -100);
        _communicator.ModPathChanged.Subscribe(OnModChangeRemoval,  100);
        _communicator.TemporaryGlobalModChange.Subscribe(OnGlobalModChange);
        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, -100);
        CreateNecessaryCaches();
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
        _communicator.ModPathChanged.Unsubscribe(OnModChangeAddition);
        _communicator.ModPathChanged.Unsubscribe(OnModChangeRemoval);
        _communicator.TemporaryGlobalModChange.Unsubscribe(OnGlobalModChange);
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
    }

    /// <summary>
    /// Cache handling. Usually recreate caches on the next framework tick,
    /// but at launch create all of them at once.
    /// </summary>
    public void CreateNecessaryCaches()
    {
        var tasks = _active.SpecialAssignments.Select(p => p.Value)
            .Concat(_active.Individuals.Select(p => p.Collection))
            .Prepend(_active.Current)
            .Prepend(_active.Default)
            .Prepend(_active.Interface)
            .Distinct()
            .Select(c => Task.Run(() => c.CalculateEffectiveFileListInternal(c == _active.Default)))
            .ToArray();

        Task.WaitAll(tasks);
    }

    private void OnCollectionChange(CollectionType type, ModCollection? old, ModCollection? newCollection, string displayName)
    {
        if (type is CollectionType.Inactive)
            return;

        var isDefault = type is CollectionType.Default;
        if (newCollection?.Index > ModCollection.Empty.Index)
        {
            newCollection.CreateCache(isDefault);
            _cache.TryAdd(newCollection, newCollection._cache!);
        }

        RemoveCache(old);
    }

    private void OnModChangeRemoval(ModPathChangeType type, Mod mod, DirectoryInfo? oldModPath, DirectoryInfo? newModPath)
    {
        switch (type)
        {
            case ModPathChangeType.Deleted:
            case ModPathChangeType.StartingReload:
                foreach (var collection in _cache.Keys.Where(c => c[mod.Index].Settings?.Enabled == true))
                    collection._cache!.RemoveMod(mod, true);
                break;
            case ModPathChangeType.Moved:
                foreach (var collection in _cache.Keys.Where(c => c.HasCache && c[mod.Index].Settings?.Enabled == true))
                    collection._cache!.ReloadMod(mod, true);
                break;
        }
    }

    private void OnModChangeAddition(ModPathChangeType type, Mod mod, DirectoryInfo? oldModPath, DirectoryInfo? newModPath)
    {
        if (type is not (ModPathChangeType.Added or ModPathChangeType.Reloaded))
            return;

        foreach (var collection in _cache.Keys.Where(c => c[mod.Index].Settings?.Enabled == true))
            collection._cache!.AddMod(mod, true);
    }

    /// <summary> Apply a mod change to all collections with a cache. </summary>
    private void OnGlobalModChange(TemporaryMod mod, bool created, bool removed)
        => TempModManager.OnGlobalModChange(_cache.Keys, mod, created, removed);

    /// <summary> Remove a cache from a collection if it is active. </summary>
    private void RemoveCache(ModCollection? collection)
    {
        if (collection != null
         && collection.Index > ModCollection.Empty.Index
         && collection.Index != _active.Default.Index
         && collection.Index != _active.Interface.Index
         && collection.Index != _active.Current.Index
         && _active.SpecialAssignments.All(c => c.Value.Index != collection.Index)
         && _active.Individuals.All(c => c.Collection.Index != collection.Index))
        {
            _cache.Remove(collection);
            collection.ClearCache();
        }
    }

    /// <summary> Prepare Changes by removing mods from caches with collections or add or reload mods. </summary>
    private void OnModOptionChange(ModOptionChangeType type, Mod mod, int groupIdx, int optionIdx, int movedToIdx)
    {
        if (type is ModOptionChangeType.PrepareChange)
        {
            foreach (var collection in _cache.Keys.Where(collection => collection[mod.Index].Settings is { Enabled: true }))
                collection._cache!.RemoveMod(mod, false);

            return;
        }

        type.HandlingInfo(out _, out var recomputeList, out var reload);

        if (!recomputeList)
            return;

        foreach (var collection in _cache.Keys.Where(collection => collection[mod.Index].Settings is { Enabled: true }))
        {
            if (reload)
                collection._cache!.ReloadMod(mod, true);
            else
                collection._cache!.AddMod(mod, true);
        }
    }
}
