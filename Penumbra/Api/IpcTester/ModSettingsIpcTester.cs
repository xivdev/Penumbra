using Dalamud.Plugin;
using ImSharp;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;

namespace Penumbra.Api.IpcTester;

public class ModSettingsIpcTester : Luna.IUiService, IDisposable
{
    private readonly IDalamudPluginInterface                               _pi;
    public readonly  EventSubscriber<ModSettingChange, Guid, string, bool> SettingChanged;

    private PenumbraApiEc    _lastSettingsError = PenumbraApiEc.Success;
    private ModSettingChange _lastSettingChangeType;
    private Guid             _lastSettingChangeCollection = Guid.Empty;
    private string           _lastSettingChangeMod        = string.Empty;
    private bool             _lastSettingChangeInherited;
    private DateTimeOffset   _lastSettingChange;

    private string                                                                         _settingsModDirectory = string.Empty;
    private string                                                                         _settingsModName      = string.Empty;
    private Guid?                                                                          _settingsCollection;
    private string                                                                         _settingsCollectionName = string.Empty;
    private bool                                                                           _settingsIgnoreInheritance;
    private bool                                                                           _settingsIgnoreTemporary;
    private int                                                                            _settingsKey;
    private bool                                                                           _settingsInherit;
    private bool                                                                           _settingsTemporary;
    private bool                                                                           _settingsEnabled;
    private int                                                                            _settingsPriority;
    private IReadOnlyDictionary<string, (string[], GroupType)>?                            _availableSettings;
    private Dictionary<string, List<string>>?                                              _currentSettings;
    private Dictionary<string, (bool, int, Dictionary<string, List<string>>, bool, bool)>? _allSettings;

    public ModSettingsIpcTester(IDalamudPluginInterface pi)
    {
        _pi            = pi;
        SettingChanged = ModSettingChanged.Subscriber(pi, UpdateLastModSetting);
        SettingChanged.Disable();
    }

    public void Dispose()
        => SettingChanged.Dispose();

