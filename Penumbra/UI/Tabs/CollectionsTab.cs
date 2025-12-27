using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.UI.Classes;
using Penumbra.UI.CollectionTab;

namespace Penumbra.UI.Tabs;

public sealed class CollectionsTab : TwoPanelLayout, ITab<TabType>
{
    private readonly TutorialService _tutorial;

    public TabType Identifier
        => TabType.Collections;

    public CollectionsTab(TutorialService tutorial, CollectionButtonFooter leftFooter, CollectionSelector leftPanel, CollectionFilter filter,
        CollectionModeHeader rightHeader, CollectionPanel rightPanel)
    {
        LeftHeader  = new FilterHeader<CollectionSelector.Entry>(filter, new StringU8("Filter..."u8));
        LeftPanel   = leftPanel;
        LeftFooter  = leftFooter;
        RightHeader = rightHeader;
        RightPanel  = rightPanel;
        RightFooter = NopHeaderFooter.Instance;
        _tutorial   = tutorial;
    }

    public override ReadOnlySpan<byte> Label
        => "Collections"u8;

    protected override void DrawLeftGroup()
    {
        base.DrawLeftGroup();
        _tutorial.OpenTutorial(BasicTutorialSteps.EditingCollections);
    }

    public void DrawContent()
        => Draw();

    public void PostTabButton()
        => _tutorial.OpenTutorial(BasicTutorialSteps.Collections);
}
