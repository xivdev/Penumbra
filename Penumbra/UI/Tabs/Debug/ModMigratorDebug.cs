using ImSharp;
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
        if (!Im.Tree.HeaderId("Mod Migrator"u8))
            return;

        Im.Input.Text("##input"u8,  ref _inputPath,  "Input Path..."u8);
        Im.Input.Text("##output"u8, ref _outputPath, "Output Path..."u8);

        if (ImEx.Button("Create Index Texture"u8, default, "Requires input to be a path to a normal texture."u8, _inputPath.Length is 0
             || _outputPath.Length is 0
             || _indexTask is
                {
                    IsCompleted: false,
                }))
            _indexTask = migrator.CreateIndexFile(_inputPath, _outputPath);

        if (_indexTask is not null)
        {
            Im.Line.Same();
            ImEx.TextFrameAligned($"{_indexTask.Status}");
        }

        if (ImEx.Button("Update Model File"u8, default, "Requires input to be a path to a mdl."u8, _inputPath.Length is 0
             || _outputPath.Length is 0
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
            ImEx.TextFrameAligned($"{_mdlTask.Status}");
        }
    }
}
