using Dalamud.Bindings.ImGui;
using ImSharp;
using OtterGui;
using OtterGui.Raii;
using Penumbra.CrashHandler;

namespace Penumbra.UI.Tabs.Debug;

public static class CrashDataExtensions
{
    public static void DrawMeta(this CrashData data)
    {
        using (Im.Group())
        {
            Im.Text(nameof(data.Mode));
            Im.Text(nameof(data.CrashTime));
            Im.Text("Current Age"u8);
            Im.Text(nameof(data.Version));
            Im.Text(nameof(data.GameVersion));
            Im.Text(nameof(data.ExitCode));
            Im.Text(nameof(data.ProcessId));
            Im.Text(nameof(data.TotalModdedFilesLoaded));
            Im.Text(nameof(data.TotalCharactersLoaded));
            Im.Text(nameof(data.TotalVFXFuncsInvoked));
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
        using var tree = ImRaii.TreeNode("Last Characters");
        if (!tree)
            return;

        using var table = Im.Table.Begin("##characterTable"u8, 6,
            TableFlags.SizingFixedFit | TableFlags.RowBackground | TableFlags.BordersInner);
        if (!table)
            return;

        ImGuiClip.ClippedDraw(data.LastCharactersLoaded, character =>
        {
            ImGuiUtil.DrawTableColumn(character.Age.ToString(CultureInfo.InvariantCulture));
            ImGuiUtil.DrawTableColumn(character.ThreadId.ToString());
            ImGuiUtil.DrawTableColumn(character.CharacterName);
            ImGuiUtil.DrawTableColumn(character.CollectionId.ToString());
            ImGuiUtil.DrawTableColumn(character.CharacterAddress);
            ImGuiUtil.DrawTableColumn(character.Timestamp.ToString());
        }, Im.Style.TextHeightWithSpacing);
    }

    public static void DrawFiles(this CrashData data)
    {
        using var tree = ImRaii.TreeNode("Last Files");
        if (!tree)
            return;

        using var table = Im.Table.Begin("##filesTable"u8, 8,
            TableFlags.SizingFixedFit | TableFlags.RowBackground | TableFlags.BordersInner);
        if (!table)
            return;

        ImGuiClip.ClippedDraw(data.LastModdedFilesLoaded, file =>
        {
            ImGuiUtil.DrawTableColumn(file.Age.ToString(CultureInfo.InvariantCulture));
            ImGuiUtil.DrawTableColumn(file.ThreadId.ToString());
            ImGuiUtil.DrawTableColumn(file.ActualFileName);
            ImGuiUtil.DrawTableColumn(file.RequestedFileName);
            ImGuiUtil.DrawTableColumn(file.CharacterName);
            ImGuiUtil.DrawTableColumn(file.CollectionId.ToString());
            ImGuiUtil.DrawTableColumn(file.CharacterAddress);
            ImGuiUtil.DrawTableColumn(file.Timestamp.ToString());
        }, Im.Style.TextHeightWithSpacing);
    }

    public static void DrawVfxInvocations(this CrashData data)
    {
        using var tree = ImRaii.TreeNode("Last VFX Invocations");
        if (!tree)
            return;

        using var table = Im.Table.Begin("##vfxTable"u8, 7,
            TableFlags.SizingFixedFit | TableFlags.RowBackground | TableFlags.BordersInner);
        if (!table)
            return;

        ImGuiClip.ClippedDraw(data.LastVFXFuncsInvoked, vfx =>
        {
            ImGuiUtil.DrawTableColumn(vfx.Age.ToString(CultureInfo.InvariantCulture));
            ImGuiUtil.DrawTableColumn(vfx.ThreadId.ToString());
            ImGuiUtil.DrawTableColumn(vfx.InvocationType);
            ImGuiUtil.DrawTableColumn(vfx.CharacterName);
            ImGuiUtil.DrawTableColumn(vfx.CollectionId.ToString());
            ImGuiUtil.DrawTableColumn(vfx.CharacterAddress);
            ImGuiUtil.DrawTableColumn(vfx.Timestamp.ToString());
        }, Im.Style.TextHeightWithSpacing);
    }
}
