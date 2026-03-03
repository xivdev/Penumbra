using Dalamud.Interface.DragDrop;
using Dalamud.Plugin.Services;
using Luna;
using Penumbra.Collections.Manager;
using Penumbra.Interop.ResourceTree;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Penumbra.UI.AdvancedWindow.Meta;
using Penumbra.UI.Classes;
using Penumbra.UI.FileEditing.Materials;
using Penumbra.UI.FileEditing.Models;
using Penumbra.UI.FileEditing.Shaders;
using Penumbra.UI.FileEditing.Skeletons;
using Penumbra.UI.FileEditing.Textures;

namespace Penumbra.UI.AdvancedWindow;

public sealed class ModEditWindowFactory(
    FileDialogService fileDialog,
    ItemSwapTabFactory itemSwapTabFactory,
    IDataManager gameData,
    Configuration config,
    ModEditorFactory editorFactory,
    ResourceTreeFactory resourceTreeFactory,
    MetaFileManager metaFileManager,
    ActiveCollections activeCollections,
    CommunicatorService communicator,
    IDragDropManager dragDropManager,
    ResourceTreeViewerFactory resourceTreeViewerFactory,
    IFramework framework,
    WindowSystem windowSystem,
    Logger log,
    MaterialEditorFactory materialEditorFactory,
    ModelEditorFactory modelEditorFactory,
    ShaderPackageEditorFactory shaderPackageEditorFactory,
    DeformerEditorFactory deformerEditorFactory,
    CombiningTextureEditorFactory textureEditorFactory,
    ModMergerFactory modMergerFactory) : WindowFactory<ModEditWindow>(log, windowSystem), IUiService
{
    protected override ModEditWindow CreateWindow(int index)
    {
        var editor = editorFactory.Create();
        return new ModEditWindow(fileDialog, itemSwapTabFactory.Create(), gameData, config, editor, resourceTreeFactory, metaFileManager,
            activeCollections, modMergerFactory.CreateTab(editor), communicator, dragDropManager, resourceTreeViewerFactory, framework,
            CreateMetaDrawers(editor.MetaEditor), materialEditorFactory, modelEditorFactory, shaderPackageEditorFactory, deformerEditorFactory,
            textureEditorFactory, index);
    }

    private MetaDrawers CreateMetaDrawers(ModMetaEditor metaEditor)
    {
        var eqdp      = new EqdpMetaDrawer(metaEditor, metaFileManager);
        var eqp       = new EqpMetaDrawer(metaEditor, metaFileManager);
        var est       = new EstMetaDrawer(metaEditor, metaFileManager);
        var globalEqp = new GlobalEqpMetaDrawer(metaEditor, metaFileManager);
        var gmp       = new GmpMetaDrawer(metaEditor, metaFileManager);
        var imc       = new ImcMetaDrawer(metaEditor, metaFileManager);
        var rsp       = new RspMetaDrawer(metaEditor, metaFileManager);
        var atch      = new AtchMetaDrawer(metaEditor, metaFileManager);
        var shp       = new ShpMetaDrawer(metaEditor, metaFileManager);
        var atr       = new AtrMetaDrawer(metaEditor, metaFileManager);

        return new MetaDrawers(eqdp, eqp, est, globalEqp, gmp, imc, rsp, atch, shp, atr);
    }

    public void OpenForMod(Mod mod)
    {
        var window = Windows.FirstOrDefault(window => window.Mod == mod);
        if (window is not null)
        {
            window.BringToFront();
            return;
        }

        window = CreateWindowInternal();
        if (window is null)
            return;

        window.ChangeMod(mod);
        window.ChangeOption(mod.Default);
    }
}
