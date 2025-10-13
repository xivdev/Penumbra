using Dalamud.Plugin;
using ImSharp;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;

namespace Penumbra.Api.IpcTester;

public class EditingIpcTester(IDalamudPluginInterface pi) : Luna.IUiService
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
        using var _ = Im.Tree.Node("Editing"u8);
        if (!_)
            return;

        Im.Input.Text("##inputPath"u8,   ref _inputPath,   "Input Texture Path..."u8);
        Im.Input.Text("##outputPath"u8,  ref _outputPath,  "Output Texture Path..."u8);
        Im.Input.Text("##inputPath2"u8,  ref _inputPath2,  "Input Texture Path 2..."u8);
        Im.Input.Text("##outputPath2"u8, ref _outputPath2, "Output Texture Path 2..."u8);
        EnumCombo<TextureType>.Instance.Draw("Convert To"u8, ref _typeSelector, StringU8.Empty, 200 * Im.Style.GlobalScale);
        Im.Checkbox("Add MipMaps"u8, ref _mipMaps);

        using var table = Im.Table.Begin("..."u8, 3, TableFlags.SizingFixedFit);
        if (!table)
            return;

        using (IpcTester.DrawIntro(ConvertTextureFile.Label, "Convert Texture 1"u8))
        {
            table.NextColumn();
            if (ImEx.Button("Save 1"u8, Vector2.Zero, StringU8.Empty, _task1 is { IsCompleted: false }))
                _task1 = new ConvertTextureFile(pi).Invoke(_inputPath, _outputPath, _typeSelector, _mipMaps);
            Im.Line.Same();
            Im.Text(_task1 is null ? "Not Initiated"u8 : $"{_task1.Status}");
            if (Im.Item.Hovered() && _task1?.Status is TaskStatus.Faulted)
                Im.Tooltip.Set($"{_task1.Exception}");
        }

        using (IpcTester.DrawIntro(ConvertTextureFile.Label, "Convert Texture 2"u8))
        {
            table.NextColumn();
            if (ImEx.Button("Save 2"u8, Vector2.Zero, StringU8.Empty, _task2 is { IsCompleted: false }))
                _task2 = new ConvertTextureFile(pi).Invoke(_inputPath2, _outputPath2, _typeSelector, _mipMaps);
            Im.Line.Same();
            Im.Text(_task2 is null ? "Not Initiated"u8 : $"{_task2.Status}");
            if (Im.Item.Hovered() && _task2?.Status is TaskStatus.Faulted)
                Im.Tooltip.Set($"{_task2.Exception}");
        }
    }
}
