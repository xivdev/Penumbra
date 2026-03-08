using Dalamud.Interface.DragDrop;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Penumbra.Api.Enums;
using Penumbra.Import.Textures;
using Penumbra.Interop.ResourceTree;
using Penumbra.Mods.Editor;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.FileEditing.Textures;

public sealed class CombiningTextureEditorFactory(
    TextureManager textures,
    IDragDropManager dragDropManager,
    FileDialogService fileDialog,
    Configuration config,
    IFramework framework,
    ModManager modManager,
    CommunicatorService communicator,
    ResourceTreeFactory resourceTreeFactory,
    IDataManager gameData) : IFileEditorFactory, Luna.IUiService
{
    public string Identifier
        => typeof(CombiningTextureEditor).FullName!;

    public string DisplayName
        => "Penumbra Texture Editor and Combiner";

    public IEnumerable<ResourceType> SupportedResourceTypes
        => [ResourceType.Atex, ResourceType.Tex];

    private CombiningTextureEditor Create(FileEditingContext? context, bool inModEditWindow, bool writable)
    {
        var textureSelectCombo = context?.Editor is { } editor
            ? new TextureSelectCombo(resourceTreeFactory, editor, gameData)
            : null;

        return new CombiningTextureEditor(textures, dragDropManager, fileDialog, config, framework, modManager, communicator,
            textureSelectCombo, context,
            inModEditWindow, writable);
    }

    public bool SupportsFile(string path, string? gamePath)
        => IsTexturePath(path) && File.Exists(path);

    public IFileEditor CreateForFile(string path, bool writable, string? gamePath, FileEditingContext? context)
    {
        var editor = Create(context, false, writable);
        editor.LoadLeft(path);
        return editor;
    }

    public bool SupportsGameFile(string path)
        => IsTexturePath(path) && gameData.FileExists(path);

    public IFileEditor CreateForGameFile(string path, FileEditingContext? context)
    {
        var editor = Create(context, false, false);
        editor.LoadLeft(path);
        return editor;
    }

    public unsafe bool SupportsResourceHandle(ResourceHandle* handle, string? gamePath)
        => false;

    public unsafe IFileEditor CreateForResourceHandle(ResourceHandle* handle, string? gamePath, FileEditingContext? context)
        => throw new NotSupportedException();

    private static bool IsTexturePath(string path)
        => ResourceType.FromPath(path) is ResourceType.Atex or ResourceType.Tex;

    public CombiningTextureEditor CreateForModEditWindow(FileEditingContext? context)
        => Create(context, true, true);
}
