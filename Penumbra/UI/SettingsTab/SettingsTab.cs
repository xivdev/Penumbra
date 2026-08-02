using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Interop.Services;
using Penumbra.Mods.Manager;
using Penumbra.Services;
using Penumbra.UI.Classes;
using Penumbra.UI.Integration;
using Penumbra.UI.ModsTab;
using Penumbra.UI.ModsTab.Selector;

namespace Penumbra.UI;

public sealed class SettingsTab : ITab<TabType>
{
    public TabType Identifier
        => TabType.Settings;

    public ReadOnlySpan<byte> Label
        => "Settings"u8;

    private readonly Configuration               _config;
    private readonly TutorialService             _tutorial;
    private readonly Penumbra                    _penumbra;
    private readonly FileDialogService           _fileDialog;
    private readonly ModManager                  _modManager;
    private readonly FileWatcher                 _fileWatcher;
    private readonly ModExportManager            _modExportManager;
    private readonly IDalamudPluginInterface     _pluginInterface;
    private readonly PredefinedTagManager        _predefinedTagManager;
    private readonly MigrationSectionDrawer      _migrationDrawer;
    private readonly PcpService                  _pcpService;
    private readonly IntegrationSettingsRegistry _integrationSettings;
    private readonly ModFileSystemDrawer         _modFileSystemDrawer;


    public SettingsTab(IDalamudPluginInterface pluginInterface, Configuration config, FontReloader fontReloader, TutorialService tutorial,
        Penumbra penumbra, FileDialogService fileDialog, ModManager modManager, CharacterUtility characterUtility,
        ResidentResourceManager residentResources, ModExportManager modExportManager,
        FileWatcher fileWatcher, HttpApi httpApi,
        DalamudSubstitutionProvider dalamudSubstitutionProvider, FileCompactor compactor, DalamudConfigService dalamudConfig,
        IDataManager gameData, PredefinedTagManager predefinedTagConfig, CrashHandlerService crashService,
        MigrationSectionDrawer migrationDrawer, CollectionAutoSelector autoSelector, AttributeHook attributeHook, PcpService pcpService,
        IntegrationSettingsRegistry integrationSettings, ModFileSystemDrawer modFileSystemDrawer)
    {
        _pluginInterface      = pluginInterface;
        _config               = config;
        _tutorial             = tutorial;
        _penumbra             = penumbra;
        _fileDialog           = fileDialog;
        _modManager           = modManager;
        _modExportManager     = modExportManager;
        _fileWatcher          = fileWatcher;
        _predefinedTagManager = predefinedTagConfig;
        _migrationDrawer      = migrationDrawer;
        _pcpService           = pcpService;
        _integrationSettings  = integrationSettings;
        _modFileSystemDrawer  = modFileSystemDrawer;
    }

    public void PostTabButton()
    {
        _tutorial.OpenTutorial(BasicTutorialSteps.Fin);
        _tutorial.OpenTutorial(BasicTutorialSteps.Faq1);
        _tutorial.OpenTutorial(BasicTutorialSteps.Faq2);
    }

    public void DrawContent()
    {
        using var child = Im.Child.Begin("##SettingsTab"u8, -Vector2.One);
        if (!child)
            return;

        DrawGeneralSettings();
        _migrationDrawer.Draw();
        DrawColorSettings();
        DrawPredefinedTagsSection();
        DrawAdvancedSettings();
        _integrationSettings.Draw();
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
        using var header = Im.Tree.HeaderId("Colors"u8);
        if (!header)
            return;

        if (ColorSettingsDrawer.Draw(Penumbra.Messager, _config.Ui.Colors, _config.Ui.ColorCache))
        {
            CacheManager.Instance.SetColorsDirty();
            _config.Ui.Save();
        }

        Im.Line.New();
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
        UiHelpers.DrawSupportButton(_penumbra);

        Im.Cursor.Position = new Vector2(xPos, 0);
        SupportButton.Discord(Penumbra.Messager, width);

        Im.Cursor.Position = new Vector2(xPos, 2 * Im.Style.FrameHeightWithSpacing);
        SupportButton.ReniGuide(Penumbra.Messager, width);

        Im.Cursor.Position = new Vector2(xPos, 3 * Im.Style.FrameHeightWithSpacing);
        if (Im.Button("Restart Tutorial"u8, new Vector2(width, 0)))
        {
            _config.Ephemeral.TutorialStep = 0;
            _config.Ephemeral.Save();
        }

        Im.Cursor.Position = new Vector2(xPos, 4 * Im.Style.FrameHeightWithSpacing);
        if (Im.Button("Show Changelogs"u8, new Vector2(width, 0)))
            _penumbra.ForceChangelogOpen();

        Im.Cursor.Position = new Vector2(xPos, 5 * Im.Style.FrameHeightWithSpacing);
        SupportButton.KoFiPatreon(Penumbra.Messager, new Vector2(width, 0));
    }

    private void DrawPredefinedTagsSection()
    {
        if (!Im.Tree.Header("Tags"u8))
            return;

        var tagIdx = TagButtons.Draw("Predefined Tags: "u8,
            "Predefined tags that can be added or removed from mods with a single click."u8, _predefinedTagManager,
            out var editedTag);

        if (tagIdx >= 0)
            _predefinedTagManager.ChangeSharedTag(tagIdx, editedTag);
    }
}
