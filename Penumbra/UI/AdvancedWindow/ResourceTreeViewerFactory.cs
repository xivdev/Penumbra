using Dalamud.Plugin.Services;
using OtterGui.Services;
using Penumbra.Interop.ResourceTree;
using Penumbra.Services;

namespace Penumbra.UI.AdvancedWindow;

public class ResourceTreeViewerFactory(
    Configuration config,
    ResourceTreeFactory treeFactory,
    ChangedItemDrawer changedItemDrawer,
    IncognitoService incognito,
    CommunicatorService communicator,
    PcpService pcpService,
    IDataManager gameData) : IService
{
    public ResourceTreeViewer Create(int actionCapacity, Action onRefresh, Action<ResourceNode, Vector2> drawActions)
        => new(config, treeFactory, changedItemDrawer, incognito, actionCapacity, onRefresh, drawActions, communicator, pcpService, gameData);
}
