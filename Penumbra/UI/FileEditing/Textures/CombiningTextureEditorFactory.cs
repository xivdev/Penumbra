using Dalamud.Interface.DragDrop;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Penumbra.Import.Textures;
using Penumbra.Interop.ResourceTree;
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
    private CombiningTextureEditor Create(FileEditingContext? context, bool inModEditWindow, bool writable)
    {
        var textureSelectCombo = context?.Editor is { } editor
            ? new TextureSelectCombo(resourceTreeFactory, editor, gameData)
            : null;

        return new CombiningTextureEditor(textures, dragDropManager, fileDialog, config, framework, modManager, communicator,
            textureSelectCombo, context,
            inModEditWindow, writable);
    }

    public bool SupportsFile(string path)
        => IsTexturePath(path) && File.Exists(path);

    public IFileEditor CreateForFile(string path, bool writable, FileEditingContext? context)
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

    public unsafe bool SupportsResourceHandle(ResourceHandle* handle)
        => false;

    public unsafe IFileEditor CreateForResourceHandle(ResourceHandle* handle, FileEditingContext? context)
        => throw new NotSupportedException();

    private static bool IsTexturePath(string path)
        => path.EndsWith(".tex", StringComparison.OrdinalIgnoreCase);

    public CombiningTextureEditor CreateForModEditWindow(FileEditingContext? context)
        => Create(context, true, true);
}
