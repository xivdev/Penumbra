using Dalamud.Game.ClientState.Objects;
using Penumbra.UI.Classes;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Interop.Services;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.UI.ModsTab;
using ModFileSystemSelector = Penumbra.UI.ModsTab.ModFileSystemSelector;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Interop;

namespace Penumbra.UI.Tabs;

public sealed class ModsTab(
    ModManager modManager,
    CollectionManager collectionManager,
    ModFileSystemSelector selector,
    ModPanel panel,
    TutorialService tutorial,
    RedrawService redrawService,
    Configuration config,
    CollectionSelectHeader collectionHeader,
    ITargetManager targets,
    ObjectManager objects)
    : ITab<TabType>
{
    private readonly ActiveCollections _activeCollections = collectionManager.Active;

    public bool IsEnabled
        => modManager.Valid;

    public ReadOnlySpan<byte> Label
        => "Mods"u8;

    public TabType Identifier
        => TabType.Mods;

    public void PostTabButton()
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
            Im.Cursor.X = MathF.Round(Im.Cursor.X);
            using var group = Im.Group();
            collectionHeader.Draw(false);

            using var style = ImStyleDouble.ItemSpacing.Push(Vector2.Zero);
            using (var child = Im.Child.Begin("##ModsTabMod"u8,
                       Im.ContentRegion.Available with { Y = config.HideRedrawBar ? 0 : -Im.Style.FrameHeight },
                       true, WindowFlags.HorizontalScrollbar))
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
        var frameColor  = Im.Style[ImGuiColor.FrameBackground];
        using (Im.Group())
        {
            using (AwesomeIcon.Font.Push())
            {
                ImEx.TextFramed(LunaStyle.HelpMarker.Span, frameHeight, frameColor);
            }

            Im.Line.Same();
            ImEx.TextFramed("Redraw:        "u8, frameHeight, frameColor);
        }

        var hovered = Im.Item.Hovered();
        tutorial.OpenTutorial(BasicTutorialSteps.Redrawing);
        if (hovered)
        {
            using var _ = Im.Tooltip.Begin();
            Im.Text("The supported modifiers for '/penumbra redraw' are:"u8);
            Im.BulletText("nothing, to redraw all characters\n"u8);
            Im.BulletText("'self' or '<me>': your own character\n"u8);
            Im.BulletText("'target' or '<t>': your target\n"u8);
            Im.BulletText("'focus' or '<f>: your focus target\n"u8);
            Im.BulletText("'mouseover' or '<mo>': the actor you are currently hovering over\n"u8);
            Im.BulletText("'furniture': most indoor furniture, does not currently work outdoors\n"u8);
            Im.BulletText("any specific actor name to redraw all actors of that exactly matching name."u8);
        }

        using var id       = Im.Id.Push("Redraw"u8);
        using var disabled = Im.Disabled(!objects[0].Valid);
        Im.Line.Same();
        var buttonWidth = frameHeight with { X = Im.ContentRegion.Available.X / 5 };
        var tt = !objects[0].Valid
            ? "Can only be used when you are logged in and your character is available."u8
            : StringU8.Empty;
        DrawButton(buttonWidth, "All"u8, string.Empty, tt);
        Im.Line.Same();
        DrawButton(buttonWidth, "Self"u8, "self", tt);
        Im.Line.Same();

        tt = targets.Target is null && targets.GPoseTarget is null
            ? "Can only be used when you have a target."u8
            : StringU8.Empty;
        DrawButton(buttonWidth, "Target"u8, "target", tt);
        Im.Line.Same();

        tt = targets.FocusTarget is null
            ? "Can only be used when you have a focus target."u8
            : StringU8.Empty;
        DrawButton(buttonWidth, "Focus"u8, "focus", tt);
        Im.Line.Same();

        tt = !IsIndoors()
            ? "Can currently only be used for indoor furniture."u8
            : StringU8.Empty;
        DrawButton(frameHeight with { X = Im.ContentRegion.Available.X - 1 }, "Furniture"u8, "furniture", tt);
        return;

        void DrawButton(Vector2 size, ReadOnlySpan<byte> label, string lower, ReadOnlySpan<byte> additionalTooltip)
        {
            using (Im.Disabled(additionalTooltip.Length > 0))
            {
                if (Im.Button(label, size))
                {
                    if (lower.Length > 0)
                        redrawService.RedrawObject(lower, RedrawType.Redraw);
                    else
                        redrawService.RedrawAll(RedrawType.Redraw);
                }
            }

            if (!Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
                return;

            using var _ = Im.Tooltip.Begin();
            if (lower.Length > 0)
                Im.Text($"Execute '/penumbra redraw {lower}'.");
            else
                Im.Text("Execute '/penumbra redraw'."u8);
            if (additionalTooltip.Length > 0)
                Im.Text(additionalTooltip);
        }
    }

    private static unsafe bool IsIndoors()
        => HousingManager.Instance()->IsInside();
}
