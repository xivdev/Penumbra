using ImSharp;
using Luna;

namespace Penumbra.UI.ModsTab.Selector;

/// <summary> The button to add a new, empty mod. </summary>
/// <param name="drawer"> The file system drawer. </param>
public sealed class AddNewModButton(ModFileSystemDrawer drawer) : BaseIconButton<AwesomeIcon>
{
    /// <inheritdoc/>
    public override AwesomeIcon Icon
        => LunaStyle.AddObjectIcon;

    /// <inheritdoc/>
    public override bool HasTooltip
        => true;

    /// <inheritdoc/>
    public override void DrawTooltip()
        => Im.Text("Create a new, empty mod of a given name."u8);

    /// <inheritdoc/>
    public override void OnClick()
        => Im.Popup.Open("Create New Mod"u8);

    /// <inheritdoc/>
    protected override void PostDraw()
    {
        if (!InputPopup.OpenName("Create New Mod"u8, out var newModName))
            return;

        if (drawer.ModManager.Creator.CreateEmptyMod(drawer.ModManager.BasePath, newModName) is { } directory)
            drawer.ModManager.AddMod(directory, false);
    }
}

/// <summary> The button to import a mod. </summary>
/// <param name="drawer"> The file system drawer. </param>
public sealed class ImportModButton(ModFileSystemDrawer drawer) : BaseIconButton<AwesomeIcon>
{
    /// <inheritdoc/>
    public override AwesomeIcon Icon
        => LunaStyle.AddObjectIcon;

    /// <inheritdoc/>
    public override bool HasTooltip
        => true;

    /// <inheritdoc/>
    public override void DrawTooltip()
        => Im.Text("Create a new, empty mod of a given name."u8);

    /// <inheritdoc/>
    public override void OnClick()
        => Im.Popup.Open("Create New Mod"u8);

    /// <inheritdoc/>
    protected override void PostDraw()
    {
        if (!InputPopup.OpenName("Create New Mod"u8, out var newModName))
            return;

        if (drawer.ModManager.Creator.CreateEmptyMod(drawer.ModManager.BasePath, newModName) is { } directory)
            drawer.ModManager.AddMod(directory, false);
    }
}

/// <summary> The button to import a mod. </summary>
/// <param name="drawer"> The file system drawer. </param>
public sealed class DeleteSelectionButton(ModFileSystemDrawer drawer) : BaseIconButton<AwesomeIcon>
{
    /// <inheritdoc/>
    public override AwesomeIcon Icon
        => LunaStyle.AddObjectIcon;

    /// <inheritdoc/>
    public override bool HasTooltip
        => true;

    /// <inheritdoc/>
    public override void DrawTooltip()
        => Im.Text("Create a new, empty mod of a given name."u8);

    /// <inheritdoc/>
    public override void OnClick()
        => Im.Popup.Open("Create New Mod"u8);

    /// <inheritdoc/>
    protected override void PostDraw()
    {
        if (!InputPopup.OpenName("Create New Mod"u8, out var newModName))
            return;

        if (drawer.ModManager.Creator.CreateEmptyMod(drawer.ModManager.BasePath, newModName) is { } directory)
            drawer.ModManager.AddMod(directory, false);
    }
}
