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
        DrawMigration();
        ImGui.Separator();
        DrawCleanup();
        ImGui.Separator();
        DrawRestore();
    }

    private void DrawSettings()
    {
        var value = config.MigrateImportedModelsToV6;
        if (ImUtf8.Checkbox("Automatically Migrate V5 Models to V6 on Import"u8, ref value))
        {
            config.MigrateImportedModelsToV6 = value;
            config.Save();
        }
    }

    private void DrawMigration()
    {
        ImUtf8.Checkbox("Create Backups During Manual Migration", ref _createBackups);
        if (ImUtf8.ButtonEx("Migrate Model Files From V5 to V6"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.MigrateDirectory(config.ModDirectory, _createBackups);

        ImUtf8.SameLineInner();
        DrawCancelButton(0, "Cancel the migration. This does not revert already finished migrations."u8);
        DrawSpinner(migrationManager is { IsMigrationTask: true, IsRunning: true });

        if (!migrationManager.HasMigrationTask)
        {
            ImUtf8.IconDummy();
            return;
        }

        var total = migrationManager.Failed + migrationManager.Migrated + migrationManager.Unchanged;
        if (total == 0)
            ImUtf8.TextFrameAligned("No model files found."u8);
        else
            ImUtf8.TextFrameAligned($"{migrationManager.Migrated} files migrated, {migrationManager.Failed} files failed, {total} total files.");
    }

    private void DrawCleanup()
    {
        if (ImUtf8.ButtonEx("Delete Existing Model Backup Files"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.CleanBackups(config.ModDirectory);

        ImUtf8.SameLineInner();
        DrawCancelButton(1, "Cancel the cleanup. This is not revertible."u8);
        DrawSpinner(migrationManager is { IsCleanupTask: true, IsRunning: true });
        if (!migrationManager.HasCleanUpTask)
        {
            ImUtf8.IconDummy();
            return;
        }

        var total = migrationManager.CleanedUp + migrationManager.CleanupFails;
        if (total == 0)
            ImUtf8.TextFrameAligned("No model backup files found."u8);
        else
            ImUtf8.TextFrameAligned(
                $"{migrationManager.CleanedUp} backups deleted, {migrationManager.CleanupFails} deletions failed, {total} total backups.");
    }

    private void DrawSpinner(bool enabled)
    {
        if (!enabled)
            return;
        ImGui.SameLine();
        ImUtf8.Spinner("Spinner"u8, ImGui.GetTextLineHeight() / 2, 2, ImGui.GetColorU32(ImGuiCol.Text));
    }

    private void DrawRestore()
    {
        if (ImUtf8.ButtonEx("Restore Model Backups"u8, "\0"u8, _buttonSize, migrationManager.IsRunning))
            migrationManager.RestoreBackups(config.ModDirectory);

        ImUtf8.SameLineInner();
        DrawCancelButton(2, "Cancel the restoration. This does not revert already finished restoration."u8);
        DrawSpinner(migrationManager is { IsRestorationTask: true, IsRunning: true });

        if (!migrationManager.HasRestoreTask)
        {
            ImUtf8.IconDummy();
            return;
        }

        var total = migrationManager.Restored + migrationManager.RestoreFails;
        if (total == 0)
            ImUtf8.TextFrameAligned("No model backup files found."u8);
        else
            ImUtf8.TextFrameAligned(
                $"{migrationManager.Restored} backups restored, {migrationManager.RestoreFails} restorations failed, {total} total backups.");
    }

    private void DrawCancelButton(int id, ReadOnlySpan<byte> tooltip)
    {
        using var _ = ImUtf8.PushId(id);
        if (ImUtf8.ButtonEx("Cancel"u8, tooltip, disabled: !migrationManager.IsRunning))
            migrationManager.Cancel();
    }
}
