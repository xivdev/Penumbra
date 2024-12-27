using OtterGui;
using OtterGui.Filesystem;
using Penumbra.Api.Enums;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.SubMods;

namespace Penumbra.Mods.Settings;

/// <summary> Contains the settings for a given mod. </summary>
public class ModSettings
{
    public static readonly ModSettings Empty = new();

    public                 SettingList Settings { get; internal init; } = [];
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
    public static AppliedModData GetResolveData(Mod mod, ModSettings? settings)
    {
        if (settings == null)
            settings = DefaultSettings(mod);
        else
            settings.Settings.FixSize(mod);

        return mod.GetData(settings);
    }

    // Automatically react to changes in a mods available options.
    public bool HandleChanges(ModOptionChangeType type, Mod mod, IModGroup? group, IModOption? option, int fromIdx)
    {
        switch (type)
        {
            case ModOptionChangeType.GroupRenamed: return true;
            case ModOptionChangeType.GroupAdded:
                // Add new empty setting for new mod.
                Settings.Insert(group!.GetIndex(), group.DefaultSettings);
                return true;
            case ModOptionChangeType.GroupDeleted:
                // Remove setting for deleted mod.
                Settings.RemoveAt(fromIdx);
                return true;
            case ModOptionChangeType.GroupTypeChanged:
            {
                // Fix settings for a changed group type.
                // Single -> Multi: set single as enabled, rest as disabled
                // Multi -> Single: set the first enabled option or 0.
                var idx    = group!.GetIndex();
                var config = Settings[idx];
                Settings[idx] = group.Type switch
                {
                    GroupType.Single => config.TurnMulti(group.Options.Count),
                    GroupType.Multi  => Setting.Multi((int)config.Value),
                    _                => config,
                };
                return config != Settings[idx];
            }
            case ModOptionChangeType.OptionDeleted:
            {
                // Single -> select the previous option if any.
                // Multi -> excise the corresponding bit.
                var groupIdx = group!.GetIndex();
                var config   = Settings[groupIdx];
                Settings[groupIdx] = group!.Type switch
                {
                    GroupType.Single => config.RemoveSingle(fromIdx),
                    GroupType.Multi  => config.RemoveBit(fromIdx),
                    GroupType.Imc    => config.RemoveBit(fromIdx),
                    _                => config,
                };
                return config != Settings[groupIdx];
            }
            case ModOptionChangeType.GroupMoved:
                // Move the group the same way.
                return Settings.Move(fromIdx, group!.GetIndex());
            case ModOptionChangeType.OptionMoved:
            {
                // Single -> select the moved option if it was currently selected
                // Multi -> move the corresponding bit
                var groupIdx = group!.GetIndex();
                var toIdx    = option!.GetIndex();
                var config   = Settings[groupIdx];
                Settings[groupIdx] = group!.Type switch
                {
                    GroupType.Single => config.MoveSingle(fromIdx, toIdx),
                    GroupType.Multi  => config.MoveBit(fromIdx, toIdx),
                    GroupType.Imc    => config.MoveBit(fromIdx, toIdx),
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

            switch (mod.Groups[idx])
            {
                case { Behaviour: GroupDrawBehaviour.SingleSelection } single when setting.Value < (ulong)single.Options.Count:
                    dict.Add(single.Name, [single.Options[setting.AsIndex].Name]);
                    break;
                case { Behaviour: GroupDrawBehaviour.MultiSelection } multi:
                    var list = multi.Options.WithIndex().Where(p => setting.HasFlag(p.Index)).Select(p => p.Value.Name).ToList();
                    dict.Add(multi.Name, list);
                    break;
            }
        }

        return (Enabled, Priority, dict);
    }
}
