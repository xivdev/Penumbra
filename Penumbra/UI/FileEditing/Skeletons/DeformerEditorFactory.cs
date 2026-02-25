using Dalamud.Plugin.Services;
using Penumbra.GameData.Files;

namespace Penumbra.UI.FileEditing.Skeletons;

public sealed class DeformerEditorFactory(
    IDataManager gameData,
    Configuration configuration) : BaseFileEditorFactory(gameData), Luna.IUiService
{
    public override bool SupportsPath(string path)
        => path.EndsWith(".pbd", StringComparison.OrdinalIgnoreCase);

    public override IFileEditor CreateForData(byte[] data, string path, bool writable, FileEditingContext? context)
        => CreateForData((ReadOnlySpan<byte>)data, path, writable, context);

    public override IFileEditor CreateForData(ReadOnlySpan<byte> data, string path, bool writable, FileEditingContext? context)
        => new DeformerEditor(configuration, new PbdFile(data), path);
}
