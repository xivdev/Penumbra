using Luna;

namespace Penumbra.UI.ModsTab.Selector;

/// <summary> The menu item to clear all temporary settings of the current collection. </summary>
/// <param name="drawer"> The file system drawer. </param>
public sealed class ClearTemporarySettingsButton(ModFileSystemDrawer drawer) : BaseButton
{
    /// <inheritdoc/>
    public override ReadOnlySpan<byte> Label
        => "Clear Temporary Settings"u8;

    /// <inheritdoc/>
    public override void OnClick()
        => drawer.CollectionManager.Editor.ClearTemporarySettings(drawer.CollectionManager.Active.Current);
}
