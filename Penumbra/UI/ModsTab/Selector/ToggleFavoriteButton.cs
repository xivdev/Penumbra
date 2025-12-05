using Luna;
using Penumbra.Mods;

namespace Penumbra.UI.ModsTab.Selector;

/// <summary> The menu item to set toggle a mod's favourite state. </summary>
/// <param name="drawer"> The file system drawer. </param>
public sealed class ToggleFavoriteButton(ModFileSystemDrawer drawer) : BaseButton<IFileSystemData>
{
    /// <inheritdoc/>
    public override ReadOnlySpan<byte> Label(in IFileSystemData data)
        => ((Mod)data.Value).Favorite ? "Remove Favorite"u8 : "Mark as Favorite"u8;

    /// <inheritdoc/>
    public override void OnClick(in IFileSystemData data)
        => drawer.ModManager.DataEditor.ChangeModFavorite((Mod)data.Value, !((Mod)data.Value).Favorite);
}
