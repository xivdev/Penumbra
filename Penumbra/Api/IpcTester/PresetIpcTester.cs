using Dalamud.Plugin;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;
using Penumbra.GameData.Gui;

namespace Penumbra.Api.IpcTester;

public class PresetIpcTester(IDalamudPluginInterface pi) : IUiService
{
    private readonly IDalamudPluginInterface _pi = pi;

    private          Guid?                      _tempGuid;
    private          string                     _modDirectory = string.Empty;
    private          int                        _playerIndex;
    private          int                        _key;
    private          PenumbraApiEc              _lastError;
    private          PresetQueryMode            _queryMode;
    private          PresetApplyMode            _applyMode;
    private readonly EnumCombo<PresetApplyMode> _combo = new();

    public void Draw()
    {
        using var _ = Im.Tree.Node("Presets"u8);
        if (!_)
            return;

        ImEx.GuidInput("Collection ID##guid"u8, ref _tempGuid);
        Im.Input.Scalar("Player Index"u8, ref _playerIndex);
        Im.Input.Text("##mod"u8, ref _modDirectory, "Existing Mod Directory..."u8);
        Im.Input.Scalar("Key"u8, ref _key);

        Im.Checkbox("Ignore Temporary"u8, ref _queryMode, PresetQueryMode.IgnoreTemporary);
        Im.Line.Same();
        Im.Checkbox("Ignore Inheritance"u8, ref _queryMode, PresetQueryMode.IgnoreInheritance);

        Im.Checkbox("Ignore Settings"u8, ref _queryMode, PresetQueryMode.IgnoreSettings);
        Im.Line.Same();
        Im.Checkbox("Get Default"u8, ref _queryMode, PresetQueryMode.GetDefault);

        Im.Checkbox("Ignore Disabled"u8, ref _queryMode, PresetQueryMode.IgnoreDisabled);
        Im.Line.Same();
        _combo.Draw("Apply Mode"u8, ref _applyMode, StringU8.Empty, 150 * Im.Style.GlobalScale);

        using var table = Im.Table.Begin(StringU8.Empty, 3, TableFlags.SizingFixedFit);
        if (!table)
            return;

        using (IpcTester.DrawIntro("Last Apply Error"u8, $"{_lastError}"))
        { }

        using (IpcTester.DrawIntro(GetPreset.LabelU8, "Get Preset"u8))
        {
            Im.Button("Hover"u8);
            if (Im.Item.Hovered())
            {
                using var tt = Im.Tooltip.Begin();
                var ec = new GetPreset(_pi).Invoke(_tempGuid ?? Guid.Empty, (_modDirectory, string.Empty), out var data, _queryMode, _key);
                if (ec is not PenumbraApiEc.Success)
                    Im.Text(ec.StringU8);
                else
                    data!.Value.DrawTooltip(null, (in _, out name) =>
                    {
                        name = null;
                        return [];
                    });
            }
        }

        using (IpcTester.DrawIntro(GetPresetPlayer.LabelU8, "Get Preset (Player)"u8))
        {
            Im.Button("Hover"u8);
            if (Im.Item.Hovered())
            {
                using var tt = Im.Tooltip.Begin();
                var       ec = new GetPresetPlayer(_pi).Invoke(_playerIndex, (_modDirectory, string.Empty), out var data, _queryMode, _key);
                if (ec is not PenumbraApiEc.Success)
                    Im.Text(ec.StringU8);
                else
                    data!.Value.DrawTooltip(null, (in _, out name) =>
                    {
                        name = null;
                        return [];
                    });
            }
        }

        using (IpcTester.DrawIntro(ApplyPreset.LabelU8, "Apply Preset"u8))
        {
            if (Im.Button("Apply (Clipboard)"u8) && SettingPresetData.FromClipboard(out var data))
                _lastError = new ApplyPreset(_pi).Invoke(_tempGuid ?? Guid.Empty, (_modDirectory, string.Empty), data, "IpcTester", _applyMode,
                    _key);
        }

        using (IpcTester.DrawIntro(ApplyPresetPlayer.LabelU8, "Apply Preset (Player)"u8))
        {
            if (Im.Button("Apply (Clipboard)"u8) && SettingPresetData.FromClipboard(out var data))
                _lastError = new ApplyPresetPlayer(_pi).Invoke(_playerIndex, (_modDirectory, string.Empty), data, "IpcTester", _applyMode,
                    _key);
        }
    }
}
