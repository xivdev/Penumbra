using Dalamud.Plugin.Services;
using Penumbra.GameData.Files;
using Penumbra.UI.Classes;

namespace Penumbra.UI.FileEditing.Shaders;

public sealed class ShaderPackageEditorFactory(
    FileDialogService fileDialog,
    IDataManager gameData) : BaseFileEditorFactory(gameData), Luna.IUiService
{
    public override bool SupportsPath(string path)
        => path.EndsWith(".shpk", StringComparison.OrdinalIgnoreCase);

    public override IFileEditor CreateForData(byte[] data, string path, bool writable, FileEditingContext? context)
        => CreateForData((ReadOnlySpan<byte>)data, path, writable, context);

    public override IFileEditor CreateForData(ReadOnlySpan<byte> data, string path, bool writable, FileEditingContext? context)
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
