using ImSharp;
using Penumbra.CrashHandler;

namespace Penumbra.UI.Tabs.Debug;

public static class CrashDataExtensions
{
    public static void DrawMeta(this CrashData data)
    {
        using (Im.Group())
        {
            Im.Text("Mode"u8);
            Im.Text("Crash Time"u8);
            Im.Text("Current Age"u8);
            Im.Text("Version"u8);
            Im.Text("Game Version"u8);
            Im.Text("Exit Code"u8);
            Im.Text("Process ID"u8);
            Im.Text("Total Modded Files Loaded"u8);
            Im.Text("Total Characters Loaded"u8);
            Im.Text("Total VFX Functions Invoked"u8);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            Im.Text(data.Mode);
            Im.Text($"{data.CrashTime}");
            Im.Text((DateTimeOffset.UtcNow - data.CrashTime).ToString(@"dd\.hh\:mm\:ss"));
            Im.Text(data.Version);
            Im.Text(data.GameVersion);
            Im.Text($"{data.ExitCode}");
            Im.Text($"{data.ProcessId}");
            Im.Text($"{data.TotalModdedFilesLoaded}");
            Im.Text($"{data.TotalCharactersLoaded}");
            Im.Text($"{data.TotalVFXFuncsInvoked}");
        }
    }

    public static void DrawCharacters(this CrashData data)
    {
        using var tree = Im.Tree.Node("Last Characters"u8);
        if (!tree)
            return;

        using var table = Im.Table.Begin("##characterTable"u8, 6,
            TableFlags.SizingFixedFit | TableFlags.RowBackground | TableFlags.BordersInner);
        if (!table)
            return;

        using var clipper = new Im.ListClipper(data.LastCharactersLoaded.Count, Im.Style.TextHeightWithSpacing);
        foreach (var character in clipper.Iterate(data.LastCharactersLoaded))
        {
            table.DrawColumn($"{character.Age}");
            table.DrawColumn($"{character.ThreadId}");
            table.DrawColumn(character.CharacterName);
            table.DrawColumn($"{character.CollectionId}");
            table.DrawColumn(character.CharacterAddress);
            table.DrawColumn($"{character.Timestamp}");
        }
    }

    public static void DrawFiles(this CrashData data)
    {
        using var tree = Im.Tree.Node("Last Files"u8);
        if (!tree)
            return;

        using var table = Im.Table.Begin("##filesTable"u8, 8,
            TableFlags.SizingFixedFit | TableFlags.RowBackground | TableFlags.BordersInner);
        if (!table)
            return;

        using var clipper = new Im.ListClipper(data.LastModdedFilesLoaded.Count, Im.Style.TextHeightWithSpacing);
        foreach (var file in clipper.Iterate(data.LastModdedFilesLoaded))
        {
            table.DrawColumn($"{file.Age}");
            table.DrawColumn($"{file.ThreadId}");
            table.DrawColumn(file.ActualFileName);
            table.DrawColumn(file.RequestedFileName);
            table.DrawColumn(file.CharacterName);
            table.DrawColumn($"{file.CollectionId}");
            table.DrawColumn(file.CharacterAddress);
            table.DrawColumn($"{file.Timestamp}");
        }
    }

    public static void DrawVfxInvocations(this CrashData data)
    {
        using var tree = Im.Tree.Node("Last VFX Invocations"u8);
        if (!tree)
            return;

        using var table = Im.Table.Begin("##vfxTable"u8, 7,
            TableFlags.SizingFixedFit | TableFlags.RowBackground | TableFlags.BordersInner);
        if (!table)
            return;

        using var clipper = new Im.ListClipper(data.LastVFXFuncsInvoked.Count, Im.Style.TextHeightWithSpacing);
        foreach (var vfx in clipper.Iterate(data.LastVFXFuncsInvoked))
        {
            table.DrawColumn($"{vfx.Age}");
            table.DrawColumn($"{vfx.ThreadId}");
            table.DrawColumn(vfx.InvocationType);
            table.DrawColumn(vfx.CharacterName);
            table.DrawColumn($"{vfx.CollectionId}");
            table.DrawColumn(vfx.CharacterAddress);
            table.DrawColumn($"{vfx.Timestamp}");
        }
    }
}
