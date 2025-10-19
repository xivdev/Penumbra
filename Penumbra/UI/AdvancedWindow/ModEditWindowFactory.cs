using Dalamud.Interface.DragDrop;
using Dalamud.Plugin.Services;
using Penumbra.Collections.Manager;
using Penumbra.Import.Models;
using Penumbra.Import.Textures;
using Penumbra.Interop.ResourceTree;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Mods.Editor;
using Penumbra.Services;
using Penumbra.UI.AdvancedWindow.Materials;
using Penumbra.UI.AdvancedWindow.Meta;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class ModEditWindowFactory(FileDialogService fileDialog, ItemSwapTabFactory itemSwapTabFactory, IDataManager gameData,
    Configuration config, ModEditorFactory editorFactory, ResourceTreeFactory resourceTreeFactory, MetaFileManager metaFileManager,
    ActiveCollections activeCollections, ModMergeTab modMergeTab,
    CommunicatorService communicator, TextureManager textures, ModelManager models, IDragDropManager dragDropManager,
    ResourceTreeViewerFactory resourceTreeViewerFactory, IFramework framework,
    MtrlTabFactory mtrlTabFactory, ModSelection selection) : WindowFactory<ModEditWindow>, Luna.IUiService
{
    protected override void OnWindowSystemSet()
    {
        if (config is { OpenWindowAtStart: true, Ephemeral.AdvancedEditingOpen: true } && selection.Mod is not null)
            OpenForMod(selection.Mod);
    }

    protected override ModEditWindow? DoCreateWindow()
    {
        var editor = editorFactory.Create();

        return new(fileDialog, itemSwapTabFactory.Create(), gameData, config, editor, resourceTreeFactory, metaFileManager,
            activeCollections, modMergeTab, communicator, textures, models, dragDropManager, resourceTreeViewerFactory, framework,
            CreateMetaDrawers(editor.MetaEditor), mtrlTabFactory, WindowSystem ?? throw new InvalidOperationException("WindowSystem not set"),
            GetFreeIndex());
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
        
        return new(eqdp, eqp, est, globalEqp, gmp, imc, rsp, atch, shp, atr);
    }

    public void OpenForMod(Mod mod)
    {
        var window = OpenWindows.FirstOrDefault(window => window.Mod == mod);
        if (window is not null)
        {
            window.BringToFront();
            return;
        }

        window = CreateWindow()!;
        window.ChangeMod(mod);
        window.ChangeOption(mod.Default);
    }
}
