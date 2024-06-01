using OtterGui.Services;
using Penumbra.Interop.ResourceTree;

namespace Penumbra.UI.AdvancedWindow;

public class ResourceTreeViewerFactory(
    Configuration config,
    ResourceTreeFactory treeFactory,
    ChangedItemDrawer changedItemDrawer,
    IncognitoService incognito) : IService
{
    public ResourceTreeViewer Create(int actionCapacity, Action onRefresh, Action<ResourceNode, Vector2> drawActions)
        => new(config, treeFactory, changedItemDrawer, incognito, actionCapacity, onRefresh, drawActions);
}
