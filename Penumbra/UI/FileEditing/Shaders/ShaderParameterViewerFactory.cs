using Dalamud.Plugin.Services;
using Penumbra.Api.Enums;
using Penumbra.GameData.Files;
using Penumbra.UI.Classes;

namespace Penumbra.UI.FileEditing.Shaders;

public class ShaderParameterViewerFactory(
    FileDialogService fileDialog,
    IDataManager gameData) : BaseFileEditorFactory(gameData), Luna.IUiService
{
    public override string Identifier
        => typeof(ShaderParameterViewer).FullName!;

    public override string DisplayName
        => "Penumbra Shader Parameter Viewer";

    public override IEnumerable<ResourceType> SupportedResourceTypes
        => [ResourceType.Spm];

    public override IFileEditor CreateForData(byte[] data, string path, bool writable, string? gamePath, FileEditingContext? context)
        => CreateForData((ReadOnlySpan<byte>)data, path, writable, gamePath, context);

    public override IFileEditor CreateForData(ReadOnlySpan<byte> data, string path, bool writable, string? gamePath,
        FileEditingContext? context)
        => new ShaderParameterViewer(fileDialog, new SpmFile(data), path);
}
