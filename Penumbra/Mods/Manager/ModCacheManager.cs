using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.Services;

namespace Penumbra.Mods.Manager;

public class ModCacheManager : IDisposable, IReadOnlyList<ModCache>
{
    private readonly CommunicatorService _communicator;
    private readonly IdentifierService _identifier;
    private readonly IReadOnlyList<Mod> _modManager;

    private readonly List<ModCache> _cache = new();

    public ModCacheManager(CommunicatorService communicator, IdentifierService identifier, ModManager modManager)
    {
        _communicator = communicator;
        _identifier = identifier;
        _modManager = modManager;

        _communicator.ModOptionChanged.Event += OnModOptionChange;
        _communicator.ModPathChanged.Event += OnModPathChange;
        _communicator.ModDataChanged.Event += OnModDataChange;
        _communicator.ModDiscoveryFinished.Event += OnModDiscoveryFinished;
        if (!identifier.Valid)
            identifier.FinishedCreation += OnIdentifierCreation;
        OnModDiscoveryFinished();
    }

    public IEnumerator<ModCache> GetEnumerator()
        => _cache.Take(Count).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count { get; private set; }

    public ModCache this[int index]
        => _cache[index];

    public ModCache this[Mod mod]
        => _cache[mod.Index];

    public void Dispose()
    {
        _communicator.ModOptionChanged.Event -= OnModOptionChange;
        _communicator.ModPathChanged.Event -= OnModPathChange;
        _communicator.ModDataChanged.Event -= OnModDataChange;
        _communicator.ModDiscoveryFinished.Event -= OnModDiscoveryFinished;
    }

    /// <summary> Compute the items changed by a given meta manipulation and put them into the changedItems dictionary. </summary>
    public static void ComputeChangedItems(IObjectIdentifier identifier, IDictionary<string, object?> changedItems, MetaManipulation manip)
    {
        switch (manip.ManipulationType)
        {
            case MetaManipulation.Type.Imc:
                switch (manip.Imc.ObjectType)
                {
                    case ObjectType.Equipment:
                    case ObjectType.Accessory:
                        identifier.Identify(changedItems,
                            GamePaths.Equipment.Mtrl.Path(manip.Imc.PrimaryId, GenderRace.MidlanderMale, manip.Imc.EquipSlot, manip.Imc.Variant,
                                "a"));
                        break;
                    case ObjectType.Weapon:
                        identifier.Identify(changedItems,
                            GamePaths.Weapon.Mtrl.Path(manip.Imc.PrimaryId, manip.Imc.SecondaryId, manip.Imc.Variant, "a"));
                        break;
                    case ObjectType.DemiHuman:
                        identifier.Identify(changedItems,
                            GamePaths.DemiHuman.Mtrl.Path(manip.Imc.PrimaryId, manip.Imc.SecondaryId, manip.Imc.EquipSlot, manip.Imc.Variant,
                                "a"));
                        break;
                    case ObjectType.Monster:
                        identifier.Identify(changedItems,
                            GamePaths.Monster.Mtrl.Path(manip.Imc.PrimaryId, manip.Imc.SecondaryId, manip.Imc.Variant, "a"));
                        break;
                }

                break;
            case MetaManipulation.Type.Eqdp:
                identifier.Identify(changedItems,
                    GamePaths.Equipment.Mdl.Path(manip.Eqdp.SetId, Names.CombinedRace(manip.Eqdp.Gender, manip.Eqdp.Race), manip.Eqdp.Slot));
                break;
            case MetaManipulation.Type.Eqp:
                identifier.Identify(changedItems, GamePaths.Equipment.Mdl.Path(manip.Eqp.SetId, GenderRace.MidlanderMale, manip.Eqp.Slot));
                break;
            case MetaManipulation.Type.Est:
                switch (manip.Est.Slot)
                {
                    case EstManipulation.EstType.Hair:
                        changedItems.TryAdd($"Customization: {manip.Est.Race} {manip.Est.Gender} Hair (Hair) {manip.Est.SetId}", null);
                        break;
                    case EstManipulation.EstType.Face:
                        changedItems.TryAdd($"Customization: {manip.Est.Race} {manip.Est.Gender} Face (Face) {manip.Est.SetId}", null);
                        break;
                    case EstManipulation.EstType.Body:
                        identifier.Identify(changedItems,
                            GamePaths.Equipment.Mdl.Path(manip.Est.SetId, Names.CombinedRace(manip.Est.Gender, manip.Est.Race),
                                EquipSlot.Body));
                        break;
                    case EstManipulation.EstType.Head:
                        identifier.Identify(changedItems,
                            GamePaths.Equipment.Mdl.Path(manip.Est.SetId, Names.CombinedRace(manip.Est.Gender, manip.Est.Race),
                                EquipSlot.Head));
                        break;
                }

                break;
            case MetaManipulation.Type.Gmp:
                identifier.Identify(changedItems, GamePaths.Equipment.Mdl.Path(manip.Gmp.SetId, GenderRace.MidlanderMale, EquipSlot.Head));
                break;
            case MetaManipulation.Type.Rsp:
                changedItems.TryAdd($"{manip.Rsp.SubRace.ToName()} {manip.Rsp.Attribute.ToFullString()}", null);
                break;
        }
    }

