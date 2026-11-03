using Luna;
using Luna.Generators;
using Penumbra.Api.Enums;
using Penumbra.Api.Preset;
using Penumbra.Api.Wrappers;
using Penumbra.Mods;

namespace Penumbra.Api;

public sealed partial class PresetAdapter(PresetManagerAdapter parent, Mod? mod, SettingPreset preset)
    : IpcObjectManager.BasicAdapter(parent.Parent, parent.Owner, nameof(PresetAdapter)), IpcObjectManager.IBasicAdapter
{
    private SettingPreset _preset = preset;
    private Mod?          _mod    = mod;

    public override Version Version
        => PresetManagerAdapter.LocalVersion;

    public override bool IsDisposed
        => Parent is null;

    public event Action? Disposed;

    protected override void InvokeDisposed()
        => Disposed?.Invoke();

    [AdapterMethod(PresetWrapper.Method.AssociatedMod)]
    public ModIdentifier AssociatedMod
        => _mod is { } m ? new ModIdentifier(m.Identifier, m.Name) : ModIdentifier.Empty;

    [AdapterMethod(PresetWrapper.Method.Identifier)]
    public Guid Identifier
        => _preset.Identifier;

    [AdapterMethod(PresetWrapper.Method.Name)]
    public string Name
    {
        get => _preset.Name;
        set => parent.Parent.Presets.ChangeName(_mod?.Identifier ?? string.Empty, _preset, value);
    }

    [AdapterMethod(PresetWrapper.Method.LastEdit)]
    public DateTimeOffset LastEdit
        => _preset.LastEdit;

    [AdapterMethod(PresetWrapper.Method.LastApplication)]
    public DateTimeOffset LastApplication
        => _preset.LastApplication;

    [AdapterMethod(PresetWrapper.Method.Data)]
    public SettingsDictionary Data
        => _preset.Data.Settings;

    [AdapterMethod(PresetWrapper.Method.Priority)]
    public int? Priority
    {
        get => _preset.Data.Priority;
        set => parent.Parent.Presets.ChangePriority(_mod?.Identifier ?? string.Empty, _preset, value);
    }

    [AdapterMethod(PresetWrapper.Method.State)]
    public byte State
    {
        get => _preset.Data._state;
        set => parent.Parent.Presets.ChangeState(_mod?.Identifier ?? string.Empty, _preset, (ModState)value);
    }

    [AdapterMethod(PresetWrapper.Method.Delete)]
    public void Delete()
    {
        if (_mod is null)
            parent.Parent.Presets.DeleteGeneric(_preset);
        else
            parent.Parent.Presets.DeletePreset(_mod, _preset);
        Dispose();
    }

    [AdapterMethod(PresetWrapper.Method.UpdateFull)]
    public void Update(SettingPresetData newData)
        => parent.Parent.Presets.Update(_mod?.Identifier ?? string.Empty, _preset, newData);

    [AdapterMethod(PresetWrapper.Method.UpdateOptions)]
    public void Update(SettingsDictionary newData)
        => parent.Parent.Presets.Update(_mod?.Identifier ?? string.Empty, _preset,
            (newData, _preset.Data._priority, _preset.Data.Version, _preset.Data._hasPriority, _preset.Data._state));

    [AdapterMethod(PresetWrapper.Method.Apply)]
    public int Apply(Guid collection, ModIdentifier mod, int mode, int key, string source)
        => (int)parent.Parent.ApplyPreset(collection, mod, _preset.Data, (PresetApplyMode)mode, key, source);

    [AdapterMethod(PresetWrapper.Method.ApplyToPlayer)]
    public int ApplyToPlayer(int objectIndex, ModIdentifier mod, int mode, int key, string source)
        => (int)parent.Parent.ApplyPresetPlayer(objectIndex, mod, _preset.Data, (PresetApplyMode)mode, key, source);


    protected override void DisposeInternal()
    {
        _mod    = null!;
        _preset = null!;
    }
}
