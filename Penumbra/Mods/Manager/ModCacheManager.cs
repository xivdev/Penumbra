using Penumbra.Communication;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.Services;

namespace Penumbra.Mods.Manager;

public class ModCacheManager : IDisposable
{
    private readonly CommunicatorService _communicator;
    private readonly IdentifierService   _identifier;
    private readonly ModStorage          _modManager;

    public ModCacheManager(CommunicatorService communicator, IdentifierService identifier, ModStorage modStorage)
    {
        _communicator = communicator;
        _identifier   = identifier;
        _modManager   = modStorage;

        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, ModOptionChanged.Priority.ModCacheManager);
        _communicator.ModPathChanged.Subscribe(OnModPathChange, ModPathChanged.Priority.ModCacheManager);
        _communicator.ModDataChanged.Subscribe(OnModDataChange, ModDataChanged.Priority.ModCacheManager);
        _communicator.ModDiscoveryFinished.Subscribe(OnModDiscoveryFinished, ModDiscoveryFinished.Priority.ModCacheManager);
        if (!identifier.Valid)
            identifier.FinishedCreation += OnIdentifierCreation;
        OnModDiscoveryFinished();
    }

    public void Dispose()
    {
        _communicator.ModOptionChanged.Unsubscribe(OnModOptionChange);
        _communicator.ModPathChanged.Unsubscribe(OnModPathChange);
        _communicator.ModDataChanged.Unsubscribe(OnModDataChange);
        _communicator.ModDiscoveryFinished.Unsubscribe(OnModDiscoveryFinished);
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
                Refresh(mod);
                break;
        }
    }

    private static void OnModDataChange(ModDataChangeType type, Mod mod, string? _)
    {
        if ((type & (ModDataChangeType.LocalTags | ModDataChangeType.ModTags)) != 0)
            UpdateTags(mod);
    }

    private void OnModDiscoveryFinished()
        => Parallel.ForEach(_modManager, Refresh);

    private void OnIdentifierCreation()
    {
        Parallel.ForEach(_modManager, UpdateChangedItems);
        _identifier.FinishedCreation -= OnIdentifierCreation;
    }

    private static void UpdateFileCount(Mod mod)
        => mod.TotalFileCount = mod.AllSubMods.Sum(s => s.Files.Count);

    private static void UpdateSwapCount(Mod mod)
        => mod.TotalSwapCount = mod.AllSubMods.Sum(s => s.FileSwaps.Count);

    private static void UpdateMetaCount(Mod mod)
        => mod.TotalManipulations = mod.AllSubMods.Sum(s => s.Manipulations.Count);

    private static void UpdateHasOptions(Mod mod)
        => mod.HasOptions = mod.Groups.Any(o => o.IsOption);

    private static void UpdateTags(Mod mod)
        => mod.AllTagsLower = string.Join('\0', mod.ModTags.Concat(mod.LocalTags).Select(s => s.ToLowerInvariant()));

    private void UpdateChangedItems(Mod mod)
    {
        var changedItems = (SortedList<string, object?>)mod.ChangedItems;
        changedItems.Clear();
        if (!_identifier.Valid)
            return;

        foreach (var gamePath in mod.AllSubMods.SelectMany(m => m.Files.Keys.Concat(m.FileSwaps.Keys)))
            _identifier.AwaitedService.Identify(changedItems, gamePath.ToString());

        foreach (var manip in mod.AllSubMods.SelectMany(m => m.Manipulations))
            ComputeChangedItems(_identifier.AwaitedService, changedItems, manip);

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
            mod.HasOptions |= group.IsOption;
            foreach (var s in group)
            {
                mod.TotalFileCount     += s.Files.Count;
                mod.TotalSwapCount     += s.FileSwaps.Count;
                mod.TotalManipulations += s.Manipulations.Count;
            }
        }
    }

    private void Refresh(Mod mod)
    {
        UpdateTags(mod);
        UpdateCounts(mod);
        UpdateChangedItems(mod);
    }
}
