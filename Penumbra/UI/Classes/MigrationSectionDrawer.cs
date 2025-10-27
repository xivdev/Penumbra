using ImSharp;
using Penumbra.Services;

namespace Penumbra.UI.Classes;

public class MigrationSectionDrawer(MigrationManager migrationManager, Configuration config) : Luna.IUiService
{
    private bool    _createBackups = true;
    private Vector2 _buttonSize;

    public void Draw()
    {
        using var header = Im.Tree.HeaderId("Migration"u8);
        if (!header)
            return;

        _buttonSize = UiHelpers.InputTextWidth;
        DrawSettings();
        Im.Separator();
        DrawMdlMigration();
        DrawMdlRestore();
        DrawMdlCleanup();
        // TODO enable when this works
        Im.Separator();
        //DrawMtrlMigration();
        DrawMtrlRestore();
        DrawMtrlCleanup();
    }

    private void DrawSettings()
    {
        var value = config.MigrateImportedModelsToV6;
        if (Im.Checkbox("Automatically Migrate V5 Models to V6 on Import"u8, ref value))
        {
            config.MigrateImportedModelsToV6 = value;
            config.Save();
        }

        Im.Tooltip.OnHover("This increments the version marker and restructures the bone table to the new version."u8);

        // TODO enable when this works
        //value = config.MigrateImportedMaterialsToLegacy;
        //if (Im.Checkbox("Automatically Migrate Materials to Dawntrail on Import"u8, ref value))
        //{
        //    config.MigrateImportedMaterialsToLegacy = value;
        //    config.Save();
        //}
        //
        //Im.Tooltip.OnHover(
        //    "This currently only increases the color-table size and switches the shader from 'character.shpk' to 'characterlegacy.shpk', if the former is used."u8);

        Im.Checkbox("Create Backups During Manual Migration"u8, ref _createBackups);
    }

    private static ReadOnlySpan<byte> MigrationTooltip
        => "Cancel the migration. This does not revert already finished migrations."u8;

    private void DrawMdlMigration()
    {
        if (ImEx.Button("Migrate Model Files From V5 to V6"u8, _buttonSize, StringU8.Empty, migrationManager.IsRunning))
            migrationManager.MigrateMdlDirectory(config.ModDirectory, _createBackups);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MdlMigration, "Cancel the migration. This does not revert already finished migrations."u8);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlMigration, IsRunning: true });
        DrawData(migrationManager.MdlMigration, "No model files found."u8, "migrated"u8);
    }

    private void DrawMtrlMigration()
    {
        if (ImEx.Button("Migrate Material Files to Dawntrail"u8, _buttonSize, StringU8.Empty, migrationManager.IsRunning))
            migrationManager.MigrateMtrlDirectory(config.ModDirectory, _createBackups);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlMigration, MigrationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlMigration, IsRunning: true });
        DrawData(migrationManager.MtrlMigration, "No material files found."u8, "migrated"u8);
    }


    private static ReadOnlySpan<byte> CleanupTooltip
        => "Cancel the cleanup. This is not revertible."u8;

    private void DrawMdlCleanup()
    {
        if (ImEx.Button("Delete Existing Model Backup Files"u8, _buttonSize, StringU8.Empty, migrationManager.IsRunning))
            migrationManager.CleanMdlBackups(config.ModDirectory);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MdlCleanup, CleanupTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlCleanup, IsRunning: true });
        DrawData(migrationManager.MdlCleanup, "No model backup files found."u8, "deleted"u8);
    }

    private void DrawMtrlCleanup()
    {
        if (ImEx.Button("Delete Existing Material Backup Files"u8, _buttonSize, StringU8.Empty, migrationManager.IsRunning))
            migrationManager.CleanMtrlBackups(config.ModDirectory);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlCleanup, CleanupTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlCleanup, IsRunning: true });
        DrawData(migrationManager.MtrlCleanup, "No material backup files found."u8, "deleted"u8);
    }

    private static ReadOnlySpan<byte> RestorationTooltip
        => "Cancel the restoration. This does not revert already finished restoration."u8;

    private void DrawMdlRestore()
    {
        if (ImEx.Button("Restore Model Backups"u8, _buttonSize, StringU8.Empty, migrationManager.IsRunning))
            migrationManager.RestoreMdlBackups(config.ModDirectory);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MdlRestoration, RestorationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlRestoration, IsRunning: true });
        DrawData(migrationManager.MdlRestoration, "No model backup files found."u8, "restored"u8);
    }

    private void DrawMtrlRestore()
    {
        if (ImEx.Button("Restore Material Backups"u8, _buttonSize, StringU8.Empty, migrationManager.IsRunning))
            migrationManager.RestoreMtrlBackups(config.ModDirectory);

        Im.Line.SameInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlRestoration, RestorationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlRestoration, IsRunning: true });
        DrawData(migrationManager.MtrlRestoration, "No material backup files found."u8, "restored"u8);
    }

    private static void DrawSpinner(bool enabled)
    {
        if (!enabled)
            return;

        Im.Line.Same();
        ImEx.Spinner("Spinner"u8, Im.Style.TextHeight / 2, 2, ImGuiColor.Text.Get());
    }

    private void DrawCancelButton(MigrationManager.TaskType task, ReadOnlySpan<byte> tooltip)
    {
        using var _ = Im.Id.Push((int)task);
        if (ImEx.Button("Cancel"u8, Vector2.Zero, tooltip, !migrationManager.IsRunning || task != migrationManager.CurrentTask))
            migrationManager.Cancel();
    }

    private static void DrawData(MigrationManager.MigrationData data, ReadOnlySpan<byte> empty, ReadOnlySpan<byte> action)
    {
        if (!data.HasData)
        {
            Im.FrameDummy();
            return;
        }

        var total = data.Total;
        if (total is 0)
            ImEx.TextFrameAligned(empty);
        else
            ImEx.TextFrameAligned($"{data.Changed} files {action}, {data.Failed} files failed, {total} files found.");
    }
}
