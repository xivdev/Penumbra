using Penumbra.Communication;
using Penumbra.GameData.Data;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.SubMods;
using Penumbra.Services;
using Penumbra.Util;

namespace Penumbra.Mods.Manager;

public class ModCacheManager : IDisposable
{
    private readonly Configuration        _config;
    private readonly CommunicatorService  _communicator;
    private readonly ObjectIdentification _identifier;
    private readonly ModStorage           _modManager;
    private          bool                 _updatingItems = false;

    public ModCacheManager(CommunicatorService communicator, ObjectIdentification identifier, ModStorage modStorage, Configuration config)
    {
        _communicator = communicator;
        _identifier   = identifier;
        _modManager   = modStorage;
        _config       = config;

        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, ModOptionChanged.Priority.ModCacheManager);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModCacheManager);
        _communicator.ModDataChanged.Subscribe(OnModDataChange, ModDataChanged.Priority.ModCacheManager);
        _communicator.ModDiscoveryFinished.Subscribe(OnModDiscoveryFinished, ModDiscoveryFinished.Priority.ModCacheManager);
        identifier.Awaiter.ContinueWith(_ => OnIdentifierCreation(), TaskScheduler.Default);
        OnModDiscoveryFinished();
    }

    public void Dispose()
    {
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
        _communicator.ModDataChanged.Unsubscribe(OnModDataChange);
        _communicator.ModDiscoveryFinished.Unsubscribe(OnModDiscoveryFinished);
    }

    private void OnModOptionChange(ModOptionChangeType type, Mod mod, IModGroup? group, IModOption? option, IModDataContainer? container,
        int fromIdx)
    {
        switch (type)
        {
            case ModOptionChangeType.GroupAdded:
            case ModOptionChangeType.GroupDeleted:
            case ModOptionChangeType.OptionAdded:
            case ModOptionChangeType.OptionDeleted:
                UpdateChangedItems(mod);
                UpdateCounts(mod);
                break;
            case ModOptionChangeType.GroupTypeChanged:
                UpdateHasOptions(mod);
                break;
            case ModOptionChangeType.OptionFilesChanged:
            case ModOptionChangeType.OptionFilesAdded:
                UpdateChangedItems(mod);
                UpdateFileCount(mod);
                break;
            case ModOptionChangeType.OptionSwapsChanged:
                UpdateChangedItems(mod);
                UpdateSwapCount(mod);
                break;
            case ModOptionChangeType.OptionMetaChanged:
                UpdateChangedItems(mod);
                UpdateMetaCount(mod);
                break;
        }
    }

    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? old, DirectoryInfo? @new)
    {
        switch (type)
        {
            case ModPathChangeType.Added:
            case ModPathChangeType.Reloaded:
                RefreshWithChangedItems(mod);
                break;
        }
    }

    private static void OnModDataChange(ModDataChangeType type, Mod mod, string? _)
    {
        if ((type & (ModDataChangeType.LocalTags | ModDataChangeType.ModTags)) != 0)
            UpdateTags(mod);
    }

    private void OnModDiscoveryFinished()
    {
        if (!_identifier.Awaiter.IsCompletedSuccessfully || _updatingItems)
        {
            Parallel.ForEach(_modManager, RefreshWithoutChangedItems);
        }
        else
        {
            _updatingItems = true;
            Parallel.ForEach(_modManager, RefreshWithChangedItems);
            _updatingItems = false;
        }
    }

    private void OnIdentifierCreation()
    {
        if (_updatingItems)
            return;

        _updatingItems = true;
        Parallel.ForEach(_modManager, UpdateChangedItems);
        _updatingItems = false;
    }

    private static void UpdateFileCount(Mod mod)
        => mod.TotalFileCount = mod.AllDataContainers.Sum(s => s.Files.Count);

    private static void UpdateSwapCount(Mod mod)
        => mod.TotalSwapCount = mod.AllDataContainers.Sum(s => s.FileSwaps.Count);

    private static void UpdateMetaCount(Mod mod)
        => mod.TotalManipulations = mod.AllDataContainers.Sum(s => s.Manipulations.Count);

    private static void UpdateHasOptions(Mod mod)
        => mod.HasOptions = mod.Groups.Any(o => o.IsOption);

    private static void UpdateTags(Mod mod)
        => mod.AllTagsLower = string.Join('\0', mod.ModTags.Concat(mod.LocalTags).Select(s => s.ToLowerInvariant()));

    private void UpdateChangedItems(Mod mod)
    {
        mod.ChangedItems.Clear();

        _identifier.AddChangedItems(mod.Default, mod.ChangedItems);
        foreach (var group in mod.Groups)
            group.AddChangedItems(_identifier, mod.ChangedItems);

        if (_config.HideMachinistOffhandFromChangedItems)
            mod.ChangedItems.RemoveMachinistOffhands();

        mod.LowerChangedItemsString = string.Join("\0", mod.ChangedItems.Keys.Select(k => k.ToLowerInvariant()));
    }

    private static void UpdateCounts(Mod mod)
    {
        mod.TotalFileCount     = mod.Default.Files.Count;
        mod.TotalSwapCount     = mod.Default.FileSwaps.Count;
        mod.TotalManipulations = mod.Default.Manipulations.Count;
        mod.HasOptions         = false;
        foreach (var group in mod.Groups)
        {
            mod.HasOptions             |= group.IsOption;
            var (files, swaps, manips) =  group.GetCounts();
            mod.TotalFileCount         += files;
            mod.TotalSwapCount         += swaps;
            mod.TotalManipulations     += manips;
        }
    }

    private void RefreshWithChangedItems(Mod mod)
    {
        UpdateTags(mod);
        UpdateCounts(mod);
        UpdateChangedItems(mod);
    }

    private void RefreshWithoutChangedItems(Mod mod)
    {
        UpdateTags(mod);
        UpdateCounts(mod);
    }
}
