using Luna;

namespace Penumbra.UI.ModsTab.Selector;

/// <summary> The menu item to clear the default import folder. </summary>
/// <param name="drawer"> The file system drawer. </param>
public sealed class ClearDefaultImportFolderButton(ModFileSystemDrawer drawer) : BaseButton
{
    /// <inheritdoc/>
    public override ReadOnlySpan<byte> Label
        => "Clear Default Import Folder"u8;

    /// <inheritdoc/>
    public override void OnClick()
    {
        if (drawer.Config.DefaultImportFolder.Length is 0)
            return;

        drawer.Config.DefaultImportFolder = string.Empty;
        drawer.Config.Save();
    }
}
