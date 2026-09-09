using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.UI.Classes;
using Penumbra.UI.ModsTab.Selector;

namespace Penumbra.UI.ModsTab;

public sealed class ModTab : TwoPanelLayout, ITab<TabType>
{
    private readonly EphemeralConfig _config;

    public override ReadOnlySpan<byte> Label
        => "Mods"u8;

    public ModTab(ModFileSystemDrawer drawer, ModPanel panel, CollectionSelectHeader collectionHeader, RedrawFooter redrawFooter,
        EphemeralConfig config)
    {
        _config     = config;
        LeftHeader  = drawer.Header;
        LeftFooter  = drawer.Footer;
        LeftPanel   = drawer;
        RightPanel  = panel;
        RightHeader = collectionHeader;
        RightFooter = redrawFooter;
    }

    public void DrawContent()
        => Draw(_config.ModTabScale);

    public TabType Identifier
        => TabType.Mods;

    protected override void SetWidth(float width, ScalingMode mode)
        => _config.ModTabScale = new TwoPanelWidth(width, mode);

    protected override float MinimumWidth
        => LeftFooter.MinimumWidth;

    protected override float MaximumWidth
        => Im.Window.Width - 500 * Im.Style.GlobalScale;
}
