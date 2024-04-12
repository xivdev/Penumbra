using OtterGui;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.Manager;
using Penumbra.String.Classes;

namespace Penumbra.Mods.Subclasses;

/// <summary> Contains the settings for a given mod. </summary>
public class ModSettings
{
    public static readonly ModSettings Empty = new();
    public                 SettingList Settings { get; private init; } = [];
    public                 ModPriority Priority { get; set; }
    public                 bool        Enabled  { get; set; }

    // Create an independent copy of the current settings.
    public ModSettings DeepCopy()
        => new()
        {
            Enabled  = Enabled,
            Priority = Priority,
            Settings = Settings.Clone(),
        };

    // Create default settings for a given mod.
    public static ModSettings DefaultSettings(Mod mod)
        => new()
        {
            Enabled  = false,
            Priority = ModPriority.Default,
            Settings = SettingList.Default(mod),
        };

    // Return everything required to resolve things for a single mod with given settings (which can be null, in which case the default is used.
    public static (Dictionary<Utf8GamePath, FullPath>, HashSet<MetaManipulation>) GetResolveData(Mod mod, ModSettings? settings)
    {
        if (settings == null)
            settings = DefaultSettings(mod);
        else
            settings.Settings.FixSize(mod);

        var dict = new Dictionary<Utf8GamePath, FullPath>();
        var set  = new HashSet<MetaManipulation>();

        foreach (var (group, index) in mod.Groups.WithIndex().OrderByDescending(g => g.Value.Priority))
        {
            if (group.Type is GroupType.Single)
            {
                if (group.Count > 0)
                    AddOption(group[settings.Settings[index].AsIndex]);
            }
            else
            {
                foreach (var (option, optionIdx) in group.WithIndex().OrderByDescending(o => group.OptionPriority(o.Index)))
                {
                    if (settings.Settings[index].HasFlag(optionIdx))
                        AddOption(option);
                }
            }
        }

        AddOption(mod.Default);
        return (dict, set);

        void AddOption(ISubMod option)
        {
            foreach (var (path, file) in option.Files.Concat(option.FileSwaps))
                dict.TryAdd(path, file);

            foreach (var manip in option.Manipulations)
                set.Add(manip);
        }
    }

    // Automatically react to changes in a mods available options.
    public bool HandleChanges(ModOptionChangeType type, Mod mod, int groupIdx, int optionIdx, int movedToIdx)
    {
        switch (type)
        {
            case ModOptionChangeType.GroupRenamed: return true;
            case ModOptionChangeType.GroupAdded:
                // Add new empty setting for new mod.
                Settings.Insert(groupIdx, mod.Groups[groupIdx].DefaultSettings);
                return true;
            case ModOptionChangeType.GroupDeleted:
                // Remove setting for deleted mod.
                Settings.RemoveAt(groupIdx);
                return true;
            case ModOptionChangeType.GroupTypeChanged:
            {
                // Fix settings for a changed group type.
                // Single -> Multi: set single as enabled, rest as disabled
                // Multi -> Single: set the first enabled option or 0.
                var group  = mod.Groups[groupIdx];
                var config = Settings[groupIdx];
                Settings[groupIdx] = group.Type switch
                {
                    GroupType.Single => config.TurnMulti(group.Count),
                    GroupType.Multi  => Setting.Multi((int)config.Value),
                    _                => config,
                };
                return config != Settings[groupIdx];
            }
            case ModOptionChangeType.OptionDeleted:
            {
                // Single -> select the previous option if any.
                // Multi -> excise the corresponding bit.
                var group  = mod.Groups[groupIdx];
                var config = Settings[groupIdx];
                Settings[groupIdx] = group.Type switch
                {
                    GroupType.Single => config.AsIndex >= optionIdx
                        ? config.AsIndex > 1 ? Setting.Single(config.AsIndex - 1) : Setting.Zero
                        : config,
                    GroupType.Multi => config.RemoveBit(optionIdx),
                    _               => config,
                };
                return config != Settings[groupIdx];
            }
            case ModOptionChangeType.GroupMoved:
                // Move the group the same way.
                return Settings.Move(groupIdx, movedToIdx);
            case ModOptionChangeType.OptionMoved:
            {
                // Single -> select the moved option if it was currently selected
                // Multi -> move the corresponding bit
                var group  = mod.Groups[groupIdx];
                var config = Settings[groupIdx];
                Settings[groupIdx] = group.Type switch
                {
                    GroupType.Single => config.AsIndex == optionIdx ? Setting.Single(movedToIdx) : config,
                    GroupType.Multi  => config.MoveBit(optionIdx, movedToIdx),
                    _                => config,
                };
                return config != Settings[groupIdx];
            }
            default: return false;
        }
    }

    /// <summary> Set a setting. Ensures that there are enough settings and fixes the setting beforehand. </summary>
    public void SetValue(Mod mod, int groupIdx, Setting newValue)
    {
        Settings.FixSize(mod);
        var group = mod.Groups[groupIdx];
        Settings[groupIdx] = group.FixSetting(newValue);
    }

    // A simple struct conversion to easily save settings by name instead of value.
    public struct SavedSettings
    {
        public Dictionary<string, Setting> Settings;
        public ModPriority                 Priority;
        public bool                        Enabled;

        public SavedSettings DeepCopy()
            => this with { Settings = Settings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) };

        public SavedSettings(ModSettings settings, Mod mod)
        {
            Priority = settings.Priority;
            Enabled  = settings.Enabled;
            Settings = new Dictionary<string, Setting>(mod.Groups.Count);
            settings.Settings.FixSize(mod);

            foreach (var (group, setting) in mod.Groups.Zip(settings.Settings))
                Settings.Add(group.Name, setting);
        }

        // Convert and fix.
        public readonly bool ToSettings(Mod mod, out ModSettings settings)
        {
            var list    = new SettingList(mod.Groups.Count);
            var changes = Settings.Count != mod.Groups.Count;
            foreach (var group in mod.Groups)
            {
                if (Settings.TryGetValue(group.Name, out var config))
                {
                    var actualConfig = group.FixSetting(config);
                    list.Add(actualConfig);
                    if (actualConfig != config)
                        changes = true;
                }
                else
                {
                    list.Add(group.DefaultSettings);
                    changes = true;
                }
            }

            settings = new ModSettings
            {
                Enabled  = Enabled,
                Priority = Priority,
                Settings = list,
            };

            return changes;
        }
    }

    // Return the settings for a given mod in a shareable format, using the names of groups and options instead of indices.
    // Does not repair settings but ignores settings not fitting to the given mod.
    public (bool Enabled, ModPriority Priority, Dictionary<string, List<string>> Settings) ConvertToShareable(Mod mod)
    {
        var dict = new Dictionary<string, List<string>>(Settings.Count);
        foreach (var (setting, idx) in Settings.WithIndex())
        {
            if (idx >= mod.Groups.Count)
                break;

            var group = mod.Groups[idx];
            if (group.Type == GroupType.Single && setting.Value < (ulong)group.Count)
            {
                dict.Add(group.Name, [group[(int)setting.Value].Name]);
            }
            else
            {
                var list = group.Where((_, optionIdx) => (setting.Value & (1ul << optionIdx)) != 0).Select(o => o.Name).ToList();
                dict.Add(group.Name, list);
            }
        }

        return (Enabled, Priority, dict);
    }
}
