using OtterGui.Widgets;
using Penumbra.UI.AdvancedWindow;

namespace Penumbra.UI.Tabs;

public class OnScreenTab : ITab
{
    private readonly ResourceTreeViewer _viewer;

    public OnScreenTab(ResourceTreeViewerFactory resourceTreeViewerFactory)
    {
        _viewer = resourceTreeViewerFactory.Create(0, delegate { }, delegate { });
    }

    public ReadOnlySpan<byte> Label
        => "On-Screen"u8;

    public void DrawContent()
        => _viewer.Draw();
}
