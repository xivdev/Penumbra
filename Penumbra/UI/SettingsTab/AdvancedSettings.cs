using ImSharp;
using JetBrains.Annotations;
using Luna;
using Penumbra.Import.Textures;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Interop.Services;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.UI;

public sealed class AdvancedSettings(
    AdvancedConfig config,
    FileCompactor compactor,
    ModManager modManager,
    DalamudConfigService dalamudConfig,
    FontReloader fontReloader,
    ResidentResourceManager residentResources,
    CharacterUtility characterUtility) : IUiService
{
    [UsedImplicitly]
    private readonly bool _initializedCompactor = InitializeCompactor(config, compactor);

    /// <summary> Draw all advanced settings. </summary>
    public void Draw()
    {
        if (SettingsTab.Checkbox("Enable Penumbra Crash Logging (Experimental)"u8,
                "Enables Penumbra to launch a secondary process that records some game activity which may or may not help diagnosing Penumbra-related game crashes."u8,
                config.UseCrashHandler ?? false))
            config.UseCrashHandler = !(config.UseCrashHandler ?? false);

        DrawMinimumDimensionConfig();
        DrawHdrRenderTargets();
        DrawAuxiliaryDeviceMode();
        if (SettingsTab.Checkbox("Auto Deduplicate on Import"u8,
                "Automatically deduplicate mod files on import. This will make mod file sizes smaller, but deletes (binary identical) files."u8,
                config.AutoDeduplicateOnImport))
            config.AutoDeduplicateOnImport ^= true;
        if (SettingsTab.Checkbox("Auto Reduplicate UI Files on PMP Import"u8,
                "Automatically reduplicate and normalize UI-specific files on import from PMP files. This is STRONGLY recommended because deduplicated UI files crash the game."u8,
                config.AutoReduplicateUiOnImport))
            config.AutoDeduplicateOnImport ^= true;
        DrawCompressionBox();
        if (SettingsTab.Checkbox("Keep Default Metadata Changes on Import"u8,
                "Normally, metadata changes that equal their default values, which are sometimes exported by TexTools, are discarded. "u8
              + "Toggle this to keep them, for example if an option in a mod is supposed to disable a metadata change from a prior option."u8,
                config.KeepDefaultMetaChanges))
            config.KeepDefaultMetaChanges ^= true;
        if (SettingsTab.Checkbox("Enable Custom Shape and Attribute Support"u8,
                "Penumbra will allow for custom shape keys and attributes for modded models to be considered and combined."u8,
                config.EnableCustomShapes))
            config.EnableCustomShapes ^= true;
        DrawWaitForPluginsReflection();
        DrawEnableHttpApiBox();
        DrawEnableDebugModeBox();
        Im.Separator();
        DrawReloadResourceButton();
        DrawReloadFontsButton();
        Im.Line.Spacing();
    }

    private void DrawCompressionBox()
    {
        if (!compactor.CanCompact)
            return;

        if (SettingsTab.Checkbox("Use Filesystem Compression"u8,
                "Use Windows functionality to transparently reduce storage size of mod files on your computer. This might cost performance, but seems to generally be beneficial to performance by shifting more responsibility to the underused CPU and away from the overused hard drives."u8,
                config.UseFileSystemCompression))
        {
            config.UseFileSystemCompression ^= true;
            compactor.Enabled               =  config.UseFileSystemCompression;
        }

        Im.Line.Same();
        if (ImEx.Button("Compress Existing Files"u8, Vector2.Zero,
                "Try to compress all files in your root directory. This will take a while."u8,
                compactor.MassCompactRunning || !modManager.Valid))
            compactor.StartMassCompact(modManager.BasePath.EnumerateFiles("*.*", SearchOption.AllDirectories),
                CompressionAlgorithm.Xpress8K,
                true);

        Im.Line.Same();
        if (ImEx.Button("Decompress Existing Files"u8, Vector2.Zero,
                "Try to decompress all files in your root directory. This will take a while."u8,
                compactor.MassCompactRunning || !modManager.Valid))
            compactor.StartMassCompact(modManager.BasePath.EnumerateFiles("*.*", SearchOption.AllDirectories), CompressionAlgorithm.None,
                true);

        if (compactor.MassCompactRunning)
        {
            Im.ProgressBar((float)compactor.CurrentIndex / compactor.TotalFiles, new Vector2(
                    Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X - UiHelpers.IconButtonSize.X,
                    Im.Style.FrameHeight),
                compactor.CurrentFile?.FullName[(modManager.BasePath.FullName.Length + 1)..] ?? "Gathering Files...");
            Im.Line.Same();
            if (ImEx.Icon.Button(LunaStyle.CancelIcon, "Cancel the mass action."u8, !compactor.MassCompactRunning))
                compactor.CancelMassCompact();
        }
        else
        {
            Im.FrameDummy();
        }
    }

    /// <summary> Draw two integral inputs for minimum dimensions of this window. </summary>
    private void DrawMinimumDimensionConfig()
    {
        var warning = config.MinimumSize.X < AdvancedConfig.MinimumSizeX
            ? config.MinimumSize.Y < AdvancedConfig.MinimumSizeY
                ? "Size is smaller than default: This may look undesirable."u8
                : "Width is smaller than default: This may look undesirable."u8
            : config.MinimumSize.Y < AdvancedConfig.MinimumSizeY
                ? "Height is smaller than default: This may look undesirable."u8
                : StringU8.Empty;
        var buttonWidth = UiHelpers.InputTextWidth.X / 2.5f;
        Im.Item.SetNextWidth(buttonWidth);
        if (ImEx.InputOnDeactivation.Drag("##xMinSize"u8, (int)config.MinimumSize.X, out var newX, 500, 1500, 0.1f))
            config.MinimumSize = config.MinimumSize with { X = newX };

        Im.Line.Same();
        Im.Item.SetNextWidth(buttonWidth);
        if (ImEx.InputOnDeactivation.Drag("##yMinSize"u8, (int)config.MinimumSize.Y, out var newY, 300, 1500, 0.1f))
            config.MinimumSize = config.MinimumSize with { Y = newY };

        Im.Line.Same();
        if (ImEx.Button("Reset##resetMinSize"u8, new Vector2(buttonWidth / 2 - Im.Style.ItemSpacing.X * 2, 0),
                $"Reset minimum dimensions to ({AdvancedConfig.MinimumSizeX}, {AdvancedConfig.MinimumSizeY}).",
                config.MinimumSize is { X: AdvancedConfig.MinimumSizeX, Y: AdvancedConfig.MinimumSizeY }))
            config.MinimumSize = new Vector2(AdvancedConfig.MinimumSizeX, AdvancedConfig.MinimumSizeY);

        LunaStyle.DrawAlignedHelpMarkerLabel("Minimum Window Dimensions"u8,
            "Set the minimum dimensions for resizing this window. Reducing these dimensions may cause the window to look bad or more confusing and is not recommended."u8);

        if (warning.Length > 0)
            ImEx.TextFramed(warning, UiHelpers.InputTextWidth, DalamudColor.AttentionBackground.Value);
        else
            Im.Line.New();
    }

    private void DrawHdrRenderTargets()
    {
        if (!RenderTargetHdrEnabler.HdrModeSupported)
            return;

#pragma warning disable CS0162 // Unreachable code detected
        Im.Item.SetNextWidth(Im.Font.CalculateSize("M"u8).X * 5.0f + Im.Style.FrameHeight);
        using (var combo = Im.Combo.Begin("##hdrRenderTarget"u8, config.HdrRenderTargets ? "HDR"u8 : "SDR"u8))
        {
            if (combo)
            {
                if (Im.Selectable("HDR"u8, config.HdrRenderTargets) && !config.HdrRenderTargets)
                {
                    config.HdrRenderTargets = true;
                    config.Save();
                }

                if (Im.Selectable("SDR"u8, !config.HdrRenderTargets) && config.HdrRenderTargets)
                {
                    config.HdrRenderTargets = false;
                    config.Save();
                }
            }
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Diffuse Dynamic Range"u8,
            "Set the dynamic range that can be used for diffuse colors in materials without causing visual artifacts.\n"u8
          + "Changing this setting requires a game restart. It also only works if Wait for Plugins on Startup is enabled."u8);
#pragma warning restore CS0162 // Unreachable code detected
    }

    private void DrawAuxiliaryDeviceMode()
    {
        Im.Item.SetNextWidth(UiHelpers.InputTextWidth.X);
        using (var combo = Im.Combo.Begin("##auxiliaryDeviceMode"u8, config.AuxiliaryDeviceMode.ToNameU8()))
        {
            if (combo)
                foreach (var value in AuxiliaryDeviceMode.Values)
                {
                    if (Im.Selectable(value.ToNameU8(), config.AuxiliaryDeviceMode == value))
                        config.AuxiliaryDeviceMode = value;

                    Im.Tooltip.OnHover(value.Tooltip());
                }
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("Hardware Acceleration Mode for Texture Compression"u8,
            "How to manage hardware acceleration for texture compression.\nChange this if you run into ReShade issues after compressing textures."u8);
    }

    /// <summary> Draw a checkbox for the HTTP API that creates and destroys the web server when toggled. </summary>
    private void DrawEnableHttpApiBox()
    {
        if (SettingsTab.Checkbox("Enable HTTP API"u8,
                "Enables other applications, e.g. Anamnesis, to use some Penumbra functions, like requesting redraws."u8, config.EnableHttpApi))
            config.EnableHttpApi ^= true;
    }

    /// <summary> Draw a checkbox to toggle Debug mode. </summary>
    private void DrawEnableDebugModeBox()
    {
        if (SettingsTab.Checkbox("Enable Debug Mode"u8,
                "Enable the Debug Tab and Resource Manager Tab as well as some additional data collection. Also open the config window on plugin load."u8,
                config.DebugMode))
            config.DebugMode ^= true;
    }

    /// <summary> Draw a button that reloads resident resources. </summary>
    private void DrawReloadResourceButton()
    {
        if (ImEx.Button("Reload Resident Resources"u8, Vector2.Zero,
                "Reload some specific files that the game keeps in memory at all times.\nYou usually should not need to do this."u8,
                !characterUtility.Ready))
            residentResources.Reload();
    }

    /// <summary> Draw a button that reloads fonts. </summary>
    private void DrawReloadFontsButton()
    {
        if (ImEx.Button("Reload Fonts"u8, Vector2.Zero, "Force the game to reload its font files."u8, !fontReloader.Valid))
            fontReloader.Reload();
    }


    /// <summary> Draw a checkbox that toggles the dalamud setting to wait for plugins on open. </summary>
    private void DrawWaitForPluginsReflection()
    {
        if (!dalamudConfig.GetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, out bool value))
        {
            using var disabled = Im.Disabled();
            SettingsTab.Checkbox("Wait for Plugins on Startup (Disabled, can not access Dalamud Configuration)"u8, StringU8.Empty,
                false);
        }
        else
        {
            if (SettingsTab.Checkbox("Wait for Plugins on Startup"u8,
                    "Some mods need to change files that are loaded once when the game starts and never afterwards.\n"u8
                  + "This can cause issues with Penumbra loading after the files are already loaded.\n"u8
                  + "This setting causes the game to wait until certain plugins have finished loading, making those mods work (in the base collection).\n\n"u8
                  + "This changes a setting in the Dalamud Configuration found at /xlsettings -> General."u8, value))
                dalamudConfig.SetDalamudConfig(DalamudConfigService.WaitingForPluginsOption, !value, "doWaitForPluginsOnStartup");
        }
    }

    private static bool InitializeCompactor(AdvancedConfig config, FileCompactor compactor)
    {
        if (compactor.CanCompact)
            compactor.Enabled = config.UseFileSystemCompression;
        return true;
    }
}
