using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.Services;

namespace Penumbra.UI.ManagementTab;

public enum ManagementTabType
{
    UnusedMods,
    DuplicateMods,
    Cleanup,
}

public sealed class CleanupTab(CleanupService cleanup, Configuration config) : ITab<ManagementTabType>
{
    public ReadOnlySpan<byte> Label
        => "General Cleanup"u8;

    public ManagementTabType Identifier
        => ManagementTabType.Cleanup;

    public void DrawContent()
    {
        using var child = Im.Child.Begin("c"u8, Im.ContentRegion.Available);
        if (!child)
            return;

        var enabled = config.DeleteModModifier.IsActive();
        if (cleanup.Progress is not 0.0 and not 1.0)
        {
            Im.ProgressBar((float)cleanup.Progress, new Vector2(200 * Im.Style.GlobalScale, Im.Style.FrameHeight),
                $"{cleanup.Progress * 100}%");
            Im.Line.Same();
            if (Im.Button("Cancel##FileCleanup"u8))
                cleanup.Cancel();
        }
        else
        {
            Im.Line.New();
        }

        if (ImEx.Button("Clear Unused Local Mod Data Files"u8, default,
                "Delete all local mod data files that do not correspond to currently installed mods."u8,
                !enabled || cleanup.IsRunning))
            cleanup.CleanUnusedLocalData();
        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"Hold {config.DeleteModModifier} while clicking to delete files.");

        if (ImEx.Button("Clear Backup Files"u8, default,
                "Delete all backups of .json configuration files in your configuration folder and all backups of mod group files in your mod directory."u8,
                !enabled || cleanup.IsRunning))
            cleanup.CleanBackupFiles();
        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"Hold {config.DeleteModModifier} while clicking to delete files.");

        if (ImEx.Button("Clear All Unused Settings"u8, default,
                "Remove all mod settings in all of your collections that do not correspond to currently installed mods."u8,
                !enabled || cleanup.IsRunning))
            cleanup.CleanupAllUnusedSettings();
        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"Hold {config.DeleteModModifier} while clicking to remove settings.");
    }
}

public sealed class ManagementTab : TabBar<ManagementTabType>, ITab<TabType>
{
    public new ReadOnlySpan<byte> Label
        => base.Label;

    public TabType Identifier
        => TabType.Management;

    public ManagementTab(Logger log,
        EphemeralConfig config,
        UnusedModsTab unusedMods,
        DuplicateModsTab duplicateMods,
        CleanupTab cleanup)
        : base("Management", log, unusedMods, duplicateMods, cleanup)
    {
        NextTab = config.SelectedManagementTab;
        TabSelected.Subscribe((in tab) => config.SelectedManagementTab = tab, 0);
    }

    public void DrawContent()
        => Draw();
}
