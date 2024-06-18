using OtterGui.Services;
using OtterGui.Widgets;
using Penumbra.UI.AdvancedWindow;

namespace Penumbra.UI.Tabs;

public class OnScreenTab(ResourceTreeViewerFactory resourceTreeViewerFactory) : ITab, IUiService
{
    private readonly ResourceTreeViewer _viewer = resourceTreeViewerFactory.Create(0, delegate { }, delegate { });

    public ReadOnlySpan<byte> Label
        => "On-Screen"u8;

    public void DrawContent()
        => _viewer.Draw();
}
