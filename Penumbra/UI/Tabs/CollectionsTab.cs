using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.UI.Classes;
using Penumbra.UI.CollectionTab;

namespace Penumbra.UI.Tabs;

public sealed class CollectionsTab : TwoPanelLayout, ITab<TabType>
{
    private readonly TutorialService _tutorial;
    private readonly UiConfig        _config;

    public TabType Identifier
        => TabType.Collections;

    public CollectionsTab(TutorialService tutorial, CollectionButtonFooter leftFooter, CollectionSelector leftPanel, CollectionFilter filter,
        CollectionModeHeader rightHeader, CollectionPanel rightPanel, UiConfig config)
    {
        LeftHeader  = new FilterHeader<CollectionSelector.Entry>(filter, new StringU8("Filter..."u8));
        LeftPanel   = leftPanel;
        LeftFooter  = leftFooter;
        RightHeader = rightHeader;
        RightPanel  = rightPanel;
        RightFooter = NopHeaderFooter.Instance;
        _tutorial   = tutorial;
        _config     = config;
    }

    protected override float MinimumWidth
        => LeftFooter.MinimumWidth;

    protected override float MaximumWidth
        => Im.Window.Width - 690 * Im.Style.GlobalScale;

    public override ReadOnlySpan<byte> Label
        => "Collections"u8;

    protected override void DrawLeftGroup(in TwoPanelWidth width)
    {
        base.DrawLeftGroup(width);
        _tutorial.OpenTutorial(BasicTutorialSteps.EditingCollections);
    }

    public void DrawContent()
        => Draw(_config.CollectionsTabScale);

    protected override void SetWidth(float width, ScalingMode mode)
        => _config.CollectionsTabScale = new TwoPanelWidth(width, mode);

    public void PostTabButton()
        => _tutorial.OpenTutorial(BasicTutorialSteps.Collections);
}
