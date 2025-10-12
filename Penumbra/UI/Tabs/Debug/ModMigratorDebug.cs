using Dalamud.Bindings.ImGui;
using ImSharp;
using OtterGui.Text;
using Penumbra.Services;

namespace Penumbra.UI.Tabs.Debug;

public class ModMigratorDebug(ModMigrator migrator) : Luna.IUiService
{
    private string _inputPath  = string.Empty;
    private string _outputPath = string.Empty;
    private Task?  _indexTask;
    private Task?  _mdlTask;

    public void Draw()
    {
        if (!ImUtf8.CollapsingHeaderId("Mod Migrator"u8))
            return;

        ImUtf8.InputText("##input"u8,  ref _inputPath,  "Input Path..."u8);
        ImUtf8.InputText("##output"u8, ref _outputPath, "Output Path..."u8);

        if (ImUtf8.ButtonEx("Create Index Texture"u8, "Requires input to be a path to a normal texture."u8, default, _inputPath.Length == 0
             || _outputPath.Length == 0
             || _indexTask is
                {
                    IsCompleted: false,
                }))
            _indexTask = migrator.CreateIndexFile(_inputPath, _outputPath);

        if (_indexTask is not null)
        {
            Im.Line.Same();
            ImUtf8.TextFrameAligned($"{_indexTask.Status}");
        }

        if (ImUtf8.ButtonEx("Update Model File"u8, "Requires input to be a path to a mdl."u8, default, _inputPath.Length == 0
             || _outputPath.Length == 0
             || _mdlTask is
                {
                    IsCompleted: false,
                }))
            _mdlTask = Task.Run(() =>
            {
                File.Copy(_inputPath, _outputPath, true);
                MigrationManager.TryMigrateSingleModel(_outputPath, false);
            });

        if (_mdlTask is not null)
        {
            Im.Line.Same();
            ImUtf8.TextFrameAligned($"{_mdlTask.Status}");
        }
    }
}
