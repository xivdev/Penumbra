using Dalamud.Interface.DragDrop;
using Dalamud.Plugin.Services;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Files;
using Penumbra.Import.Models;
using Penumbra.UI.Classes;

namespace Penumbra.UI.FileEditing.Models;

public sealed class ModelEditorFactory(
    ModelManager models,
    ActiveCollections activeCollections,
    IDataManager gameData,
    Configuration config,
    FileDialogService fileDialog,
    IDragDropManager dragDropManager) : BaseFileEditorFactory(gameData), Luna.IUiService
{
    public override bool SupportsPath(string path)
        => path.EndsWith(".mdl", StringComparison.OrdinalIgnoreCase);

    public override IFileEditor CreateForData(byte[] data, string path, bool writable, FileEditingContext? context)
        => new ModelEditor(models, activeCollections, GameData, config, fileDialog, dragDropManager, context, new MdlFile(data), path);
}
