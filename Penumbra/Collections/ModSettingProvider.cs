using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.Services;

namespace Penumbra.Collections;

public readonly struct ModSettingProvider
{
    private ModSettingProvider(IEnumerable<FullModSettings> settings, Dictionary<string, ModSettings.SavedSettings> unusedSettings)
    {
        _settings = settings.Select(s => s.DeepCopy()).ToList();
        _unused   = unusedSettings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.DeepCopy());
    }

    public ModSettingProvider()
    { }

    public static ModSettingProvider Empty(int count)
        => new(Enumerable.Repeat(FullModSettings.Empty, count), []);

    public ModSettingProvider(Dictionary<string, ModSettings.SavedSettings> allSettings)
        => _unused = allSettings;

    private readonly List<FullModSettings> _settings = [];

    /// <summary> Settings for deleted mods will be kept via the mods identifier (directory name). </summary>
    private readonly Dictionary<string, ModSettings.SavedSettings> _unused = [];

    public int Count
        => _settings.Count;

    public bool RemoveUnused(string key)
        => _unused.Remove(key);

    internal void Set(Index index, ModSettings? settings)
        => _settings[index] = _settings[index] with { Settings = settings };

    internal void SetTemporary(Index index, TemporaryModSettings? settings)
        => _settings[index] = _settings[index] with { TempSettings = settings };

    internal void SetAll(Index index, FullModSettings settings)
        => _settings[index] = settings;

    public IReadOnlyList<FullModSettings> Settings
        => _settings;

    public IReadOnlyDictionary<string, ModSettings.SavedSettings> Unused
        => _unused;

    public ModSettingProvider Clone()
        => new(_settings, _unused);

    /// <summary> Add settings for a new appended mod, by checking if the mod had settings from a previous deletion. </summary>
    internal bool AddMod(Mod mod)
    {
        if (_unused.Remove(mod.ModPath.Name, out var save))
        {
            var ret = save.ToSettings(mod, out var settings);
            _settings.Add(new FullModSettings(settings));
            return ret;
        }

        _settings.Add(FullModSettings.Empty);
        return false;
    }

    /// <summary> Move settings from the current mod list to the unused mod settings. </summary>
    internal void RemoveMod(Mod mod)
    {
        var settings = _settings[mod.Index];
        if (settings.Settings != null)
            _unused[mod.ModPath.Name] = new ModSettings.SavedSettings(settings.Settings, mod);

        _settings.RemoveAt(mod.Index);
    }

    /// <summary> Move all settings to unused settings for rediscovery. </summary>
    internal void PrepareModDiscovery(ModStorage mods)
    {
        foreach (var (mod, setting) in mods.Zip(_settings).Where(s => s.Second.Settings != null))
            _unused[mod.ModPath.Name] = new ModSettings.SavedSettings(setting.Settings!, mod);

        _settings.Clear();
    }

    /// <summary>
    /// Apply all mod settings from unused settings to the current set of mods.
    /// Also fixes invalid settings.
    /// </summary>
    internal void ApplyModSettings(ModCollection parent, SaveService saver, ModStorage mods)
    {
        _settings.Capacity = Math.Max(_settings.Capacity, mods.Count);
        var settings = this;
        if (mods.Aggregate(false, (current, mod) => current | settings.AddMod(mod)))
            saver.ImmediateSave(new ModCollectionSave(mods, parent));
    }
}
