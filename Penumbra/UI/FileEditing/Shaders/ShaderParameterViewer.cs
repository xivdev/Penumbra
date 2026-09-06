using Dalamud.Interface.ImGuiNotification;
using ImSharp;
using Luna;
using Penumbra.GameData.Files;
using Penumbra.UI.Classes;

namespace Penumbra.UI.FileEditing.Shaders;

public sealed class ShaderParameterViewer(FileDialogService fileDialog, SpmFile spm, string path) : IFileEditor
{
    private readonly bool _hasConstantColumns = spm.Columns.Any(column => column.ConstantValue.HasValue);
    private readonly bool _hasBlankRows       = spm.Rows.Any(row => row.IsBlank);

    public bool DrawToolbar(bool disabled)
    {
        if (Im.Button("Export as CSV"u8))
        {
            fileDialog.OpenSavePicker("Export Shader Parameters as CSV...", ".csv", $"{Path.GetFileNameWithoutExtension(path)}.csv", ".csv",
                (success, name) =>
                {
                    if (!success)
                        return;

                    ExportAsCsv(name);
                }, null, false);
        }

        return false;
    }

    public bool DrawPanel(bool disabled)
    {
        Im.Text($"Version: 0x{spm.Version:X8}");
        Im.Separator();
        DrawMainTable();
        if (_hasConstantColumns)
        {
            Im.Separator();
            DrawConstantColumns();
        }

        if (_hasBlankRows)
        {
            Im.Separator();
            DrawBlankTable();
        }

        return false;
    }

    private void DrawMainTable()
    {
        var scale       = Im.Style.GlobalScale;
        var cellPadding = Im.Style.CellPadding.X * 2f;
        var numColumns  = spm.Columns.Count(column => !column.ConstantValue.HasValue);
        var totalWidth = 120f * scale
          + 2f * cellPadding
          + spm.Columns.Sum(column
                => column.ConstantValue.HasValue ? 0f : MathF.Max(50f * scale, Im.Font.CalculateSize(column.Name.ToNameU8()).X) + cellPadding);

        using var table = Im.Table.Begin("###MainTable"u8, numColumns + 2, TableFlags.SizingFixedFit, new Vector2(totalWidth, 0f));
        if (!table)
            return;

        table.SetupColumn("Table"u8, TableColumnFlags.WidthFixed, 70f * scale);
        table.SetupColumn("ID"u8,    TableColumnFlags.WidthFixed, 50f * scale);
        foreach (var column in spm.Columns)
        {
            if (column.ConstantValue.HasValue)
                continue;

            var name = column.Name.ToNameU8();
            table.SetupColumn(name, TableColumnFlags.WidthFixed, MathF.Max(50f * scale, Im.Font.CalculateSize(name).X));
        }

        table.HeaderRow();

        foreach (var row in spm.Rows)
        {
            if (row.IsBlank)
                continue;

            table.NextColumn();
            ImEx.TextRightAligned(row.Table.ToNameU8());
            table.NextColumn();
            ImEx.TextRightAligned($"{row.Index}");
            foreach (var (index, column) in spm.Columns.Index())
            {
                if (column.ConstantValue.HasValue)
                    continue;

                table.NextColumn();
                ImEx.TextRightAligned($"{row.Values[index].ToString(column.Type, CultureInfo.CurrentCulture)}");
            }
        }
    }

    private void DrawConstantColumns()
    {
        Im.Text("Constant Columns:"u8);

        var maxNameWidth = 0.0f;
        foreach (var column in spm.Columns)
        {
            if (!column.ConstantValue.HasValue)
                continue;

            maxNameWidth = MathF.Max(maxNameWidth, Im.Font.CalculateSize(column.Name.ToNameU8()).X);
        }

        foreach (var column in spm.Columns)
        {
            if (!column.ConstantValue.HasValue)
                continue;

            var name  = column.Name.ToNameU8();
            var width = Im.Font.CalculateSize(name).X;
            Im.Cursor.X += maxNameWidth - width;
            Im.Text(name);
            Im.Line.Same();
            Im.Text(column.ConstantValue.Value.ToString(column.Type, CultureInfo.CurrentCulture));
        }
    }

    private void DrawBlankTable()
    {
        Im.Text("Blank Rows:"u8);

        var scale             = Im.Style.GlobalScale;
        var cellPadding       = Im.Style.CellPadding.X * 2f;

        using var table = Im.Table.Begin("###BlankTable"u8, 2, TableFlags.SizingFixedFit, new Vector2(120f * scale + 2f * cellPadding, 0f));
        if (!table)
            return;

        table.SetupColumn("Table"u8, TableColumnFlags.WidthFixed, 70f * scale);
        table.SetupColumn("ID"u8,    TableColumnFlags.WidthFixed, 50f * scale);

        table.HeaderRow();

        foreach (var row in spm.Rows)
        {
            if (!row.IsBlank)
                continue;

            table.NextColumn();
            ImEx.TextRightAligned(row.Table.ToNameU8());
            table.NextColumn();
            ImEx.TextRightAligned($"{row.Index}");
        }
    }

    private async void ExportAsCsv(string name)
    {
        var lines   = new List<string>();
        var builder = new StringBuilder();

        // Header
        builder.Append(null, $"Table;ID");
        foreach (var column in spm.Columns)
            builder.Append(null, $";{column.Name.ToName()}");
        lines.Add(builder.ToString());
        builder.Clear();

        // Data
        foreach (var row in spm.Rows)
        {
            builder.Append(null, $"{row.Table};{row.Index}");
            foreach (var (index, column) in spm.Columns.Index())
                builder.Append(null, $";{row.Values[index].ToString(column.Type, CultureInfo.InvariantCulture)}");
            lines.Add(builder.ToString());
            builder.Clear();
        }

        try
        {
            await File.WriteAllLinesAsync(name, lines);
        }
        catch (Exception e)
        {
            Penumbra.Messager.NotificationMessage(e, $"Could not export {Path.GetFileName(path)} to {Path.GetFileName(name)}.",
                NotificationType.Error, false);
        }
    }

    #region IFileEditor dummies

    bool IWritable.Valid
        => false;

    event Action? IFileEditor.SaveRequested
    {
        add { }
        remove { }
    }

    void IDisposable.Dispose()
    { }

    byte[] IWritable.Write()
        => throw new NotSupportedException();

    Task<byte[]> IFileEditor.WriteAsync()
        => throw new NotSupportedException();

    #endregion
}
