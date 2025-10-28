using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ImSharp;
using Luna;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.UI.AdvancedWindow;
using ImGuiColor = ImSharp.ImGuiColor;

namespace Penumbra.UI.ModsTab;

public class ModPanelTabBar : IUiService
{
    private enum ModPanelTabType
    {
        Description,
        Settings,
        ChangedItems,
        Conflicts,
        Collections,
        Edit,
    };

    public readonly  ModPanelSettingsTab     Settings;
    public readonly  ModPanelDescriptionTab  Description;
    public readonly  ModPanelCollectionsTab  Collections;
    public readonly  ModPanelConflictsTab    Conflicts;
    public readonly  ModPanelChangedItemsTab ChangedItems;
    public readonly  ModPanelEditTab         Edit;
    private readonly ModEditWindowFactory    _modEditWindowFactory;
    private readonly ModManager              _modManager;
    private readonly TutorialService         _tutorial;

    public readonly ITab[]          Tabs;
    private         ModPanelTabType _preferredTab = ModPanelTabType.Settings;
    private         Mod?            _lastMod;

    public ModPanelTabBar(ModEditWindowFactory modEditWindowFactory, ModPanelSettingsTab settings, ModPanelDescriptionTab description,
        ModPanelConflictsTab conflicts, ModPanelChangedItemsTab changedItems, ModPanelEditTab edit, ModManager modManager,
        TutorialService tutorial, ModPanelCollectionsTab collections)
    {
        _modEditWindowFactory = modEditWindowFactory;
        Settings              = settings;
        Description           = description;
        Conflicts             = conflicts;
        ChangedItems          = changedItems;
        Edit                  = edit;
        _modManager           = modManager;
        _tutorial             = tutorial;
        Collections           = collections;

        Tabs =
        [
            Settings,
            Description,
            Conflicts,
            ChangedItems,
            Collections,
            Edit,
        ];
    }

    public void Draw(Mod mod)
    {
        var tabBarHeight = ImGui.GetCursorPosY();
        if (_lastMod != mod)
        {
            _lastMod = mod;
            TabBar.Draw(string.Empty, ImGuiTabBarFlags.NoTooltip, ToLabel(_preferredTab), out _, () => DrawAdvancedEditingButton(mod), Tabs);
        }
        else
        {
            TabBar.Draw(string.Empty, ImGuiTabBarFlags.NoTooltip, ReadOnlySpan<byte>.Empty, out var label, () => DrawAdvancedEditingButton(mod),
                Tabs);
            _preferredTab = ToType(label);
        }

        DrawFavoriteButton(mod, tabBarHeight);
    }

    private ReadOnlySpan<byte> ToLabel(ModPanelTabType type)
        => type switch
        {
            ModPanelTabType.Description  => Description.Label,
            ModPanelTabType.Settings     => Settings.Label,
            ModPanelTabType.ChangedItems => ChangedItems.Label,
            ModPanelTabType.Conflicts    => Conflicts.Label,
            ModPanelTabType.Collections  => Collections.Label,
            ModPanelTabType.Edit         => Edit.Label,
            _                            => ReadOnlySpan<byte>.Empty,
        };

    private ModPanelTabType ToType(ReadOnlySpan<byte> label)
    {
        if (label == Description.Label)
            return ModPanelTabType.Description;
        if (label == Settings.Label)
            return ModPanelTabType.Settings;
        if (label == ChangedItems.Label)
            return ModPanelTabType.ChangedItems;
        if (label == Conflicts.Label)
            return ModPanelTabType.Conflicts;
        if (label == Collections.Label)
            return ModPanelTabType.Collections;
        if (label == Edit.Label)
            return ModPanelTabType.Edit;

        return 0;
    }

    private void DrawAdvancedEditingButton(Mod mod)
    {
        if (ImGui.TabItemButton("Advanced Editing", ImGuiTabItemFlags.Trailing | ImGuiTabItemFlags.NoTooltip))
        {
            _modEditWindowFactory.OpenForMod(mod);
        }

        ImGuiUtil.HoverTooltip(
            "Clicking this will open a new window in which you can\nedit the following things per option for this mod:\n\n"
          + "\t\t- file redirections\n"
          + "\t\t- file swaps\n"
          + "\t\t- metadata manipulations\n"
          + "\t\t- model materials\n"
          + "\t\t- duplicates\n"
          + "\t\t- textures");
    }

    private void DrawFavoriteButton(Mod mod, float height)
    {
        var size   = ImEx.Icon.CalculateSize(LunaStyle.FavoriteIcon) + ImGui.GetStyle().FramePadding * 2;
        var newPos = new Vector2(ImGui.GetWindowWidth() - size.X - ImGui.GetStyle().ItemSpacing.X, height);
        if (ImGui.GetScrollMaxX() > 0)
            newPos.X += ImGui.GetScrollX();

        var rectUpper = ImGui.GetWindowPos() + newPos;
        var color = ImGui.IsMouseHoveringRect(rectUpper, rectUpper + size) ? Im.Style[ImGuiColor.Text] :
            mod.Favorite                                                   ? LunaStyle.FavoriteColor : Im.Style[ImGuiColor.TextDisabled];
        using var c = ImGuiColor.Text.Push(color)
            .Push(ImGuiColor.Button,        Vector4.Zero)
            .Push(ImGuiColor.ButtonHovered, Vector4.Zero)
            .Push(ImGuiColor.ButtonActive,  Vector4.Zero);

        ImGui.SetCursorPos(newPos);
        if (ImEx.Icon.Button(LunaStyle.FavoriteIcon))
            _modManager.DataEditor.ChangeModFavorite(mod, !mod.Favorite);

        var hovered = Im.Item.Hovered();
        _tutorial.OpenTutorial(BasicTutorialSteps.Favorites);

        if (hovered)
            ImGui.SetTooltip("Favorite");
    }
}
