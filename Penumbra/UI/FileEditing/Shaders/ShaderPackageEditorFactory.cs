using Dalamud.Plugin.Services;
using Penumbra.Api.Enums;
using Penumbra.GameData.Files;
using Penumbra.UI.Classes;

namespace Penumbra.UI.FileEditing.Shaders;

public sealed class ShaderPackageEditorFactory(
    FileDialogService fileDialog,
    IDataManager gameData) : BaseFileEditorFactory(gameData), Luna.IUiService
{
    public override string Identifier
        => typeof(ShaderPackageEditor).FullName!;

    public override string DisplayName
        => "Penumbra Shader Package Editor";

    public override IEnumerable<ResourceType> SupportedResourceTypes
        => [ResourceType.Shpk];

    public override IFileEditor CreateForData(byte[] data, string path, bool writable, string? gamePath, FileEditingContext? context)
        => CreateForData((ReadOnlySpan<byte>)data, path, writable, gamePath, context);

    public override IFileEditor CreateForData(ReadOnlySpan<byte> data, string path, bool writable, string? gamePath,
        FileEditingContext? context)
        => new ShaderPackageEditor(fileDialog, Parse(data), path);

    private static ShpkFile Parse(ReadOnlySpan<byte> data)
    {
        try
        {
            return new ShpkFile(data, true);
        }
        catch (NotImplementedException)
        {
            return new ShpkFile(data, false);
        }
    }
}
