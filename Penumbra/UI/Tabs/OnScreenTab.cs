using Luna;
using Penumbra.Api.Enums;
using Penumbra.UI.AdvancedWindow;

namespace Penumbra.UI.Tabs;

public sealed class OnScreenTab(ResourceTreeViewerFactory resourceTreeViewerFactory) : ITab<TabType>
{
    private readonly ResourceTreeViewer _viewer = resourceTreeViewerFactory.Create(0, delegate { }, delegate { });

    public ReadOnlySpan<byte> Label
        => "On-Screen"u8;

    public void DrawContent()
        => _viewer.Draw();

    public TabType Identifier
        => TabType.OnScreen;
}
