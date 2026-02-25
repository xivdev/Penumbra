using ImSharp;
using Luna;
using Penumbra.Mods;

namespace Penumbra.UI.ModsTab.Selector;

public sealed class DeleteSelectionButton(ModFileSystemDrawer drawer) : BaseIconButton<AwesomeIcon>
{
    /// <inheritdoc/>
    public override AwesomeIcon Icon
        => LunaStyle.DeleteIcon;

    /// <inheritdoc/>
    public override bool HasTooltip
        => true;

    /// <inheritdoc/>
    public override void DrawTooltip()
    {
        var anySelected = drawer.FileSystem.Selection.DataNodes.Count > 0;
        var modifier    = Enabled;

        Im.Text(anySelected ? "Delete the currently selected mods entirely from your drive\nThis can not be undone."u8 : "No mods selected."u8);
        if (!modifier)
            Im.Text($"Hold {drawer.Config.DeleteModModifier} while clicking to delete the mods.");
    }

    /// <inheritdoc/>
    public override bool Enabled
        => drawer.Config.DeleteModModifier.IsActive() && drawer.FileSystem.Selection.DataNodes.Count > 0;

    /// <inheritdoc/>
    public override void OnClick()
    {
        foreach (var node in drawer.FileSystem.Selection.DataNodes.ToArray())
        {
            if (node.GetValue<Mod>() is { } mod)
                drawer.ModManager.DeleteMod(mod);
        }
    }
}
