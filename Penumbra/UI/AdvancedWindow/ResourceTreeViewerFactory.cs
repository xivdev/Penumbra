using Dalamud.Plugin.Services;
using Luna;
using Penumbra.Communication;
using Penumbra.GameData.Files;
using Penumbra.Interop.ResourceTree;
using Penumbra.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public class ResourceTreeViewerFactory(
    Configuration config,
    ResourceTreeFactory treeFactory,
    ChangedItemDrawer changedItemDrawer,
    IncognitoService incognito,
    PcpService pcpService,
    IDataManager gameData,
    FileDialogService fileDialog,
    FileCompactor compactor,
    UiNavigator navigator) : IService
{
    public ResourceTreeViewer Create(int actionCapacity, Action onRefresh, Action<ResourceNode, IWritable?, Vector2> drawActions)
        => new(config, treeFactory, changedItemDrawer, incognito, actionCapacity, onRefresh, drawActions, pcpService, gameData,
            fileDialog, compactor, navigator);
}
