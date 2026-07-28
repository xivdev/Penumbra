using Luna;
using Penumbra.Communication;
using Penumbra.Files;
using Penumbra.Mods.Groups;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.Mods.SubMods;
using Penumbra.Services;

namespace Penumbra.Mods.Settings;

public readonly record struct ModObjectIdentifier(Guid Identifier, string? Name = null)
{
    public bool IsEmpty
        => Name is null && Identifier == Guid.Empty;

    public ModObjectIdentifier(IModObject @object)
        : this(@object.Id, @object.Name)
    { }

    public ModObjectIdentifier(string name)
        : this(Guid.Empty, name)
    { }

    public IModObject? Find(Mod mod)
    {
        if (Identifier != Guid.Empty && mod.SubObjects.TryGetValue(Identifier, out var @object))
            return @object;

        if (Name is not { } name)
            return null;

        return mod.SubObjects.Values.FirstOrDefault(o => o.Name == name);
    }
}

public sealed class SettingPreset
{
    public Guid                                                    Identifier { get; init; } = Guid.NewGuid();
    public string                                                  Name       { get; set; }  = "Preset";
    public SetDictionary<ModObjectIdentifier, ModObjectIdentifier> Settings   { get; set; }  = [];
    public ModPriority                                             Priority   { get; set; }
    public bool?                                                   State      { get; set; }

    public static SetDictionary<ModObjectIdentifier, ModObjectIdentifier> FromSettings(Mod mod, ModSettings? settings)
    {
        var ret = new SetDictionary<ModObjectIdentifier, ModObjectIdentifier>(mod.Groups.Count);
        if (settings is null || settings.IsEmpty)
            foreach (var group in mod.Groups)
                ret.TryAddOwned(new ModObjectIdentifier(group), GetSet(group, group.DefaultSettings));
        else
            foreach (var group in mod.Groups)
                ret.TryAddOwned(new ModObjectIdentifier(group), GetSet(group, settings.Settings[group.Index]));

        return ret;

        static HashSet<ModObjectIdentifier> GetSet(IModGroup group, Setting setting)
        {
            if (group.Behaviour is not GroupDrawBehaviour.SingleSelection)
                return group.Options.Where(o => setting.HasFlag(o.Index)).Select(o => new ModObjectIdentifier(o)).ToHashSet();

            if (group.Options.Count > setting.AsIndex)
                return [new ModObjectIdentifier(group.Options[setting.AsIndex])];

            return [new ModObjectIdentifier(group.Options[group.DefaultSettings.AsIndex])];
        }
    }

    public IEnumerable<KeyValuePair<IModGroup, Setting>> Convert(Mod mod)
    {
        foreach (var (identifier, settings) in Settings.Grouped)
        {
            if (identifier.Find(mod) is not IModGroup group)
                continue;

            var setting = Setting.Zero;
            foreach (var option in settings)
            {
                if (option.Find(mod) is not IModOption o || o.Group != group)
                    continue;

                if (group.Behaviour is GroupDrawBehaviour.SingleSelection)
                    setting = Setting.Single(o.Index);
                else
                    setting.SetBit(o.Index, true);
            }

            yield return KeyValuePair.Create(group, setting);
        }
    }

    public bool Update(Mod mod, ModSettings? settings)
    {
        var ret = false;
        if (settings is null)
        {
            ret      = State is not null;
            State    = null;
            Priority = ModPriority.Default;
            Settings = [];
            return ret;
        }


        if (Priority != settings.Priority)
        {
            Priority = settings.Priority;
            ret      = true;
        }

        if (settings.Enabled != State)
        {
            State = settings.Enabled;
            ret   = true;
        }

        var newSettings = FromSettings(mod, settings);
        if (!Settings.Equals(newSettings))
        {
            Settings = newSettings;
            ret      = true;
        }

        return ret;
    }
}

public sealed class SettingPresetManager : ISavable, IDisposable, IService
{
    private readonly CommunicatorService                   _communicator;
    public readonly  ListDictionary<string, SettingPreset> Presets = [];

    public SettingPresetManager(CommunicatorService communicator)
    {
        _communicator = communicator;
        _communicator.ModOptionChanged.Subscribe(OnModOptionChange, 0); // TODO Priority
        _communicator.ModPathChanged.Subscribe(OnModPathChange, 0);     // TODO Priority
    }

    private void OnModPathChange(in ModPathChanged.Arguments arguments)
    {
        if (arguments.Type != ModPathChangeType.Moved)
            return;

        if (!Presets.TryGetValue(arguments.OldDirectory!.Name, out var list))
            return;

        if (Presets.TryGetValue(arguments.NewDirectory!.Name, out var newList))
            foreach (var value in list.Where(v => newList.All(v2 => v2.Identifier != v.Identifier)))
                Presets.TryAdd(arguments.NewDirectory!.Name, value);
        else
            foreach (var value in list)
                Presets.TryAdd(arguments.NewDirectory!.Name, value);
    }

    private void OnModOptionChange(in ModOptionChanged.Arguments arguments)
    {
        if (!Presets.TryGetValue(arguments.Mod.Identifier, out var presets))
            return;

        switch (arguments.Type)
        {
            case ModOptionChangeType.GroupIdentifierChanged:
            {
                var oldIdentifier = new ModObjectIdentifier(arguments.Id, arguments.Group!.Name);
                var newIdentifier = new ModObjectIdentifier(arguments.Group!);
                foreach (var preset in presets)
                {
                    preset.Settings =
                        preset.Settings.ToSetDictionary(kvp => kvp.Key == oldIdentifier ? newIdentifier : kvp.Key, kvp => kvp.Value);
                }

                break;
            }
            case ModOptionChangeType.OptionIdentifierChanged:
            {
                var oldIdentifier = new ModObjectIdentifier(arguments.Id, arguments.Option!.Name);
                var newIdentifier = new ModObjectIdentifier(arguments.Option!);
                foreach (var preset in presets)
                {
                    preset.Settings =
                        preset.Settings.ToSetDictionary(kvp => kvp.Key, kvp => kvp.Value == oldIdentifier ? newIdentifier : kvp.Value);
                }

                break;
            }
            case ModOptionChangeType.GroupRenamed:
            {
                var newIdentifier = new ModObjectIdentifier(arguments.Group!);
                foreach (var preset in presets)
                {
                    preset.Settings =
                        preset.Settings.ToSetDictionary(kvp => kvp.Key.Identifier == newIdentifier.Identifier ? newIdentifier : kvp.Key,
                            kvp => kvp.Value);
                }

                break;
            }
            case ModOptionChangeType.OptionRenamed:
            {
                var newIdentifier = new ModObjectIdentifier(arguments.Option!);
                foreach (var preset in presets)
                {
                    preset.Settings = preset.Settings.ToSetDictionary(kvp => kvp.Key,
                        kvp => kvp.Value.Identifier == newIdentifier.Identifier ? newIdentifier : kvp.Value);
                }

                break;
            }
            case ModOptionChangeType.OptionDeleted:
            {
                var deletedIdentifier = new ModObjectIdentifier(arguments.Option!);
                foreach (var preset in presets)
                {
                    foreach (var key in preset.Settings.Keys)
                        preset.Settings.RemoveValue(key, deletedIdentifier);
                }

                break;
            }
        }
    }

    public void Dispose()
    { }

    public string ToFilePath(FilenameService fileNames)
        => throw new NotImplementedException();

    public void Save(Stream stream)
    {
        throw new NotImplementedException();
    }
}
