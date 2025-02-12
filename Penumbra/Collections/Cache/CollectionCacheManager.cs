using Dalamud.Plugin.Services;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Interop.Hooks.ResourceLoading;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.String.Classes;

namespace Penumbra.Collections.Cache;

public class CollectionCacheManager : IDisposable, IService
{
    private readonly  FrameworkManager    _framework;
    private readonly  CommunicatorService _communicator;
    private readonly  TempModManager      _tempMods;
    private readonly  ModStorage          _modStorage;
    private readonly  CollectionStorage   _storage;
    private readonly  ActiveCollections   _active;
    internal readonly Configuration       Config;
    internal readonly ResolvedFileChanged ResolvedFileChanged;
    internal readonly MetaFileManager     MetaFileManager;
    internal readonly ResourceLoader      ResourceLoader;

    private readonly ConcurrentQueue<CollectionCache.ChangeData> _changeQueue = new();

    private int _count;

    public int Count
        => _count;

    public IEnumerable<ModCollection> Active
        => _storage.Where(c => c.HasCache);

    public CollectionCacheManager(FrameworkManager framework, CommunicatorService communicator, TempModManager tempMods, ModStorage modStorage,
        MetaFileManager metaFileManager, ActiveCollections active, CollectionStorage storage, ResourceLoader resourceLoader,
        Configuration config)
    {
        _framework          = framework;
        _communicator       = communicator;
        _tempMods           = tempMods;
        _modStorage         = modStorage;
        MetaFileManager     = metaFileManager;
        _active             = active;
        _storage            = storage;
        ResourceLoader      = resourceLoader;
        Config              = config;
        ResolvedFileChanged = _communicator.ResolvedFileChanged;

        if (!_active.Individuals.IsLoaded)
            _active.Individuals.Loaded += CreateNecessaryCaches;
        _framework.Framework.Update += OnFramework;
        _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.CollectionCacheManager);
        _communicator.ModPathChanged.Subscribe(OnModChangeAddition, ModPathChanged.Priority.CollectionCacheManagerAddition);
        _communicator.ModPathChanged.Subscribe(OnModChangeRemoval,  ModPathChanged.Priority.CollectionCacheManagerRemoval);
        _communicator.TemporaryGlobalModChange.Subscribe(OnGlobalModChange, TemporaryGlobalModChange.Priority.CollectionCacheManager);
        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, ModOptionChanged.Priority.CollectionCacheManager);
        _communicator.ModSettingChanged.Subscribe(OnModSettingChange, ModSettingChanged.Priority.CollectionCacheManager);
        _communicator.CollectionInheritanceChanged.Subscribe(OnCollectionInheritanceChange,
            CollectionInheritanceChanged.Priority.CollectionCacheManager);
        _communicator.ModDiscoveryStarted.Subscribe(OnModDiscoveryStarted, ModDiscoveryStarted.Priority.CollectionCacheManager);
        _communicator.ModDiscoveryFinished.Subscribe(OnModDiscoveryFinished, ModDiscoveryFinished.Priority.CollectionCacheManager);

        if (!MetaFileManager.CharacterUtility.Ready)
            MetaFileManager.CharacterUtility.LoadingFinished.Subscribe(IncrementCounters, CharacterUtilityFinished.Priority.CollectionCacheManager);
    }

    public void Dispose()
    {
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
        _communicator.ModPathChanged.Unsubscribe(OnModChangeAddition);
        _communicator.ModPathChanged.Unsubscribe(OnModChangeRemoval);
        _communicator.TemporaryGlobalModChange.Unsubscribe(OnGlobalModChange);
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
        _communicator.ModSettingChanged.Unsubscribe(OnModSettingChange);
        _communicator.CollectionInheritanceChanged.Unsubscribe(OnCollectionInheritanceChange);
        MetaFileManager.CharacterUtility.LoadingFinished.Unsubscribe(IncrementCounters);

        foreach (var collection in _storage)
        {
            collection._cache?.Dispose();
            collection._cache = null;
        }
    }

    public void AddChange(CollectionCache.ChangeData data)
    {
        if (data.Cache.Calculating == -1)
        {
            if (_framework.Framework.IsInFrameworkUpdateThread)
                data.Apply();
            else
                _changeQueue.Enqueue(data);
        }
        else if (data.Cache.Calculating == Environment.CurrentManagedThreadId)
        {
            data.Apply();
        }
        else
        {
            _changeQueue.Enqueue(data);
        }
    }

    /// <summary> Only creates a new cache, does not update an existing one. </summary>
    public bool CreateCache(ModCollection collection)
    {
        if (collection.Identity.Index == ModCollection.Empty.Identity.Index)
            return false;

        if (collection._cache != null)
            return false;

        collection._cache = new CollectionCache(this, collection);
        if (collection.Identity.Index > 0)
            Interlocked.Increment(ref _count);
        Penumbra.Log.Verbose($"Created new cache for collection {collection.Identity.AnonymizedName}.");
        return true;
    }

    /// <summary>
    /// Update the effective file list for the given cache.
    /// Does not create caches.
    /// </summary>
    public void CalculateEffectiveFileList(ModCollection collection)
        => _framework.RegisterImportant(nameof(CalculateEffectiveFileList) + collection.Identity.Identifier,
            () => CalculateEffectiveFileListInternal(collection));

    private void CalculateEffectiveFileListInternal(ModCollection collection)
    {
        // Skip the empty collection.
        if (collection.Identity.Index == 0)
            return;

        Penumbra.Log.Debug($"[{Environment.CurrentManagedThreadId}] Recalculating effective file list for {collection.Identity.AnonymizedName}");
        if (!collection.HasCache)
        {
            Penumbra.Log.Error(
                $"[{Environment.CurrentManagedThreadId}] Recalculating effective file list for {collection.Identity.AnonymizedName} failed, no cache exists.");
        }
        else if (collection._cache!.Calculating != -1)
        {
            Penumbra.Log.Error(
                $"[{Environment.CurrentManagedThreadId}] Recalculating effective file list for {collection.Identity.AnonymizedName} failed, already in calculation on [{collection._cache!.Calculating}].");
        }
        else
        {
            FullRecalculation(collection);

            Penumbra.Log.Debug(
                $"[{Environment.CurrentManagedThreadId}] Recalculation of effective file list for {collection.Identity.AnonymizedName} finished.");
        }
    }

    private void FullRecalculation(ModCollection collection)
    {
        var cache = collection._cache;
        if (cache is not { Calculating: -1 })
            return;

        cache.Calculating = Environment.CurrentManagedThreadId;
        try
        {
            ResolvedFileChanged.Invoke(collection, ResolvedFileChanged.Type.FullRecomputeStart, Utf8GamePath.Empty, FullPath.Empty,
                FullPath.Empty, null);
            cache.ResolvedFiles.Clear();
            cache.Meta.Reset();
            cache.ConflictDict.Clear();

            // Add all forced redirects.
            foreach (var tempMod in _tempMods.ModsForAllCollections
                         .Concat(_tempMods.Mods.TryGetValue(collection, out var list)
                             ? list
                             : Array.Empty<TemporaryMod>()))
                cache.AddModSync(tempMod, false);

            foreach (var mod in _modStorage)
                cache.AddModSync(mod, false);

            collection.Counters.IncrementChange();

            MetaFileManager.ApplyDefaultFiles(collection);
            ResolvedFileChanged.Invoke(collection, ResolvedFileChanged.Type.FullRecomputeFinished, Utf8GamePath.Empty, FullPath.Empty,
                FullPath.Empty,
                null);
        }
        finally
        {
            cache.Calculating = -1;
        }
    }

    private void OnCollectionChange(CollectionType type, ModCollection? old, ModCollection? newCollection, string displayName)
    {
        if (type is CollectionType.Temporary)
        {
            if (newCollection != null && CreateCache(newCollection))
                CalculateEffectiveFileList(newCollection);

            if (old != null)
                ClearCache(old);
        }
        else
        {
            RemoveCache(old);
            if (type is not CollectionType.Inactive && newCollection != null && newCollection.Identity.Index != 0 && CreateCache(newCollection))
                CalculateEffectiveFileList(newCollection);

            if (type is CollectionType.Default)
                if (newCollection != null)
                    MetaFileManager.ApplyDefaultFiles(newCollection);
                else
                    MetaFileManager.CharacterUtility.ResetAll();
        }
    }


    private void OnModChangeRemoval(ModPathChangeType type, Mod mod, DirectoryInfo? oldModPath, DirectoryInfo? newModPath)
    {
        switch (type)
        {
            case ModPathChangeType.Deleted:
            case ModPathChangeType.StartingReload:
                foreach (var collection in _storage.Where(c => c.HasCache && c.GetActualSettings(mod.Index).Settings?.Enabled == true))
                    collection._cache!.RemoveMod(mod, true);
                break;
            case ModPathChangeType.Moved:
                foreach (var collection in _storage.Where(c => c.HasCache && c.GetActualSettings(mod.Index).Settings?.Enabled == true))
                    collection._cache!.ReloadMod(mod, true);
                break;
        }
    }

    private void OnModChangeAddition(ModPathChangeType type, Mod mod, DirectoryInfo? oldModPath, DirectoryInfo? newModPath)
    {
        if (type is not (ModPathChangeType.Added or ModPathChangeType.Reloaded))
            return;

        foreach (var collection in _storage.Where(c => c.HasCache && c.GetActualSettings(mod.Index).Settings?.Enabled == true))
            collection._cache!.AddMod(mod, true);
    }

    /// <summary> Apply a mod change to all collections with a cache. </summary>
    private void OnGlobalModChange(TemporaryMod mod, bool created, bool removed)
        => TempModManager.OnGlobalModChange(_storage.Where(c => c.HasCache), mod, created, removed);

    /// <summary> Remove a cache from a collection if it is active. </summary>
    private void RemoveCache(ModCollection? collection)
    {
        if (collection != null
         && collection.Identity.Index > ModCollection.Empty.Identity.Index
         && collection.Identity.Index != _active.Default.Identity.Index
         && collection.Identity.Index != _active.Interface.Identity.Index
         && collection.Identity.Index != _active.Current.Identity.Index
         && _active.SpecialAssignments.All(c => c.Value.Identity.Index != collection.Identity.Index)
         && _active.Individuals.All(c => c.Collection.Identity.Index != collection.Identity.Index))
            ClearCache(collection);
    }

    /// <summary> Prepare Changes by removing mods from caches with collections or add or reload mods. </summary>
    private void OnModOptionChange(ModOptionChangeType type, Mod mod, IModGroup? group, IModOption? option, IModDataContainer? container,
        int movedToIdx)
    {
        if (type is ModOptionChangeType.PrepareChange)
        {
            foreach (var collection in _storage.Where(collection => collection.HasCache && collection.GetActualSettings(mod.Index).Settings is { Enabled: true }))
                collection._cache!.RemoveMod(mod, false);

            return;
        }

        type.HandlingInfo(out _, out var recomputeList, out var justAdd);

        if (!recomputeList)
            return;

        foreach (var collection in _storage.Where(collection => collection.HasCache && collection.GetActualSettings(mod.Index).Settings is { Enabled: true }))
        {
            if (justAdd)
                collection._cache!.AddMod(mod, true);
            else
                collection._cache!.ReloadMod(mod, true);
        }
    }

    /// <summary> Increment the counter to ensure new files are loaded after applying meta changes. </summary>
    private void IncrementCounters()
    {
        foreach (var collection in _storage.Where(c => c.HasCache))
            collection.Counters.IncrementChange();
        MetaFileManager.CharacterUtility.LoadingFinished.Unsubscribe(IncrementCounters);
    }

    private void OnModSettingChange(ModCollection collection, ModSettingChange type, Mod? mod, Setting oldValue, int groupIdx, bool _)
    {
        if (!collection.HasCache)
            return;

        var cache = collection._cache!;
        switch (type)
        {
            case ModSettingChange.Inheritance:
                cache.ReloadMod(mod!, true);
                break;
            case ModSettingChange.EnableState:
                if (oldValue == Setting.False)
                    cache.AddMod(mod!, true);
                else if (oldValue == Setting.True)
                    cache.RemoveMod(mod!, true);
                else if (collection.GetActualSettings(mod!.Index).Settings?.Enabled == true)
                    cache.ReloadMod(mod!, true);
                else
                    cache.RemoveMod(mod!, true);

                break;
            case ModSettingChange.Priority:
                if (cache.Conflicts(mod!).Count > 0)
                    cache.ReloadMod(mod!, true);

                break;
            case ModSettingChange.Setting:
                if (collection.GetActualSettings(mod!.Index).Settings?.Enabled == true)
                    cache.ReloadMod(mod, true);

                break;
            case ModSettingChange.TemporarySetting:
                cache.ReloadMod(mod!, true);
                break;
            case ModSettingChange.MultiInheritance:
            case ModSettingChange.MultiEnableState:
                FullRecalculation(collection);
                break;
            case ModSettingChange.TemporaryMod:
            case ModSettingChange.Edited:
                // handled otherwise
                break;
        }
    }

    /// <summary>
    /// Inheritance changes are too big to check for relevance,
    /// just recompute everything.
    /// </summary>
    private void OnCollectionInheritanceChange(ModCollection collection, bool _)
        => FullRecalculation(collection);

    /// <summary> Clear the current cache of a collection. </summary>
    private void ClearCache(ModCollection collection)
    {
        if (!collection.HasCache)
            return;

        collection._cache!.Dispose();
        collection._cache = null;
        if (collection.Identity.Index > 0)
            Interlocked.Decrement(ref _count);
        Penumbra.Log.Verbose($"Cleared cache of collection {collection.Identity.AnonymizedName}.");
    }

    /// <summary>
    /// Cache handling. Usually recreate caches on the next framework tick,
    /// but at launch create all of them at once.
    /// </summary>
    public void CreateNecessaryCaches()
    {
        ModCollection[] caches;
        // Lock to make sure no race conditions during CreateCache happen.
        lock (this)
        {
            caches = _active.SpecialAssignments.Select(p => p.Value)
                .Concat(_active.Individuals.Select(p => p.Collection))
                .Prepend(_active.Current)
                .Prepend(_active.Default)
                .Prepend(_active.Interface)
                .Where(CreateCache).ToArray();
        }

        Parallel.ForEach(caches, CalculateEffectiveFileListInternal);
    }

    private void OnModDiscoveryStarted()
    {
        foreach (var collection in Active)
        {
            collection._cache!.ResolvedFiles.Clear();
            collection._cache!.Meta.Reset();
            collection._cache!.ConflictDict.Clear();
        }
    }

    private void OnModDiscoveryFinished()
        => Parallel.ForEach(Active, CalculateEffectiveFileListInternal);

    /// <summary>
    /// Update forced files only on framework.
    /// </summary>
    private void OnFramework(IFramework _)
    {
        while (_changeQueue.TryDequeue(out var changeData))
            changeData.Apply();
    }
}
