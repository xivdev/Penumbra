using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.GameData.Interop;
using Penumbra.Interop.Services;
using Penumbra.UI.Classes;

namespace Penumbra.UI.ModsTab;

public sealed class RedrawFooter(
    Configuration config,
    TutorialService tutorial,
    ObjectManager objects,
    ITargetManager targets,
    RedrawService redrawService) : IFooter
{
    public bool Collapsed
        => config.HideRedrawBar;

    public void PostCollapsed()
        => tutorial.SkipTutorial(BasicTutorialSteps.Redrawing);

    private void DrawTooltip()
    {
        var hovered = Im.Item.Hovered();
        tutorial.OpenTutorial(BasicTutorialSteps.Redrawing);
        if (!hovered)
            return;

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

    private static void DrawInfo(Vector2 height)
    {
        var       frameColor = Im.Style[ImGuiColor.FrameBackground];
        using var group      = Im.Group();
        using (AwesomeIcon.Font.Push())
        {
            ImEx.TextFramed(LunaStyle.HelpMarker.Span, height, frameColor);
        }

        Im.Line.NoSpacing();
        ImEx.TextFramed("Redraw:        "u8, height, frameColor);
    }

    public void Draw(Vector2 size)
    {
        using var style = Im.Style.PushDefault(ImStyleDouble.FramePadding);
        DrawInfo(size with { X = 0 });
        DrawTooltip();

        using var id       = Im.Id.Push("Redraw"u8);
        using var disabled = Im.Disabled(!objects[0].Valid);
        Im.Line.NoSpacing();
        var buttonWidth = size with { X = Im.ContentRegion.Available.X / 5 };
        var tt = !objects[0].Valid
            ? "Can only be used when you are logged in and your character is available."u8
            : StringU8.Empty;
        DrawButton(buttonWidth, "All"u8, string.Empty, tt);
        Im.Line.NoSpacing();
        DrawButton(buttonWidth, "Self"u8, "self", tt);
        Im.Line.NoSpacing();

        tt = targets.Target is null && targets.GPoseTarget is null
            ? "Can only be used when you have a target."u8
            : StringU8.Empty;
        DrawButton(buttonWidth, "Target"u8, "target", tt);
        Im.Line.NoSpacing();

        tt = targets.FocusTarget is null
            ? "Can only be used when you have a focus target."u8
            : StringU8.Empty;
        DrawButton(buttonWidth, "Focus"u8, "focus", tt);
        Im.Line.NoSpacing();

        tt = !IsIndoors()
            ? "Can currently only be used for indoor furniture."u8
            : StringU8.Empty;
        DrawButton(buttonWidth, "Furniture"u8, "furniture", tt);
    }

    private void DrawButton(Vector2 width, ReadOnlySpan<byte> label, string lower, ReadOnlySpan<byte> additionalTooltip)
    {
        using (Im.Disabled(additionalTooltip.Length > 0))
        {
            if (Im.Button(label, width))
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

    private static unsafe bool IsIndoors()
        => HousingManager.Instance()->IsInside();
}