    private void OnModOptionChange(ModOptionChangeType type, Mod mod, int groupIdx, int _, int _2)
    {
        ModCache cache;
        switch (type)
        {
            case ModOptionChangeType.GroupAdded:
            case ModOptionChangeType.GroupDeleted:
            case ModOptionChangeType.OptionAdded:
            case ModOptionChangeType.OptionDeleted:
                cache = EnsureCount(mod);
                UpdateChangedItems(cache, mod);
                UpdateCounts(cache, mod);
                break;
            case ModOptionChangeType.GroupTypeChanged:
                UpdateHasOptions(EnsureCount(mod), mod);
                break;
            case ModOptionChangeType.OptionFilesChanged:
            case ModOptionChangeType.OptionFilesAdded:
                cache = EnsureCount(mod);
                UpdateChangedItems(cache, mod);
                UpdateFileCount(cache, mod);
                break;
            case ModOptionChangeType.OptionSwapsChanged:
                cache = EnsureCount(mod);
                UpdateChangedItems(cache, mod);
                UpdateSwapCount(cache, mod);
                break;
            case ModOptionChangeType.OptionMetaChanged:
                cache = EnsureCount(mod);
                UpdateChangedItems(cache, mod);
                UpdateMetaCount(cache, mod);
                break;
        }
    }

    private void OnModPathChange(ModPathChangeType type, Mod mod, DirectoryInfo? old, DirectoryInfo? @new)
    {
        switch (type)
        {
            case ModPathChangeType.Added:
            case ModPathChangeType.Reloaded:
                Refresh(EnsureCount(mod), mod);
                break;
            case ModPathChangeType.Deleted:
                --Count;
                var oldCache = _cache[mod.Index];
                oldCache.Reset();
                for (var i = mod.Index; i < Count; ++i)
                    _cache[i] = _cache[i + 1];
                _cache[Count] = oldCache;
                break;
        }
    }

    private void OnModDataChange(ModDataChangeType type, Mod mod, string? _)
    {
        if ((type & (ModDataChangeType.LocalTags | ModDataChangeType.ModTags)) != 0)
            UpdateTags(EnsureCount(mod), mod);
    }

    private void OnModDiscoveryFinished()
    {
        if (_modManager.Count > _cache.Count)
            _cache.AddRange(Enumerable.Range(0, _modManager.Count - _cache.Count).Select(_ => new ModCache()));

        Parallel.ForEach(Enumerable.Range(0, _modManager.Count), idx => { Refresh(_cache[idx], _modManager[idx]); });
        Count = _modManager.Count;
    }

    private void OnIdentifierCreation()
    {
        Parallel.ForEach(Enumerable.Range(0, _modManager.Count), idx => { UpdateChangedItems(_cache[idx], _modManager[idx]); });
        _identifier.FinishedCreation -= OnIdentifierCreation;
    }

    private static void UpdateFileCount(ModCache cache, Mod mod)
        => cache.TotalFileCount = mod.AllSubMods.Sum(s => s.Files.Count);

    private static void UpdateSwapCount(ModCache cache, Mod mod)
        => cache.TotalFileCount = mod.AllSubMods.Sum(s => s.FileSwaps.Count);

    private static void UpdateMetaCount(ModCache cache, Mod mod)
        => cache.TotalFileCount = mod.AllSubMods.Sum(s => s.Manipulations.Count);

    private static void UpdateHasOptions(ModCache cache, Mod mod)
        => cache.HasOptions = mod.Groups.Any(o => o.IsOption);

    private static void UpdateTags(ModCache cache, Mod mod)
        => cache.AllTagsLower = string.Join('\0', mod.ModTags.Concat(mod.LocalTags).Select(s => s.ToLowerInvariant()));

    private void UpdateChangedItems(ModCache cache, Mod mod)
    {
        cache.ChangedItems.Clear();
        if (!_identifier.Valid)
            return;

        foreach (var gamePath in mod.AllRedirects)
            _identifier.AwaitedService.Identify(cache.ChangedItems, gamePath.ToString());

        foreach (var manip in mod.AllManipulations)
            ComputeChangedItems(_identifier.AwaitedService, cache.ChangedItems, manip);

        cache.LowerChangedItemsString = string.Join("\0", cache.ChangedItems.Keys.Select(k => k.ToLowerInvariant()));
    }

    private static void UpdateCounts(ModCache cache, Mod mod)
    {
        cache.TotalFileCount = mod.Default.Files.Count;
        cache.TotalSwapCount = mod.Default.FileSwaps.Count;
        cache.TotalManipulations = mod.Default.Manipulations.Count;
        cache.HasOptions = false;
        foreach (var group in mod.Groups)
        {
            cache.HasOptions |= group.IsOption;
            foreach (var s in group)
            {
                cache.TotalFileCount += s.Files.Count;
                cache.TotalSwapCount += s.FileSwaps.Count;
                cache.TotalManipulations += s.Manipulations.Count;
            }
        }
    }

    private void Refresh(ModCache cache, Mod mod)
    {
        UpdateTags(cache, mod);
        UpdateCounts(cache, mod);
        UpdateChangedItems(cache, mod);
    }

    private ModCache EnsureCount(Mod mod)
    {
        if (mod.Index < Count)
            return _cache[mod.Index];


        if (mod.Index >= _cache.Count)
            _cache.AddRange(Enumerable.Range(0, mod.Index + 1 - _cache.Count).Select(_ => new ModCache()));
        for (var i = Count; i < mod.Index; ++i)
            Refresh(_cache[i], _modManager[i]);
        Count = mod.Index + 1;
        return _cache[mod.Index];
    }
}
