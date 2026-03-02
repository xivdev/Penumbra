using ImSharp;
using Luna;
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
public class ModSelection : EventBase<ModSelection.Arguments, ModSelection.Priority>
{
    private readonly ActiveCollections   _collections;
    private readonly CommunicatorService _communicator;
    private readonly ModFileSystem       _modFileSystem;

    public ModSelection(Logger log, CommunicatorService communicator, ModManager mods, ActiveCollections collections, EphemeralConfig config,
        ModFileSystem modFileSystem)
        : base(nameof(ModSelection), log)
    {
        _communicator  = communicator;
        _collections   = collections;
        _modFileSystem = modFileSystem;
        _communicator.CollectionChange.Subscribe(OnCollectionChange, CollectionChange.Priority.ModSelection);
        _communicator.CollectionInheritanceChanged.Subscribe(OnInheritanceChange, CollectionInheritanceChanged.Priority.ModSelection);
        _communicator.ModSettingChanged.Subscribe(OnSettingChange, ModSettingChanged.Priority.ModSelection);
        _modFileSystem.Selection.Changed += OnSelectionChanged;
        SelectModInternal(_modFileSystem.Selection.Selection?.GetValue<Mod>());
    }

    private void OnSelectionChanged()
        => SelectModInternal(_modFileSystem.Selection.Selection?.GetValue<Mod>());

    public StringU8              ModName           { get; private set; } = StringU8.Empty;
    public ModSettings           Settings          { get; private set; } = ModSettings.Empty;
    public ModCollection         Collection        { get; private set; } = ModCollection.Empty;
    public Mod?                  Mod               { get; private set; }
    public ModSettings?          OwnSettings       { get; private set; }
    public TemporaryModSettings? TemporarySettings { get; private set; }

    public void SelectMod(Mod? mod)
    {
        if (mod is null)
            _modFileSystem.Selection.UnselectAll();
        else if (mod.Node is { } node)
            _modFileSystem.Selection.Select(node, true);
    }

    private void SelectModInternal(Mod? mod)
    {
        if (mod == Mod)
            return;

        var oldMod = Mod;
        Mod = mod;
        OnCollectionChange(new CollectionChange.Arguments(CollectionType.Current, null, _collections.Current, string.Empty));
        Invoke(new Arguments(oldMod, Mod));
    }

    protected override void Dispose(bool _)
    {
        _communicator.CollectionChange.Unsubscribe(OnCollectionChange);
        _communicator.CollectionInheritanceChanged.Unsubscribe(OnInheritanceChange);
        _communicator.ModSettingChanged.Unsubscribe(OnSettingChange);
        _modFileSystem.Selection.Changed -= OnSelectionChanged;
    }

    private void OnCollectionChange(in CollectionChange.Arguments arguments)
    {
        if (arguments.Type is CollectionType.Current && arguments.OldCollection != arguments.NewCollection)
            UpdateSettings();
    }

    private void OnSettingChange(in ModSettingChanged.Arguments arguments)
    {
        if (arguments.Collection == _collections.Current && arguments.Mod == Mod)
            UpdateSettings();
    }

    private void OnInheritanceChange(in CollectionInheritanceChanged.Arguments arguments)
    {
        if (arguments.Collection == _collections.Current)
            UpdateSettings();
    }

    private void UpdateSettings()
    {
        if (Mod is null)
        {
            ModName     = StringU8.Empty;
            Settings    = ModSettings.Empty;
            Collection  = ModCollection.Empty;
            OwnSettings = null;
        }
        else
        {
            ModName                    = new StringU8(Mod.Name);
            (var settings, Collection) = _collections.Current.GetActualSettings(Mod.Index);
            OwnSettings                = _collections.Current.GetOwnSettings(Mod.Index);
            TemporarySettings          = _collections.Current.GetTempSettings(Mod.Index);
            Settings                   = settings ?? ModSettings.Empty;
        }
    }

    public enum Priority
    {
        /// <seealso cref="UI.ModsTab.ModPanel.OnSelectionChange"/>
        ModPanel = 0,
    }

    public readonly record struct Arguments(Mod? OldSelection, Mod? NewSelection);
}
