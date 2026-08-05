using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.UI.Classes;
using Penumbra.UI.Integration;

namespace Penumbra.UI;

public sealed class SettingsTab(
    MainSettings main,
    BehaviorSettings behavior,
    UiSettings ui,
    IoSettings io,
    EditingSettings editing,
    AdvancedSettings advanced,
    Configuration config,
    TutorialService tutorial,
    PredefinedTagManager predefinedTagManager,
    MigrationSectionDrawer migrationDrawer,
    IntegrationSettingsRegistry integrationSettings,
    Penumbra penumbra)
    : ITab<TabType>
{
    public TabType Identifier
        => TabType.Settings;

    public ReadOnlySpan<byte> Label
        => "Settings"u8;

    public void PostTabButton()
    {
        tutorial.OpenTutorial(BasicTutorialSteps.Fin);
        tutorial.OpenTutorial(BasicTutorialSteps.Faq1);
        tutorial.OpenTutorial(BasicTutorialSteps.Faq2);
    }

    public void DrawContent()
    {
        using var child = Im.Child.Begin("##SettingsTab"u8, -Vector2.One);
        if (!child)
            return;

        main.DrawHeader();

        using (var header = Im.Tree.HeaderId("General"u8))
        {
            if (header)
            {
                main.DrawGeneralSettings();
                Im.Line.Spacing();
            }
        }

        using (var header = Im.Tree.HeaderId("Penumbra Behavior"u8))
        {
            if (header)
            {
                behavior.Draw();
                Im.Line.Spacing();
            }
        }

        using (var header = Im.Tree.HeaderId("User Interface"u8))
        {
            if (header)
            {
                ui.Draw();
                DrawColorSettings();
                DrawPredefinedTagsSection();
                Im.Line.Spacing();
            }
        }


        using (var header = Im.Tree.HeaderId("Mod Import/Export"u8))
        {
            if (header)
            {
                io.Draw();
                using (var node = Im.Tree.Node("Mod Migration"u8))
                {
                    if (node)
                        migrationDrawer.Draw();
                }

                Im.Line.Spacing();
            }
        }

        using (var header = Im.Tree.HeaderId("File Editing"u8))
        {
            if (header)
            {
                editing.Draw();
                Im.Line.Spacing();
            }
        }

        using (var header = Im.Tree.HeaderId("Advanced"u8))
        {
            if (header)
            {
                advanced.Draw();
                Im.Line.Spacing();
            }
        }

        integrationSettings.Draw();
        DrawSupportButtons();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool Checkbox(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, bool value)
    {
        using var id  = Im.Id.Push(label);
        var       ret = Im.Checkbox(StringU8.Empty, value);
        LunaStyle.DrawAlignedHelpMarkerLabel(label, tooltip);
        return ret;
    }

    /// <summary> Draw the entire Color subsection. </summary>
    private void DrawColorSettings()
    {
        using var header = Im.Tree.Node("Colors"u8);
        if (!header)
            return;

        if (!ColorSettingsDrawer.Draw(Penumbra.Messager, config.Ui.Colors, config.Ui.ColorCache))
            return;

        CacheManager.Instance.SetColorsDirty();
        config.Ui.Save();
    }

    /// <summary> Draw the support button group on the right-hand side of the window. </summary>
    private void DrawSupportButtons()
    {
        var width = Im.Font.CalculateSize(UiHelpers.SupportInfoButtonText).X + Im.Style.FramePadding.X * 2;
        var xPos  = Im.Window.Width - width;
        // Respect the scroll bar width.
        if (Im.Scroll.MaximumY > 0)
            xPos -= Im.Style.ScrollbarSize + Im.Style.FramePadding.X;

        Im.Cursor.Position = new Vector2(xPos, Im.Style.FrameHeightWithSpacing);
        UiHelpers.DrawSupportButton(penumbra);

        Im.Cursor.Position = new Vector2(xPos, 0);
        SupportButton.Discord(Penumbra.Messager, width);

        Im.Cursor.Position = new Vector2(xPos, 2 * Im.Style.FrameHeightWithSpacing);
        SupportButton.ReniGuide(Penumbra.Messager, width);

        Im.Cursor.Position = new Vector2(xPos, 3 * Im.Style.FrameHeightWithSpacing);
        if (Im.Button("Restart Tutorial"u8, new Vector2(width, 0)))
        {
            config.Ephemeral.TutorialStep = 0;
            config.Ephemeral.Save();
        }

        Im.Cursor.Position = new Vector2(xPos, 4 * Im.Style.FrameHeightWithSpacing);
        if (Im.Button("Show Changelogs"u8, new Vector2(width, 0)))
            penumbra.ForceChangelogOpen();

        Im.Cursor.Position = new Vector2(xPos, 5 * Im.Style.FrameHeightWithSpacing);
        SupportButton.KoFiPatreon(Penumbra.Messager, new Vector2(width, 0));
    }

    private void DrawPredefinedTagsSection()
    {
        using var node = Im.Tree.Node("Tagging"u8, TreeNodeFlags.DefaultOpen);
        if (!node)
            return;

        var tagIdx = TagButtons.Draw("Predefined Tags: "u8,
            "Predefined tags that can be added or removed from mods with a single click."u8, predefinedTagManager,
            out var editedTag);

        if (tagIdx >= 0)
            predefinedTagManager.ChangeSharedTag(tagIdx, editedTag);
    }
}
