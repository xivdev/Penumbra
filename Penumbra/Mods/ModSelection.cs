using OtterGui.Classes;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Communication;
using Penumbra.Mods.Manager;
using Penumbra.Mods.Settings;
using Penumbra.Services;

namespace Penumbra.Mods;

/// <summary>
/// Triggered whenever the selected mod changes
/// <list type="number">
///     <item>Parameter is the old selected mod. </item>
///     <item>Parameter is the new selected mod </item>
/// </list>
/// </summary>
public class ModSelection : EventWrapper<Mod?, Mod?, ModSelection.Priority>
{
    private readonly ActiveCollections   _collections;
    private readonly EphemeralConfig     _config;
    private readonly CommunicatorService _communicator;

    public ModSelection(CommunicatorService communicator, ModManager mods, ActiveCollections collections, EphemeralConfig config)
        : base(nameof(ModSelection))
    {
        _communicator = communicator;
        _collections  = collections;
        _config       = config;
        if (_config.LastModPath.Length > 0)
            SelectMod(mods.FirstOrDefault(m => string.Equals(m.Identifier, config.LastModPath, StringComparison.OrdinalIgnoreCase)));

        _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.ModSelection);
        _communicator.CollectionInheritanceChanged.Subscribe(OnInheritanceChange, CollectionInheritanceChanged.Priority.ModSelection);
        _communicator.ModSettingChanged.Subscribe(OnSettingChange, ModSettingChanged.Priority.ModSelection);
    }

    public ModSettings   Settings   { get; private set; } = ModSettings.Empty;
    public ModCollection Collection { get; private set; } = ModCollection.Empty;
    public Mod?          Mod        { get; private set; }


    public void SelectMod(Mod? mod)
    {
        if (mod == Mod)
            return;

        var oldMod = Mod;
        Mod = mod;
        OnCollectionChange(CollectionType.Current, null, _collections.Current, string.Empty);
        Invoke(oldMod, Mod);
        _config.LastModPath = mod?.ModPath.Name ?? string.Empty;
        _config.Save();
    }

    protected override void Dispose(bool _)
    {
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
        _communicator.CollectionInheritanceChanged.Unsubscribe(OnInheritanceChange);
        _communicator.ModSettingChanged.Unsubscribe(OnSettingChange);
    }

    private void OnCollectionChange(CollectionType type, ModCollection? oldCollection, ModCollection? newCollection, string _2)
    {
        if (type is CollectionType.Current && oldCollection != newCollection)
            UpdateSettings();
    }

    private void OnSettingChange(ModCollection collection, ModSettingChange _1, Mod? mod, Setting _2, int _3, bool _4)
    {
        if (collection == _collections.Current && mod == Mod)
            UpdateSettings();
    }

    private void OnInheritanceChange(ModCollection collection, bool arg2)
    {
        if (collection == _collections.Current)
            UpdateSettings();
    }

    private void UpdateSettings()
    {
        if (Mod == null)
        {
            Settings   = ModSettings.Empty;
            Collection = ModCollection.Empty;
        }
        else
        {
            (var settings, Collection) = _collections.Current[Mod.Index];
            Settings                   = settings ?? ModSettings.Empty;
        }
    }

    public enum Priority
    {
        /// <seealso cref="UI.ModsTab.ModPanel.OnSelectionChange"/>
        ModPanel = 0,

        /// <seealso cref="Editor.ModMerger.OnSelectionChange"/>
        ModMerger = 0,
    }
}
