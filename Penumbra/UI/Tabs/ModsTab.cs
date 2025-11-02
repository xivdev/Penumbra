using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects;
using OtterGui;
using OtterGui.Raii;
using Penumbra.UI.Classes;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImSharp;
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
    : ITab, Luna.IUiService
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
            selector.Draw();
            Im.Line.Same();
            ImGui.SetCursorPosX(MathF.Round(ImGui.GetCursorPosX()));
            using var group = Im.Group();
            collectionHeader.Draw(false);

            using var style = ImStyleDouble.ItemSpacing.Push(Vector2.Zero);
            using (var child = ImRaii.Child("##ModsTabMod", Im.ContentRegion.Available with { Y = config.HideRedrawBar ? 0 : -Im.Style.FrameHeight },
                       true, ImGuiWindowFlags.HorizontalScrollbar))
            {
                style.Pop();
                if (child)
                    panel.Draw();

                style.Push(ImStyleDouble.ItemSpacing, Vector2.Zero);
            }

            style.Push(ImStyleSingle.FrameRounding, 0);
            DrawRedrawLine();
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Exception thrown during ModPanel Render:\n{e}");
            Penumbra.Log.Error($"{modManager.Count} Mods\n"
              + $"{_activeCollections.Current.Identity.AnonymizedName} Current Collection\n"
              + $"{_activeCollections.Current.Settings.Count} Settings\n"
              + $"{selector.SortMode.Name} Sort Mode\n"
              + $"{selector.SelectedLeaf?.Name ?? "NULL"} Selected Leaf\n"
              + $"{selector.Selected?.Name ?? "NULL"} Selected Mod\n"
              + $"{string.Join(", ", _activeCollections.Current.Inheritance.DirectlyInheritsFrom.Select(c => c.Identity.AnonymizedName))} Inheritances\n");
        }
    }

    private void DrawRedrawLine()
    {
        if (config.HideRedrawBar)
        {
            tutorial.SkipTutorial(BasicTutorialSteps.Redrawing);
            return;
        }

        var frameHeight = new Vector2(0, Im.Style.FrameHeight);
        var frameColor  = ImGuiColor.FrameBackground.Get().Color;
        using (Im.Group())
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                ImGuiUtil.DrawTextButton(FontAwesomeIcon.InfoCircle.ToIconString(), frameHeight, frameColor);
                Im.Line.Same();
            }

            ImGuiUtil.DrawTextButton("Redraw:        ", frameHeight, frameColor);
        }

        var hovered = Im.Item.Hovered();
        tutorial.OpenTutorial(BasicTutorialSteps.Redrawing);
        if (hovered)
            ImGui.SetTooltip($"The supported modifiers for '/penumbra redraw' are:\n{TutorialService.SupportedRedrawModifiers}");

        using var id       = ImRaii.PushId("Redraw");
        using var disabled = ImRaii.Disabled(clientState.LocalPlayer == null);
        Im.Line.Same();
        var buttonWidth = frameHeight with { X = Im.ContentRegion.Available.X / 5 };
        var tt = !objects[0].Valid
            ? "\nCan only be used when you are logged in and your character is available."
            : string.Empty;
        DrawButton(buttonWidth, "All", string.Empty, tt);
        Im.Line.Same();
        DrawButton(buttonWidth, "Self", "self", tt);
        Im.Line.Same();

        tt = targets.Target == null && targets.GPoseTarget == null
            ? "\nCan only be used when you have a target."
            : string.Empty;
        DrawButton(buttonWidth, "Target", "target", tt);
        Im.Line.Same();

        tt = targets.FocusTarget == null
            ? "\nCan only be used when you have a focus target."
            : string.Empty;
        DrawButton(buttonWidth, "Focus", "focus", tt);
        Im.Line.Same();

        tt = !IsIndoors()
            ? "\nCan currently only be used for indoor furniture."
            : string.Empty;
        DrawButton(frameHeight with { X = Im.ContentRegion.Available.X - 1 }, "Furniture", "furniture", tt);
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

            Im.Tooltip.OnHover(lower.Length > 0
                ? $"Execute '/penumbra redraw {lower}'.{additionalTooltip}"
                : $"Execute '/penumbra redraw'.{additionalTooltip}", HoveredFlags.AllowWhenDisabled);
        }
    }

    private static unsafe bool IsIndoors()
        => HousingManager.Instance()->IsInside();
}
