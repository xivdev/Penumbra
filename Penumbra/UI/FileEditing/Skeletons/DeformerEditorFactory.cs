using Dalamud.Plugin.Services;
using Penumbra.Api.Enums;
using Penumbra.GameData.Files;

namespace Penumbra.UI.FileEditing.Skeletons;

public sealed class DeformerEditorFactory(
    IDataManager gameData,
    Configuration configuration) : BaseFileEditorFactory(gameData), Luna.IUiService
{
    public override string Identifier
        => typeof(DeformerEditor).FullName!;

    public override string DisplayName
        => "Penumbra Pre Bone Deformer Editor";

    public override IEnumerable<ResourceType> SupportedResourceTypes
        => [ResourceType.Pbd];

    public override IFileEditor CreateForData(byte[] data, string path, bool writable, string? gamePath, FileEditingContext? context)
        => CreateForData((ReadOnlySpan<byte>)data, path, writable, gamePath, context);

    public override IFileEditor CreateForData(ReadOnlySpan<byte> data, string path, bool writable, string? gamePath,
        FileEditingContext? context)
        => new DeformerEditor(configuration, new PbdFile(data), path);
}
