using Dalamud.Plugin;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;
using Penumbra.UI;

namespace Penumbra.Api.IpcTester;

public class ModSettingsIpcTester : IUiService, IDisposable
{
    private readonly IDalamudPluginInterface                                _pi;
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
    {
        SettingChanged.Dispose();
    }

    public void Draw()
    {
        using var _ = ImRaii.TreeNode("Mod Settings");
        if (!_)
            return;

        ImGui.InputTextWithHint("##settingsDir",  "Mod Directory Name...", ref _settingsModDirectory, 100);
        ImGui.InputTextWithHint("##settingsName", "Mod Name...",           ref _settingsModName,      100);
        ImGuiUtil.GuidInput("##settingsCollection", "Collection...", string.Empty, ref _settingsCollection, ref _settingsCollectionName);
        ImUtf8.Checkbox("Ignore Inheritance"u8, ref _settingsIgnoreInheritance);
        ImUtf8.Checkbox("Ignore Temporary"u8, ref _settingsIgnoreTemporary);
        ImUtf8.InputScalar("Key"u8, ref _settingsKey);
        var collection = _settingsCollection.GetValueOrDefault(Guid.Empty);

        using var table = ImRaii.Table(string.Empty, 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro("Last Error", _lastSettingsError.ToString());

        IpcTester.DrawIntro(ModSettingChanged.Label, "Last Mod Setting Changed");
        ImGui.TextUnformatted(_lastSettingChangeMod.Length > 0
            ? $"{_lastSettingChangeType} of {_lastSettingChangeMod} in {_lastSettingChangeCollection}{(_lastSettingChangeInherited ? " (Inherited)" : string.Empty)} at {_lastSettingChange}"
            : "None");

        IpcTester.DrawIntro(GetAvailableModSettings.Label, "Get Available Settings");
        if (ImGui.Button("Get##Available"))
        {
            _availableSettings = new GetAvailableModSettings(_pi).Invoke(_settingsModDirectory, _settingsModName);
            _lastSettingsError = _availableSettings == null ? PenumbraApiEc.ModMissing : PenumbraApiEc.Success;
        }

        IpcTester.DrawIntro(GetCurrentModSettings.Label, "Get Current Settings");
        if (ImGui.Button("Get##Current"))
        {
            var ret = new GetCurrentModSettings(_pi)
                .Invoke(collection, _settingsModDirectory, _settingsModName, _settingsIgnoreInheritance);
            _lastSettingsError = ret.Item1;
            if (ret.Item1 == PenumbraApiEc.Success)
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

        IpcTester.DrawIntro(GetCurrentModSettingsWithTemp.Label, "Get Current Settings With Temp");
        if (ImGui.Button("Get##CurrentTemp"))
        {
            var ret = new GetCurrentModSettingsWithTemp(_pi)
                .Invoke(collection, _settingsModDirectory, _settingsModName, _settingsIgnoreInheritance, _settingsIgnoreTemporary, _settingsKey);
            _lastSettingsError = ret.Item1;
            if (ret.Item1 == PenumbraApiEc.Success)
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
        
        IpcTester.DrawIntro(GetAllModSettings.Label, "Get All Mod Settings");
        if (ImGui.Button("Get##All"))
        {
            var ret = new GetAllModSettings(_pi).Invoke(collection, _settingsIgnoreInheritance, _settingsIgnoreTemporary, _settingsKey);
            _lastSettingsError = ret.Item1;
            _allSettings       = ret.Item2;
        }

        if (_allSettings != null)
        {
            ImGui.SameLine();
            ImUtf8.Text($"{_allSettings.Count} Mods");
        }

        IpcTester.DrawIntro(TryInheritMod.Label, "Inherit Mod");
        ImGui.Checkbox("##inherit", ref _settingsInherit);
        ImGui.SameLine();
        if (ImGui.Button("Set##Inherit"))
            _lastSettingsError = new TryInheritMod(_pi)
                .Invoke(collection, _settingsModDirectory, _settingsInherit, _settingsModName);

        IpcTester.DrawIntro(TrySetMod.Label, "Set Enabled");
        ImGui.Checkbox("##enabled", ref _settingsEnabled);
        ImGui.SameLine();
        if (ImGui.Button("Set##Enabled"))
            _lastSettingsError = new TrySetMod(_pi)
                .Invoke(collection, _settingsModDirectory, _settingsEnabled, _settingsModName);

        IpcTester.DrawIntro(TrySetModPriority.Label, "Set Priority");
        ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
        ImGui.DragInt("##Priority", ref _settingsPriority);
        ImGui.SameLine();
        if (ImGui.Button("Set##Priority"))
            _lastSettingsError = new TrySetModPriority(_pi)
                .Invoke(collection, _settingsModDirectory, _settingsPriority, _settingsModName);

        IpcTester.DrawIntro(CopyModSettings.Label, "Copy Mod Settings");
        if (ImGui.Button("Copy Settings"))
            _lastSettingsError = new CopyModSettings(_pi)
                .Invoke(_settingsCollection, _settingsModDirectory, _settingsModName);

        ImGuiUtil.HoverTooltip("Copy settings from Mod Directory Name to Mod Name (as directory) in collection.");

        IpcTester.DrawIntro(TrySetModSetting.Label, "Set Setting(s)");
        if (_availableSettings == null)
            return;

        foreach (var (group, (list, type)) in _availableSettings)
        {
            using var id      = ImRaii.PushId(group);
            var       preview = list.Length > 0 ? list[0] : string.Empty;
            if (_currentSettings != null && _currentSettings.TryGetValue(group, out var current) && current.Count > 0)
            {
                preview = current[0];
            }
            else
            {
                current = [];
                if (_currentSettings != null)
                    _currentSettings[group] = current;
            }

            ImGui.SetNextItemWidth(200 * UiHelpers.Scale);
            using (var c = ImRaii.Combo("##group", preview))
            {
                if (c)
                    foreach (var s in list)
                    {
                        var contained = current.Contains(s);
                        if (ImGui.Checkbox(s, ref contained))
                        {
                            if (contained)
                                current.Add(s);
                            else
                                current.Remove(s);
                        }
                    }
            }

            ImGui.SameLine();
            if (ImGui.Button("Set##setting"))
                _lastSettingsError = type == GroupType.Single
                    ? new TrySetModSetting(_pi).Invoke(collection, _settingsModDirectory, group, current.Count > 0 ? current[0] : string.Empty,
                        _settingsModName)
                    : new TrySetModSettings(_pi).Invoke(collection, _settingsModDirectory, group, current.ToArray(), _settingsModName);

            ImGui.SameLine();
            ImGui.TextUnformatted(group);
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
