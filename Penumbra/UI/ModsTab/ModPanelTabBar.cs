using System;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.UI.AdvancedWindow;

namespace Penumbra.UI.ModsTab;

public class ModPanelTabBar
{
    private enum ModPanelTabType
    {
        Description,
        Settings,
        ChangedItems,
        Conflicts,
        Edit,
    };

    public readonly  ModPanelSettingsTab     Settings;
    public readonly  ModPanelDescriptionTab  Description;
    public readonly  ModPanelConflictsTab    Conflicts;
    public readonly  ModPanelChangedItemsTab ChangedItems;
    public readonly  ModPanelEditTab         Edit;
    private readonly ModEditWindow           _modEditWindow;
    private readonly ModManager             _modManager;
    private readonly TutorialService         _tutorial;

    public readonly ITab[]          Tabs;
    private         ModPanelTabType _preferredTab = ModPanelTabType.Settings;
    private         Mod?            _lastMod      = null;

    public ModPanelTabBar(ModEditWindow modEditWindow, ModPanelSettingsTab settings, ModPanelDescriptionTab description,
        ModPanelConflictsTab conflicts, ModPanelChangedItemsTab changedItems, ModPanelEditTab edit, ModManager modManager,
        TutorialService tutorial)
    {
        _modEditWindow = modEditWindow;
        Settings       = settings;
        Description    = description;
        Conflicts      = conflicts;
        ChangedItems   = changedItems;
        Edit           = edit;
        _modManager    = modManager;
        _tutorial      = tutorial;

        Tabs = new ITab[]
        {
            Settings,
            Description,
            Conflicts,
            ChangedItems,
            Edit,
        };
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
        if (label == Edit.Label)
            return ModPanelTabType.Edit;

        return 0;
    }

    private void DrawAdvancedEditingButton(Mod mod)
    {
        if (ImGui.TabItemButton("Advanced Editing", ImGuiTabItemFlags.Trailing | ImGuiTabItemFlags.NoTooltip))
        {
            _modEditWindow.ChangeMod(mod);
            _modEditWindow.ChangeOption((SubMod) mod.Default);
            _modEditWindow.IsOpen = true;
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
        using (var font = ImRaii.PushFont(UiBuilder.IconFont))
        {
            var size   = ImGui.CalcTextSize(FontAwesomeIcon.Star.ToIconString()) + ImGui.GetStyle().FramePadding * 2;
            var newPos = new Vector2(ImGui.GetWindowWidth() - size.X - ImGui.GetStyle().ItemSpacing.X, height);
            if (ImGui.GetScrollMaxX() > 0)
                newPos.X += ImGui.GetScrollX();

            var rectUpper = ImGui.GetWindowPos() + newPos;
            var color = ImGui.IsMouseHoveringRect(rectUpper, rectUpper + size) ? ImGui.GetColorU32(ImGuiCol.Text) :
                mod.Favorite                                                   ? 0xFF00FFFF : ImGui.GetColorU32(ImGuiCol.TextDisabled);
            using var c = ImRaii.PushColor(ImGuiCol.Text, color)
                .Push(ImGuiCol.Button,        0)
                .Push(ImGuiCol.ButtonHovered, 0)
                .Push(ImGuiCol.ButtonActive,  0);

            ImGui.SetCursorPos(newPos);
            if (ImGui.Button(FontAwesomeIcon.Star.ToIconString()))
                _modManager.DataEditor.ChangeModFavorite(mod, !mod.Favorite);
        }

        var hovered = ImGui.IsItemHovered();
        _tutorial.OpenTutorial(BasicTutorialSteps.Favorites);

        if (hovered)
            ImGui.SetTooltip("Favorite");
    }
}
