using Dalamud.Plugin;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;

namespace Penumbra.Api.IpcTester;

public class EditingIpcTester(IDalamudPluginInterface pi) : IUiService
{
    private string _inputPath   = string.Empty;
    private string _inputPath2  = string.Empty;
    private string _outputPath  = string.Empty;
    private string _outputPath2 = string.Empty;

    private TextureType _typeSelector;
    private bool        _mipMaps = true;

    private Task? _task1;
    private Task? _task2;

    public void Draw()
    {
        using var _ = ImRaii.TreeNode("Editing");
        if (!_)
            return;

        ImGui.InputTextWithHint("##inputPath",   "Input Texture Path...",    ref _inputPath,   256);
        ImGui.InputTextWithHint("##outputPath",  "Output Texture Path...",   ref _outputPath,  256);
        ImGui.InputTextWithHint("##inputPath2",  "Input Texture Path 2...",  ref _inputPath2,  256);
        ImGui.InputTextWithHint("##outputPath2", "Output Texture Path 2...", ref _outputPath2, 256);
        TypeCombo();
        ImGui.Checkbox("Add MipMaps", ref _mipMaps);

        using var table = ImRaii.Table("...", 3, ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return;

        IpcTester.DrawIntro(ConvertTextureFile.Label, (string)"Convert Texture 1");
        if (ImGuiUtil.DrawDisabledButton("Save 1", Vector2.Zero, string.Empty, _task1 is { IsCompleted: false }))
            _task1 = new ConvertTextureFile(pi).Invoke(_inputPath, _outputPath, _typeSelector, _mipMaps);
        ImGui.SameLine();
        ImGui.TextUnformatted(_task1 == null ? "Not Initiated" : _task1.Status.ToString());
        if (ImGui.IsItemHovered() && _task1?.Status == TaskStatus.Faulted)
            ImGui.SetTooltip(_task1.Exception?.ToString());

        IpcTester.DrawIntro(ConvertTextureFile.Label, (string)"Convert Texture 2");
        if (ImGuiUtil.DrawDisabledButton("Save 2", Vector2.Zero, string.Empty, _task2 is { IsCompleted: false }))
            _task2 = new ConvertTextureFile(pi).Invoke(_inputPath2, _outputPath2, _typeSelector, _mipMaps);
        ImGui.SameLine();
        ImGui.TextUnformatted(_task2 == null ? "Not Initiated" : _task2.Status.ToString());
        if (ImGui.IsItemHovered() && _task2?.Status == TaskStatus.Faulted)
            ImGui.SetTooltip(_task2.Exception?.ToString());
    }

    private void TypeCombo()
    {
        using var combo = ImRaii.Combo("Convert To", _typeSelector.ToString());
        if (!combo)
            return;

        foreach (var value in Enum.GetValues<TextureType>())
        {
            if (ImGui.Selectable(value.ToString(), _typeSelector == value))
                _typeSelector = value;
        }
    }
}
