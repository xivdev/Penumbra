using OtterGui.Widgets;
using Penumbra.Interop.ResourceTree;
using Penumbra.UI.AdvancedWindow;

namespace Penumbra.UI.Tabs;

public class OnScreenTab : ITab
{
    private readonly Configuration      _config;
    private          ResourceTreeViewer _viewer;

    public OnScreenTab(Configuration config, ResourceTreeFactory treeFactory, ChangedItemDrawer changedItemDrawer)
    {
        _config = config;
        _viewer = new ResourceTreeViewer(_config, treeFactory, changedItemDrawer, 0, delegate { }, delegate { });
    }

    public ReadOnlySpan<byte> Label
        => "On-Screen"u8;

    public void DrawContent()
        => _viewer.Draw();
}
