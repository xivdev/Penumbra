using ImSharp;
using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.UI.AdvancedWindow;
using ImGuiColor = ImSharp.ImGuiColor;

namespace Penumbra.UI.ModsTab;

public enum ModPanelTab
{
    Description,
    Settings,
    ChangedItems,
    Conflicts,
    Collections,
    Edit,
};

public class ModPanelTabBar : TabBar<ModPanelTab>
{
    public readonly  ModPanelSettingsTab Settings;
    public readonly  ModPanelEditTab     Edit;
    private readonly ModManager          _modManager;
    private readonly TutorialService     _tutorial;

    private Mod? _lastMod;

    public ModPanelTabBar(ModEditWindowFactory modEditWindowFactory, ModPanelSettingsTab settings, ModPanelDescriptionTab description,
        ModPanelConflictsTab conflicts, ModPanelChangedItemsTab changedItems, ModPanelEditTab edit, ModManager modManager,
        TutorialService tutorial, ModPanelCollectionsTab collections, Logger log)
        : base(nameof(ModPanelTabBar), log, settings, description, conflicts, changedItems, collections, edit)
    {
        Flags       = TabBarFlags.NoTooltip;
        Settings    = settings;
        Edit        = edit;
        _modManager = modManager;
        _tutorial   = tutorial;
        Buttons.AddButton(new AdvancedEditingButton(this, modEditWindowFactory), 0);
    }

    private sealed class AdvancedEditingButton(ModPanelTabBar parent, ModEditWindowFactory editFactory) : BaseButton
    {
        public override ReadOnlySpan<byte> Label
            => "Advanced Editing"u8;

        public override void OnClick()
        {
            if (parent._lastMod is { } mod)
                editFactory.OpenForMod(mod);
        }

        public override bool HasTooltip
            => true;

        public override void DrawTooltip()
            => Im.Text(
                "Clicking this will open a new window in which you can\nedit the following things per option for this mod:\n\n"u8
              + "\t\t- file redirections\n"u8
              + "\t\t- file swaps\n"u8
              + "\t\t- metadata manipulations\n"u8
              + "\t\t- model materials\n"u8
              + "\t\t- duplicates\n"u8
              + "\t\t- textures"u8);
    }

    public void Draw(Mod mod)
    {
        var tabBarHeight = Im.Cursor.Y;
        _lastMod = mod;
        base.Draw();

        DrawFavoriteButton(mod, tabBarHeight);
    }

    private void DrawFavoriteButton(Mod mod, float height)
    {
        var size   = ImEx.Icon.CalculateSize(LunaStyle.FavoriteIcon) + Im.Style.FramePadding * 2;
        var newPos = new Vector2(Im.Window.Width - size.X - Im.Style.ItemSpacing.X, height);
        if (Im.Scroll.MaximumX > 0)
            newPos.X += Im.Scroll.X;

        var rectUpper = Im.Window.Position + newPos;
        var color = Im.Mouse.IsHoveringRectangle(rectUpper, rectUpper + size) ? Im.Style[ImGuiColor.Text] :
            mod.Favorite                                                      ? LunaStyle.FavoriteColor : Im.Style[ImGuiColor.TextDisabled];
        using var c = ImGuiColor.Text.Push(color)
            .Push(ImGuiColor.Button,        Vector4.Zero)
            .Push(ImGuiColor.ButtonHovered, Vector4.Zero)
            .Push(ImGuiColor.ButtonActive,  Vector4.Zero);

        Im.Cursor.Position = newPos;
        if (ImEx.Icon.Button(LunaStyle.FavoriteIcon))
            _modManager.DataEditor.ChangeModFavorite(mod, !mod.Favorite);

        var hovered = Im.Item.Hovered();
        _tutorial.OpenTutorial(BasicTutorialSteps.Favorites);

        if (hovered)
            Im.Tooltip.Set("Favorite"u8);
    }
}
