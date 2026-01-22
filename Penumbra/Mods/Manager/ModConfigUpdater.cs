using Luna;
using Penumbra.Api.Enums;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Services;

namespace Penumbra.Mods.Manager;

public class ModConfigUpdater : IDisposable, IRequiredService
{
    private readonly CommunicatorService _communicator;
    private readonly SaveService         _saveService;
    private readonly ModStorage          _mods;
    private readonly CollectionStorage   _collections;

    public ModConfigUpdater(CommunicatorService communicator, SaveService saveService, ModStorage mods, CollectionStorage collections)
    {
        _communicator = communicator;
        _saveService  = saveService;
        _mods         = mods;
        _collections  = collections;

        _communicator.ModSettingChanged.Subscribe(OnModSettingChanged, ModSettingChanged.Priority.ModConfigUpdater);
    }

    public event Action<string, string, Dictionary<Assembly, (bool MarkUsed, string Note)>>? ModUsageQueried;

    public IEnumerable<(Mod, (string Plugin, string Notes)[])> ListUnusedMods(TimeSpan age)
    {
        var cutoff         = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (int)age.TotalMilliseconds;
        var noteDictionary = new Dictionary<Assembly, (bool InUse, string Notes)>();
        foreach (var mod in _mods)
        {
            // Skip actively ignored mods.
            if (mod.IgnoreLastConfig)
                continue;

            // Skip mods that had settings changed since the given maximum age.
            if (mod.LastConfigEdit >= cutoff)
                continue;

            // Skip mods that are currently permanently enabled or have any temporary settings.
            if (_collections.Any(c => c.GetOwnSettings(mod.Index)?.Enabled is true || c.GetTempSettings(mod.Index) is not null))
                continue;

            // Check whether other plugins mark this mod as in use.
            noteDictionary.Clear();
            ModUsageQueried?.Invoke(mod.Name, mod.Identifier, noteDictionary);
            if (noteDictionary.Values.Any(n => n.InUse))
                continue;

            // Collect other plugin's notes for this mod.
            var notes = noteDictionary.Where(t => !string.IsNullOrWhiteSpace(t.Value.Notes))
                .Select(t => (t.Key.GetName().Name ?? "Unknown Plugin", t.Value.Notes)).ToArray();

            yield return (mod, notes);
        }
    }

    private void OnModSettingChanged(in ModSettingChanged.Arguments arguments)
    {
        if (arguments.Inherited)
            return;

        switch (arguments.Type)
        {
            case ModSettingChange.Inheritance:
            case ModSettingChange.MultiInheritance:
            case ModSettingChange.MultiEnableState:
            case ModSettingChange.TemporaryMod:
            case ModSettingChange.Edited:
                return;
        }

        if (arguments.Mod is { } mod)
        {
            mod.LastConfigEdit = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _communicator.ModDataChanged.Invoke(new ModDataChanged.Arguments(ModDataChangeType.LastConfigEdit, mod, null));
            _saveService.Save(SaveType.Delay, new ModLocalData(mod));
        }
    }

    public void Dispose()
    {
        _communicator.ModSettingChanged.Unsubscribe(OnModSettingChanged);
    }
}
