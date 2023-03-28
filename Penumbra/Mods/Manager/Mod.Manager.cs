using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Penumbra.Services;
using Penumbra.Util;

namespace Penumbra.Mods;

public sealed class ModManager2 : IReadOnlyList<Mod>, IDisposable
{
    public readonly ModDataEditor   DataEditor;
    public readonly ModOptionEditor OptionEditor;

    /// <summary>
    /// An easily accessible set of new mods.
    /// Mods are added when they are created or imported.
    /// Mods are removed when they are deleted or when they are toggled in any collection.
    /// Also gets cleared on mod rediscovery.
    /// </summary>
    public readonly HashSet<Mod> NewMods = new();

    public Mod this[int idx]
        => _mods[idx];

    public Mod this[Index idx]
        => _mods[idx];

    public int Count
        => _mods.Count;

    public IEnumerator<Mod> GetEnumerator()
        => _mods.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    /// <summary>
    /// Try to obtain a mod by its directory name (unique identifier, preferred),
    /// or the first mod of the given name if no directory fits.
    /// </summary>
    public bool TryGetMod(string identifier, string modName, [NotNullWhen(true)] out Mod? mod)
    {
        mod = null;
        foreach (var m in _mods)
        {
            if (string.Equals(m.Identifier, identifier, StringComparison.OrdinalIgnoreCase))
            {
                mod = m;
                return true;
            }

            if (m.Name == modName)
                mod ??= m;
        }

        return mod != null;
    }

    /// <summary> The actual list of mods. </summary>
    private readonly List<Mod> _mods = new();

    public ModManager2(ModDataEditor dataEditor, ModOptionEditor optionEditor)
    {
        DataEditor   = dataEditor;
        OptionEditor = optionEditor;
    }

    public void Dispose()
    { }
}

public sealed partial class ModManager : IReadOnlyList<Mod>, IDisposable
{
    // Set when reading Config and migrating from v4 to v5.
    public static bool MigrateModBackups = false;

    // An easily accessible set of new mods.
    // Mods are added when they are created or imported.
    // Mods are removed when they are deleted or when they are toggled in any collection.
    // Also gets cleared on mod rediscovery.
    public readonly HashSet<Mod> NewMods = new();

    private readonly List<Mod> _mods = new();

    public Mod this[int idx]
        => _mods[idx];

    public Mod this[Index idx]
        => _mods[idx];

    public int Count
        => _mods.Count;

    public IEnumerator<Mod> GetEnumerator()
        => _mods.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    private readonly Configuration       _config;
    private readonly CommunicatorService _communicator;
    public readonly  ModDataEditor       DataEditor;
    public readonly  ModOptionEditor     OptionEditor;

    public ModManager(StartTracker time, Configuration config, CommunicatorService communicator, ModDataEditor dataEditor,
        ModOptionEditor optionEditor)
    {
        using var timer = time.Measure(StartTimeType.Mods);
        _config             =  config;
        _communicator       =  communicator;
        DataEditor          =  dataEditor;
        OptionEditor        =  optionEditor;
        ModDirectoryChanged += OnModDirectoryChange;
        SetBaseDirectory(config.ModDirectory, true);
        _communicator.ModOptionChanged.Event += OnModOptionChange;
        ModPathChanged                       += OnModPathChange;
        DiscoverMods();
    }

    public void Dispose()
    {
        _communicator.ModOptionChanged.Event -= OnModOptionChange;
    }


    // Try to obtain a mod by its directory name (unique identifier, preferred),
    // or the first mod of the given name if no directory fits.
    public bool TryGetMod(string modDirectory, string modName, [NotNullWhen(true)] out Mod? mod)
    {
        mod = null;
        foreach (var m in _mods)
        {
            if (string.Equals(m.ModPath.Name, modDirectory, StringComparison.OrdinalIgnoreCase))
            {
                mod = m;
                return true;
            }

            if (m.Name == modName)
                mod ??= m;
        }

        return mod != null;
    }

    private static void OnModOptionChange(ModOptionChangeType type, Mod mod, int groupIdx, int _, int _2)
    {
        if (type == ModOptionChangeType.PrepareChange)
            return;

        bool ComputeChangedItems()
        {
            mod.ComputeChangedItems();
            return true;
        }

        // State can not change on adding groups, as they have no immediate options.
        var unused = type switch
        {
            ModOptionChangeType.GroupAdded       => ComputeChangedItems() & mod.SetCounts(),
            ModOptionChangeType.GroupDeleted     => ComputeChangedItems() & mod.SetCounts(),
            ModOptionChangeType.GroupMoved       => false,
            ModOptionChangeType.GroupTypeChanged => mod.HasOptions = mod.Groups.Any(o => o.IsOption),
            ModOptionChangeType.PriorityChanged  => false,
            ModOptionChangeType.OptionAdded      => ComputeChangedItems() & mod.SetCounts(),
            ModOptionChangeType.OptionDeleted    => ComputeChangedItems() & mod.SetCounts(),
            ModOptionChangeType.OptionMoved      => false,
            ModOptionChangeType.OptionFilesChanged => ComputeChangedItems()
              & (0 < (mod.TotalFileCount = mod.AllSubMods.Sum(s => s.Files.Count))),
            ModOptionChangeType.OptionSwapsChanged => ComputeChangedItems()
              & (0 < (mod.TotalSwapCount = mod.AllSubMods.Sum(s => s.FileSwaps.Count))),
            ModOptionChangeType.OptionMetaChanged => ComputeChangedItems()
              & (0 < (mod.TotalManipulations = mod.AllSubMods.Sum(s => s.Manipulations.Count))),
            ModOptionChangeType.DisplayChange => false,
            _                                 => false,
        };
    }
}
