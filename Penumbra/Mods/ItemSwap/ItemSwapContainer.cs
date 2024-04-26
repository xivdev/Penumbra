using OtterGui.Classes;
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

    public IReadOnlySet<MetaManipulation> ModManipulations
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

    public bool WriteMod(ModManager manager, Mod mod, IModDataContainer container, WriteType writeType = WriteType.NoSwaps, DirectoryInfo? directory = null)
    {
        var convertedManips = new HashSet<MetaManipulation>(Swaps.Count);
        var convertedFiles  = new Dictionary<Utf8GamePath, FullPath>(Swaps.Count);
        var convertedSwaps  = new Dictionary<Utf8GamePath, FullPath>(Swaps.Count);
        directory ??= mod.ModPath;
        try
        {
            foreach (var swap in Swaps.SelectMany(s => s.WithChildren()))
            {
                switch (swap)
                {
                    case FileSwap file:
                        // Skip, nothing to do
                        if (file.SwapToModdedEqualsOriginal)
                            continue;

                        if (writeType == WriteType.UseSwaps && file.SwapToModdedExistsInGame && !file.DataWasChanged)
                        {
                            convertedSwaps.TryAdd(file.SwapFromRequestPath, file.SwapToModded);
                        }
                        else
                        {
                            var path  = file.GetNewPath(directory.FullName);
                            var bytes = file.FileData.Write();
                            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                            _manager.Compactor.WriteAllBytes(path, bytes);
                            convertedFiles.TryAdd(file.SwapFromRequestPath, new FullPath(path));
                        }

                        break;
                    case MetaSwap meta:
                        if (!meta.SwapAppliedIsDefault)
                            convertedManips.Add(meta.SwapApplied);

                        break;
                }
            }

            manager.OptionEditor.SetFiles(container, convertedFiles, SaveType.None);
            manager.OptionEditor.SetFileSwaps(container, convertedSwaps, SaveType.None);
            manager.OptionEditor.SetManipulations(container, convertedManips, SaveType.ImmediateSync);
            return true;
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Could not write FileSwapContainer to {mod.ModPath}:\n{e}");
            return false;
        }
    }

    public void LoadMod(Mod? mod, ModSettings? settings)
    {
        Clear();
        if (mod == null || mod.Index < 0)
        {
            _appliedModData  = AppliedModData.Empty;
        }
        else
        {
            _appliedModData = ModSettings.GetResolveData(mod, settings);
        }
    }

    public ItemSwapContainer(MetaFileManager manager, ObjectIdentification identifier)
    {
        _manager    = manager;
        _identifier = identifier;
        LoadMod(null, null);
    }

    private Func<Utf8GamePath, FullPath> PathResolver(ModCollection? collection)
        => collection != null
            ? p => collection.ResolvePath(p) ?? new FullPath(p)
            : p => ModRedirections.TryGetValue(p, out var path) ? path : new FullPath(p);

    private Func<MetaManipulation, MetaManipulation> MetaResolver(ModCollection? collection)
    {
        var set = collection?.MetaCache?.Manipulations.ToHashSet() ?? _appliedModData.Manipulations;
        return m => set.TryGetValue(m, out var a) ? a : m;
    }

    public EquipItem[] LoadEquipment(EquipItem from, EquipItem to, ModCollection? collection = null, bool useRightRing = true,
        bool useLeftRing = true)
    {
        Swaps.Clear();
        Loaded = false;
        var ret = EquipmentSwap.CreateItemSwap(_manager, _identifier, Swaps, PathResolver(collection), MetaResolver(collection),
            from, to, useRightRing, useLeftRing);
        Loaded = true;
        return ret;
    }

    public EquipItem[] LoadTypeSwap(EquipSlot slotFrom, EquipItem from, EquipSlot slotTo, EquipItem to, ModCollection? collection = null)
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
        var mdl          = CustomizationSwap.CreateMdl(manager, pathResolver, slot, race, from, to);
        var type = slot switch
        {
            BodySlot.Hair => EstManipulation.EstType.Hair,
            BodySlot.Face => EstManipulation.EstType.Face,
            _             => (EstManipulation.EstType)0,
        };

        var metaResolver = MetaResolver(collection);
        var est          = ItemSwap.CreateEst(manager, pathResolver, metaResolver, type, race, from, to, true);

        Swaps.Add(mdl);
        if (est != null)
            Swaps.Add(est);

        Loaded = true;
        return true;
    }
}
