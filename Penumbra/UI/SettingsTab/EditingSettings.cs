using ImSharp;
using Luna;

namespace Penumbra.UI;

public sealed class EditingSettings(EditingConfig config) : IUiService
{
    public void Draw()
    {
        using var header = Im.Tree.HeaderId("File Editing"u8);
        if (!header)
            return;

        DrawGeneralEditing();
        DrawMaterialEditing();
    }

    private void DrawGeneralEditing()
    {
        using var tree = Im.Tree.Node("General"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("Automatically Pin Mod in Editing Window"u8,
                "Determines the default pinning behavior when opening a new Advanced Editing window.\n\nPinned: The editing window will stay on the mod it was on at the time of opening/pinning.\nUnpinned: When changing your selected mod in the main window, the editing window will follow the selection, unless a pinned window exists for the new selected mod."u8,
                config.DefaultEditWindowModPinned))
            config.DefaultEditWindowModPinned ^= true;
    }


    private void DrawMaterialEditing()
    {
        using var tree = Im.Tree.Node("Materials"u8, TreeNodeFlags.DefaultOpen);
        if (!tree)
            return;

        if (SettingsTab.Checkbox("Edit Raw Tile UV Transforms"u8,
                "Edit the raw matrix components of tile UV transforms, instead of having them decomposed into scale, rotation and shear."u8,
                config.EditRawTileTransforms))
            config.EditRawTileTransforms ^= true;

        if (SettingsTab.Checkbox("Always Highlight Color Row Pair when Hovering Selection Button"u8,
                "Make the whole color row pair selection button highlight the pair in game, instead of just the crosshair, even without holding Control."u8,
                config.WholePairSelectorAlwaysHighlights))
            config.WholePairSelectorAlwaysHighlights ^= true;

        if (SettingsTab.Checkbox("Unlock More Dye Chanels"u8,
                "Although the vanilla game is limited to two dye channels, the current material file format supports four.\nThis option will allow the use of those four dye channels in the material editor.\nPlease note, though, that this has limited usefulness: at the time of writing, those four channels are only usable within the material editor."u8,
                config.AllDyeChannels))
            config.AllDyeChannels ^= true;
    }
}
