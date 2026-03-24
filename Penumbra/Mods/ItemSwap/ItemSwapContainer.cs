using Luna;
using Penumbra.Collections;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;
using Penumbra.Meta;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.Mods.SubMods;

namespace Penumbra.Mods.ItemSwap;

public class ItemSwapContainer
{
    private readonly MetaFileManager      _manager;
    private readonly ObjectIdentification _identifier;

    private AppliedModData _appliedModData = AppliedModData.Empty;

    public IReadOnlyDictionary<Utf8GamePath, FullPath> ModRedirections
        => _appliedModData.FileRedirections;

    public MetaDictionary ModManipulations
        => _appliedModData.Manipulations;

    public readonly List<Swap> Swaps = [];

    public bool Loaded { get; private set; }

    public void Clear()
    {
        Swaps.Clear();
        Loaded = false;
    }

    public enum WriteType
    {
        UseSwaps,
        NoSwaps,
    }

    public bool WriteMod(ModManager manager, DirectoryInfo directory, out MetaDictionary manips, out Dictionary<Utf8GamePath, FullPath> files,
        out Dictionary<Utf8GamePath, FullPath> swaps, WriteType writeType = WriteType.NoSwaps)
    {
        manips = new MetaDictionary();
        files  = new Dictionary<Utf8GamePath, FullPath>(Swaps.Count);
        swaps  = new Dictionary<Utf8GamePath, FullPath>(Swaps.Count);
        try
        {
            foreach (var swap in Swaps.SelectMany(s => s.WithChildren()))
            {
                if (swap is FileSwap file)
                {
                    // Skip, nothing to do
                    if (file.SwapToModdedEqualsOriginal)
                        continue;

                    if (writeType is WriteType.UseSwaps && file is { SwapToModdedExistsInGame: true, DataWasChanged: false })
                    {
                        swaps.TryAdd(file.SwapFromRequestPath, file.SwapToModded);
                    }
                    else
                    {
                        var path  = file.GetNewPath(directory.FullName);
                        var bytes = file.FileData.Write();
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        _manager.Compactor.WriteAllBytes(path, bytes);
                        files.TryAdd(file.SwapFromRequestPath, new FullPath(path));
                    }
                }
                else if (swap is IMetaSwap { SwapAppliedIsDefault: false })
                {
                    // @formatter:off
                    _ = swap switch
                    {
                        MetaSwap<EstIdentifier, EstEntry>           meta => manips.TryAdd(meta.SwapFromIdentifier, meta.SwapToModdedEntry),
                        MetaSwap<EqpIdentifier, EqpEntryInternal>   meta => manips.TryAdd(meta.SwapFromIdentifier, meta.SwapToModdedEntry),
                        MetaSwap<EqdpIdentifier, EqdpEntryInternal> meta => manips.TryAdd(meta.SwapFromIdentifier, meta.SwapToModdedEntry),
                        MetaSwap<ImcIdentifier, ImcEntry>           meta => manips.TryAdd(meta.SwapFromIdentifier, meta.SwapToModdedEntry),
                        MetaSwap<GmpIdentifier, GmpEntry>           meta => manips.TryAdd(meta.SwapFromIdentifier, meta.SwapToModdedEntry),
                        MetaSwap<AtrIdentifier, AtrEntry>           meta => manips.TryAdd(meta.SwapFromIdentifier, meta.SwapToModdedEntry),
                        MetaSwap<ShpIdentifier, ShpEntry>           meta => manips.TryAdd(meta.SwapFromIdentifier, meta.SwapToModdedEntry),
                        _ => false,
                    };
                    // @formatter:on
                }
            }

            return true;
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not write FileSwapContainer to {directory}:\n{e}");
            return false;
        }
    }

    public void LoadMod(Mod? mod, ModSettings? settings)
    {
        Clear();
        if (mod is null || mod.Index < 0)
            _appliedModData = AppliedModData.Empty;
        else
            _appliedModData = ModSettings.GetResolveData(mod, settings);
    }

    public ItemSwapContainer(MetaFileManager manager, ObjectIdentification identifier)
    {
        _manager    = manager;
        _identifier = identifier;
        LoadMod(null, null);
    }

    private Func<Utf8GamePath, FullPath> PathResolver(ModCollection? collection)
        => collection is not null
            ? p => collection.ResolvePath(p) ?? new FullPath(p)
            : p => ModRedirections.TryGetValue(p, out var path) ? path : new FullPath(p);

    private MetaDictionary MetaResolver(ModCollection? collection)
        => collection?.MetaCache is { } cache
            ? new MetaDictionary(cache)
            : _appliedModData.Manipulations;

    public HashSet<EquipItem> LoadEquipment(EquipItem from, EquipItem to, ModCollection? collection = null, bool useRightRing = true,
        bool useLeftRing = true)
    {
        Swaps.Clear();
        Loaded = false;
        var ret = EquipmentSwap.CreateItemSwap(_manager, _identifier, Swaps, PathResolver(collection), MetaResolver(collection),
            from, to, useRightRing, useLeftRing);
        Loaded = true;
        return ret;
    }

    public HashSet<EquipItem> LoadTypeSwap(EquipSlot slotFrom, EquipItem from, EquipSlot slotTo, EquipItem to, ModCollection? collection = null)
    {
        Swaps.Clear();
        Loaded = false;
        var ret = EquipmentSwap.CreateTypeSwap(_manager, _identifier, Swaps, PathResolver(collection), MetaResolver(collection),
            slotFrom, from, slotTo, to);
        Loaded = true;
        return ret;
    }

    public bool LoadCustomization(MetaFileManager manager, BodySlot slot, GenderRace race, PrimaryId from, PrimaryId to,
        ModCollection? collection = null)
    {
        var pathResolver = PathResolver(collection);
        var metaResolver = MetaResolver(collection);
        var mdl          = CustomizationSwap.CreateMdl(manager, pathResolver, metaResolver, slot, race, from, to);
        var type = slot switch
        {
            BodySlot.Hair => EstType.Hair,
            BodySlot.Face => EstType.Face,
            _             => (EstType)0,
        };

        var est = ItemSwap.CreateEst(manager, pathResolver, metaResolver, type, race, from, to, true);

        Swaps.Add(mdl);
        if (est is not null)
            Swaps.Add(est);

        Loaded = true;
        return true;
    }
}
