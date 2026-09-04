using Dalamud.Plugin.Services;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Gui;
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
    TextureArraySlicePickers textureArraySlicePickers,
    Configuration config) : BaseFileEditorFactory(gameData), IUiService
{
    public override string Identifier
        => typeof(MaterialEditor).FullName!;

    public override string DisplayName
        => "Penumbra Material Editor";

    public override IEnumerable<ResourceType> SupportedResourceTypes
        => [ResourceType.Mtrl];

    public override IFileEditor CreateForData(byte[] data, string path, bool writable, string? gamePath, FileEditingContext? context)
        => CreateForData((ReadOnlySpan<byte>)data, path, writable, gamePath, context);

    public override IFileEditor CreateForData(ReadOnlySpan<byte> data, string path, bool writable, string? gamePath,
        FileEditingContext? context)
        => new MaterialEditor(GameData, framework, objects, characterBaseDestructor, stainService, resourceTreeFactory, fileDialog,
            textureArraySlicePickers, config, context, new MtrlFile(data), path, writable);
}
