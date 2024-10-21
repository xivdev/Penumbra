using Dalamud.Game.ClientState.Objects;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.UI.Classes;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using OtterGui.Services;
using OtterGui.Widgets;
using Penumbra.Api.Enums;
using Penumbra.Interop.Services;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.UI.ModsTab;
using ModFileSystemSelector = Penumbra.UI.ModsTab.ModFileSystemSelector;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Interop;

namespace Penumbra.UI.Tabs;

public class ModsTab(
    ModManager modManager,
    CollectionManager collectionManager,
    ModFileSystemSelector selector,
    ModPanel panel,
    TutorialService tutorial,
    RedrawService redrawService,
    Configuration config,
    IClientState clientState,
    CollectionSelectHeader collectionHeader,
    ITargetManager targets,
    ObjectManager objects)
    : ITab, IUiService
{
    private readonly ActiveCollections _activeCollections = collectionManager.Active;

    public bool IsVisible
        => modManager.Valid;

    public ReadOnlySpan<byte> Label
        => "Mods"u8;

    public void DrawHeader()
        => tutorial.OpenTutorial(BasicTutorialSteps.Mods);

    public Mod SelectMod
    {
        set => selector.SelectByValue(value);
    }

    public void DrawContent()
    {
        try
        {
            selector.Draw(GetModSelectorSize(config));
            ImGui.SameLine();
            using var group = ImRaii.Group();
            collectionHeader.Draw(false);

            using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

            using (var child = ImRaii.Child("##ModsTabMod", new Vector2(-1, config.HideRedrawBar ? 0 : -ImGui.GetFrameHeight()),
                       true, ImGuiWindowFlags.HorizontalScrollbar))
            {
                style.Pop();
                if (child)
                    panel.Draw();

                style.Push(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            }

            style.Push(ImGuiStyleVar.FrameRounding, 0);
            DrawRedrawLine();
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Exception thrown during ModPanel Render:\n{e}");
            Penumbra.Log.Error($"{modManager.Count} Mods\n"
              + $"{_activeCollections.Current.AnonymizedName} Current Collection\n"
              + $"{_activeCollections.Current.Settings.Count} Settings\n"
              + $"{selector.SortMode.Name} Sort Mode\n"
              + $"{selector.SelectedLeaf?.Name ?? "NULL"} Selected Leaf\n"
              + $"{selector.Selected?.Name ?? "NULL"} Selected Mod\n"
              + $"{string.Join(", ", _activeCollections.Current.DirectlyInheritsFrom.Select(c => c.AnonymizedName))} Inheritances\n");
        }
    }

    /// <summary> Get the correct size for the mod selector based on current config. </summary>
    public static float GetModSelectorSize(Configuration config)
    {
        var absoluteSize = Math.Clamp(config.ModSelectorAbsoluteSize, Configuration.Constants.MinAbsoluteSize,
            Math.Min(Configuration.Constants.MaxAbsoluteSize, ImGui.GetContentRegionAvail().X - 100));
        var relativeSize = config.ScaleModSelector
            ? Math.Clamp(config.ModSelectorScaledSize, Configuration.Constants.MinScaledSize, Configuration.Constants.MaxScaledSize)
            : 0;
        return !config.ScaleModSelector
            ? absoluteSize
            : Math.Max(absoluteSize, relativeSize * ImGui.GetContentRegionAvail().X / 100);
    }

    private void DrawRedrawLine()
    {
        if (config.HideRedrawBar)
        {
            tutorial.SkipTutorial(BasicTutorialSteps.Redrawing);
            return;
        }

        var frameHeight = new Vector2(0, ImGui.GetFrameHeight());
        var frameColor  = ImGui.GetColorU32(ImGuiCol.FrameBg);
        using (var _ = ImRaii.Group())
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGuiUtil.DrawTextButton(FontAwesomeIcon.InfoCircle.ToIconString(), frameHeight, frameColor);
                ImGui.SameLine();
            }

            ImGuiUtil.DrawTextButton("Redraw:        ", frameHeight, frameColor);
        }

        var hovered = ImGui.IsItemHovered();
        tutorial.OpenTutorial(BasicTutorialSteps.Redrawing);
        if (hovered)
            ImGui.SetTooltip($"The supported modifiers for '/penumbra redraw' are:\n{TutorialService.SupportedRedrawModifiers}");

        using var id       = ImRaii.PushId("Redraw");
        using var disabled = ImRaii.Disabled(clientState.LocalPlayer == null);
        ImGui.SameLine();
        var buttonWidth = frameHeight with { X = ImGui.GetContentRegionAvail().X / 5 };
        var tt = !objects[0].Valid
            ? "\nCan only be used when you are logged in and your character is available."
            : string.Empty;
        DrawButton(buttonWidth, "All", string.Empty, tt);
        ImGui.SameLine();
        DrawButton(buttonWidth, "Self", "self", tt);
        ImGui.SameLine();

        tt = targets.Target == null && targets.GPoseTarget == null
            ? "\nCan only be used when you have a target."
            : string.Empty;
        DrawButton(buttonWidth, "Target", "target", tt);
        ImGui.SameLine();

        tt = targets.FocusTarget == null
            ? "\nCan only be used when you have a focus target."
            : string.Empty;
        DrawButton(buttonWidth, "Focus", "focus", tt);
        ImGui.SameLine();

        tt = !IsIndoors()
            ? "\nCan currently only be used for indoor furniture."
            : string.Empty;
        DrawButton(frameHeight with { X = ImGui.GetContentRegionAvail().X - 1 }, "Furniture", "furniture", tt);
        return;

        void DrawButton(Vector2 size, string label, string lower, string additionalTooltip)
        {
            using (_ = ImRaii.Disabled(additionalTooltip.Length > 0))
            {
                if (ImGui.Button(label, size))
                {
                    if (lower.Length > 0)
                        redrawService.RedrawObject(lower, RedrawType.Redraw);
                    else
                        redrawService.RedrawAll(RedrawType.Redraw);
                }
            }

            ImGuiUtil.HoverTooltip(lower.Length > 0
                ? $"Execute '/penumbra redraw {lower}'.{additionalTooltip}"
                : $"Execute '/penumbra redraw'.{additionalTooltip}", ImGuiHoveredFlags.AllowWhenDisabled);
        }
    }

    private static unsafe bool IsIndoors()
        => HousingManager.Instance()->IsInside();
}
