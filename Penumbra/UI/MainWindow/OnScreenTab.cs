using Luna;
using Penumbra.Api.Enums;
using Penumbra.UI.AdvancedWindow;

namespace Penumbra.UI.MainWindow;

public sealed class OnScreenTab : ITab<TabType>
{
    private readonly ResourceTreeViewer _viewer;

    public OnScreenTab(Configuration config, ResourceTreeViewerFactory resourceTreeViewerFactory)
    {
        // Hack to handle config settings because no specific filters have been made yet.
        if (!config.RememberOnScreenFilters)
            config.Filters.ClearOnScreenFilters();

        _viewer = resourceTreeViewerFactory.Create(0, delegate { }, delegate { });
    }

    public ReadOnlySpan<byte> Label
        => "On-Screen"u8;

    public void DrawContent()
        => _viewer.Draw();

    public TabType Identifier
        => TabType.OnScreen;
}
