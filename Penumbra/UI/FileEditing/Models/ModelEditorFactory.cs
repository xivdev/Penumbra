using Dalamud.Interface.DragDrop;
using Dalamud.Plugin.Services;
using Penumbra.Api.Enums;
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
    public override string Identifier
        => typeof(ModelEditor).FullName!;

    public override string DisplayName
        => "Penumbra Model Editor";

    public override IEnumerable<ResourceType> SupportedResourceTypes
        => [ResourceType.Mdl];

    public override IFileEditor CreateForData(byte[] data, string path, bool writable, string? gamePath, FileEditingContext? context)
        => new ModelEditor(models, activeCollections, GameData, config, fileDialog, dragDropManager, context, new MdlFile(data), path,
            gamePath);
}
