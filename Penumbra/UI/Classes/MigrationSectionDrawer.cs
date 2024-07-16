using ImGuiNET;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.Services;

namespace Penumbra.UI.Classes;

public class MigrationSectionDrawer(MigrationManager migrationManager, Configuration config) : IUiService
{
    private bool    _createBackups = true;
    private Vector2 _buttonSize;

    public void Draw()
    {
        using var header = ImUtf8.CollapsingHeaderId("Migration"u8);
        if (!header)
            return;

        _buttonSize = UiHelpers.InputTextWidth;
        DrawSettings();
        ImGui.Separator();
        DrawMdlMigration();
        DrawMdlRestore();
        DrawMdlCleanup();
        // TODO enable when this works
        ImGui.Separator();
        //DrawMtrlMigration();
        DrawMtrlRestore();
        DrawMtrlCleanup();
    }

    private void DrawSettings()
    {
        var value = config.MigrateImportedModelsToV6;
        if (ImUtf8.Checkbox("Automatically Migrate V5 Models to V6 on Import"u8, ref value))
        {
            config.MigrateImportedModelsToV6 = value;
            config.Save();
        }

        ImUtf8.HoverTooltip("This increments the version marker and restructures the bone table to the new version."u8);

        // TODO enable when this works
        //value = config.MigrateImportedMaterialsToLegacy;
        //if (ImUtf8.Checkbox("Automatically Migrate Materials to Dawntrail on Import"u8, ref value))
        //{
        //    config.MigrateImportedMaterialsToLegacy = value;
        //    config.Save();
        //}
        //
        //ImUtf8.HoverTooltip(
        //    "This currently only increases the color-table size and switches the shader from 'character.shpk' to 'characterlegacy.shpk', if the former is used."u8);

        ImUtf8.Checkbox("Create Backups During Manual Migration", ref _createBackups);
    }

    private static ReadOnlySpan<byte> MigrationTooltip
        => "Cancel the migration. This does not revert already finished migrations."u8;

    private void DrawMdlMigration()
    {
        if (ImUtf8.ButtonEx("Migrate Model Files From V5 to V6"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.MigrateMdlDirectory(config.ModDirectory, _createBackups);

        ImUtf8.SameLineInner();
        DrawCancelButton(MigrationManager.TaskType.MdlMigration, "Cancel the migration. This does not revert already finished migrations."u8);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlMigration, IsRunning: true });
        DrawData(migrationManager.MdlMigration, "No model files found."u8, "migrated"u8);
    }

    private void DrawMtrlMigration()
    {
        if (ImUtf8.ButtonEx("Migrate Material Files to Dawntrail"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.MigrateMtrlDirectory(config.ModDirectory, _createBackups);

        ImUtf8.SameLineInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlMigration, MigrationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlMigration, IsRunning: true });
        DrawData(migrationManager.MtrlMigration, "No material files found."u8, "migrated"u8);
    }


    private static ReadOnlySpan<byte> CleanupTooltip
        => "Cancel the cleanup. This is not revertible."u8;

    private void DrawMdlCleanup()
    {
        if (ImUtf8.ButtonEx("Delete Existing Model Backup Files"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.CleanMdlBackups(config.ModDirectory);

        ImUtf8.SameLineInner();
        DrawCancelButton(MigrationManager.TaskType.MdlCleanup, CleanupTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlCleanup, IsRunning: true });
        DrawData(migrationManager.MdlCleanup, "No model backup files found."u8, "deleted"u8);
    }

    private void DrawMtrlCleanup()
    {
        if (ImUtf8.ButtonEx("Delete Existing Material Backup Files"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.CleanMtrlBackups(config.ModDirectory);

        ImUtf8.SameLineInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlCleanup, CleanupTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlCleanup, IsRunning: true });
        DrawData(migrationManager.MtrlCleanup, "No material backup files found."u8, "deleted"u8);
    }

    private static ReadOnlySpan<byte> RestorationTooltip
        => "Cancel the restoration. This does not revert already finished restoration."u8;

    private void DrawMdlRestore()
    {
        if (ImUtf8.ButtonEx("Restore Model Backups"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.RestoreMdlBackups(config.ModDirectory);

        ImUtf8.SameLineInner();
        DrawCancelButton(MigrationManager.TaskType.MdlRestoration, RestorationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MdlRestoration, IsRunning: true });
        DrawData(migrationManager.MdlRestoration, "No model backup files found."u8, "restored"u8);
    }

    private void DrawMtrlRestore()
    {
        if (ImUtf8.ButtonEx("Restore Material Backups"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.RestoreMtrlBackups(config.ModDirectory);

        ImUtf8.SameLineInner();
        DrawCancelButton(MigrationManager.TaskType.MtrlRestoration, RestorationTooltip);
        DrawSpinner(migrationManager is { CurrentTask: MigrationManager.TaskType.MtrlRestoration, IsRunning: true });
        DrawData(migrationManager.MtrlRestoration, "No material backup files found."u8, "restored"u8);
    }

    private static void DrawSpinner(bool enabled)
    {
        if (!enabled)
            return;

        ImGui.SameLine();
        ImUtf8.Spinner("Spinner"u8, ImGui.GetTextLineHeight() / 2, 2, ImGui.GetColorU32(ImGuiCol.Text));
    }

    private void DrawCancelButton(MigrationManager.TaskType task, ReadOnlySpan<byte> tooltip)
    {
        using var _ = ImUtf8.PushId((int)task);
        if (ImUtf8.ButtonEx("Cancel"u8, tooltip, disabled: !migrationManager.IsRunning || task != migrationManager.CurrentTask))
            migrationManager.Cancel();
    }

    private static void DrawData(MigrationManager.MigrationData data, ReadOnlySpan<byte> empty, ReadOnlySpan<byte> action)
    {
        if (!data.HasData)
        {
            ImUtf8.IconDummy();
            return;
        }

        var total = data.Total;
        if (total == 0)
            ImUtf8.TextFrameAligned(empty);
        else
            ImUtf8.TextFrameAligned($"{data.Changed} files {action}, {data.Failed} files failed, {total} files found.");
    }
}
