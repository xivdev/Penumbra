using Dalamud.Plugin.Services;
using Penumbra.GameData.Files;
using Penumbra.GameData.Interop;
using Penumbra.Interop.Hooks.Objects;
using Penumbra.Interop.ResourceTree;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.FileEditing.Materials;

public sealed class MaterialEditorFactory(
    IDataManager gameData,
    IFramework framework,
    ObjectManager objects,
    CharacterBaseDestructor characterBaseDestructor,
    StainService stainService,
    ResourceTreeFactory resourceTreeFactory,
    FileDialogService fileDialog,
    MaterialTemplatePickers materialTemplatePickers,
    Configuration config) : BaseFileEditorFactory(gameData), Luna.IUiService
{
    public MaterialEditor Create(MtrlFile file, string filePath, bool writable, FileEditingContext? context)
        => new(GameData, framework, objects, characterBaseDestructor, stainService, resourceTreeFactory, fileDialog,
            materialTemplatePickers, config, context, file, filePath, writable);

    protected override bool SupportsPath(string path)
        => path.EndsWith(".mtrl", StringComparison.OrdinalIgnoreCase);

    protected override IFileEditor CreateForData(byte[] data, string path, bool writable, FileEditingContext? context)
        => Create(new MtrlFile(data), path, writable, context);

    protected override IFileEditor CreateForData(ReadOnlySpan<byte> data, string path, bool writable, FileEditingContext? context)
        => Create(new MtrlFile(data), path, writable, context);
}