    public void Draw()
    {
        using var _ = Im.Tree.Node("Mod Settings"u8);
        if (!_)
            return;

        Im.Input.Text("##settingsDir"u8,  ref _settingsModDirectory, "Mod Directory Name..."u8);
        Im.Input.Text("##settingsName"u8, ref _settingsModName,      "Mod Name..."u8);
        ImEx.GuidInput("Collection ID##settingsCollection"u8, ref _settingsCollection);
        Im.Checkbox("Ignore Inheritance"u8, ref _settingsIgnoreInheritance);
        Im.Checkbox("Ignore Temporary"u8,   ref _settingsIgnoreTemporary);
        Im.Input.Scalar("Key"u8, ref _settingsKey);
        var collection = _settingsCollection.GetValueOrDefault(Guid.Empty);

        using var table = Im.Table.Begin(StringU8.Empty, 3, TableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro("Last Error"u8, $"{_lastSettingsError}").Dispose();

        using (IpcTester.DrawIntro(ModSettingChanged.Label, "Last Mod Setting Changed"u8))
        {
            table.DrawColumn(_lastSettingChangeMod.Length > 0
                ? $"{_lastSettingChangeType} of {_lastSettingChangeMod} in {_lastSettingChangeCollection}{(_lastSettingChangeInherited ? " (Inherited)" : string.Empty)} at {_lastSettingChange}"
                : "None"u8);
        }

        using (IpcTester.DrawIntro(GetAvailableModSettings.Label, "Get Available Settings"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Get##Available"u8))
            {
                _availableSettings = new GetAvailableModSettings(_pi).Invoke(_settingsModDirectory, _settingsModName);
                _lastSettingsError = _availableSettings == null ? PenumbraApiEc.ModMissing : PenumbraApiEc.Success;
            }
        }

        using (IpcTester.DrawIntro(GetCurrentModSettings.Label, "Get Current Settings"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Get##Current"u8))
            {
                var ret = new GetCurrentModSettings(_pi)
                    .Invoke(collection, _settingsModDirectory, _settingsModName, _settingsIgnoreInheritance);
                _lastSettingsError = ret.Item1;
                if (ret.Item1 is PenumbraApiEc.Success)
                {
                    _settingsEnabled   = ret.Item2?.Item1 ?? false;
                    _settingsInherit   = ret.Item2?.Item4 ?? true;
                    _settingsTemporary = false;
                    _settingsPriority  = ret.Item2?.Item2 ?? 0;
                    _currentSettings   = ret.Item2?.Item3;
                }
                else
                {
                    _currentSettings = null;
                }
            }
        }

        using (IpcTester.DrawIntro(GetCurrentModSettingsWithTemp.Label, "Get Current Settings With Temp"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Get##CurrentTemp"u8))
            {
                var ret = new GetCurrentModSettingsWithTemp(_pi)
                    .Invoke(collection, _settingsModDirectory, _settingsModName, _settingsIgnoreInheritance, _settingsIgnoreTemporary,
                        _settingsKey);
                _lastSettingsError = ret.Item1;
                if (ret.Item1 is PenumbraApiEc.Success)
                {
                    _settingsEnabled   = ret.Item2?.Item1 ?? false;
                    _settingsInherit   = ret.Item2?.Item4 ?? true;
                    _settingsTemporary = ret.Item2?.Item5 ?? false;
                    _settingsPriority  = ret.Item2?.Item2 ?? 0;
                    _currentSettings   = ret.Item2?.Item3;
                }
                else
                {
                    _currentSettings = null;
                }
            }
        }

        using (IpcTester.DrawIntro(GetAllModSettings.Label, "Get All Mod Settings"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Get##All"u8))
            {
                var ret = new GetAllModSettings(_pi).Invoke(collection, _settingsIgnoreInheritance, _settingsIgnoreTemporary, _settingsKey);
                _lastSettingsError = ret.Item1;
                _allSettings       = ret.Item2;
            }

            if (_allSettings is not null)
            {
                Im.Line.Same();
                Im.Text($"{_allSettings.Count} Mods");
            }
        }

        using (IpcTester.DrawIntro(TryInheritMod.Label, "Inherit Mod"u8))
        {
            table.NextColumn();
            Im.Checkbox("##inherit"u8, ref _settingsInherit);
            Im.Line.Same();
            if (Im.SmallButton("Set##Inherit"u8))
                _lastSettingsError = new TryInheritMod(_pi)
                    .Invoke(collection, _settingsModDirectory, _settingsInherit, _settingsModName);
        }

        using (IpcTester.DrawIntro(TrySetMod.Label, "Set Enabled"u8))
        {
            table.NextColumn();
            Im.Checkbox("##enabled"u8, ref _settingsEnabled);
            Im.Line.Same();
            if (Im.SmallButton("Set##Enabled"u8))
                _lastSettingsError = new TrySetMod(_pi)
                    .Invoke(collection, _settingsModDirectory, _settingsEnabled, _settingsModName);
        }

        using (IpcTester.DrawIntro(TrySetModPriority.Label, "Set Priority"u8))
        {
            table.NextColumn();
            Im.Item.SetNextWidthScaled(200);
            Im.Drag("##Priority"u8, ref _settingsPriority);
            Im.Line.Same();
            if (Im.SmallButton("Set##Priority"u8))
                _lastSettingsError = new TrySetModPriority(_pi)
                    .Invoke(collection, _settingsModDirectory, _settingsPriority, _settingsModName);
        }

        using (IpcTester.DrawIntro(CopyModSettings.Label, "Copy Mod Settings"u8))
        {
            table.NextColumn();
            if (Im.SmallButton("Copy Settings"u8))
                _lastSettingsError = new CopyModSettings(_pi)
                    .Invoke(_settingsCollection, _settingsModDirectory, _settingsModName);
            Im.Tooltip.OnHover("Copy settings from Mod Directory Name to Mod Name (as directory) in collection."u8);
        }


        using (IpcTester.DrawIntro(TrySetModSetting.Label, "Set Setting(s)"u8))
        {
            if (_availableSettings == null)
                return;

            table.NextColumn();
            foreach (var (group, (list, type)) in _availableSettings)
            {
                using var id      = Im.Id.Push(group);
                var       preview = list.Length > 0 ? list[0] : string.Empty;
                if (_currentSettings is not null && _currentSettings.TryGetValue(group, out var current) && current.Count > 0)
                {
                    preview = current[0];
                }
                else
                {
                    current                  = [];
                    _currentSettings?[group] = current;
                }

                Im.Item.SetNextWidthScaled(200);
                using (var c = Im.Combo.Begin("##group"u8, preview))
                {
                    if (c)
                        foreach (var s in list)
                        {
                            var contained = current.Contains(s);
                            if (Im.Checkbox(s, ref contained))
                            {
                                if (contained)
                                    current.Add(s);
                                else
                                    current.Remove(s);
                            }
                        }
                }

                Im.Line.Same();
                if (Im.SmallButton("Set##setting"u8))
                    _lastSettingsError = type is GroupType.Single
                        ? new TrySetModSetting(_pi).Invoke(collection, _settingsModDirectory, group,
                            current.Count > 0 ? current[0] : string.Empty,
                            _settingsModName)
                        : new TrySetModSettings(_pi).Invoke(collection, _settingsModDirectory, group, current.ToArray(), _settingsModName);

                Im.Line.Same();
                Im.Text(group);
            }
        }
    }

    private void UpdateLastModSetting(ModSettingChange type, Guid collection, string mod, bool inherited)
    {
        _lastSettingChangeType       = type;
        _lastSettingChangeCollection = collection;
        _lastSettingChangeMod        = mod;
        _lastSettingChangeInherited  = inherited;
        _lastSettingChange           = DateTimeOffset.Now;
    }
}
