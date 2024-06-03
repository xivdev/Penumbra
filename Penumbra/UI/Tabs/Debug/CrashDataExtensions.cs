using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.CrashHandler;

namespace Penumbra.UI.Tabs.Debug;

public static class CrashDataExtensions
{
    public static void DrawMeta(this CrashData data)
    {
        using (ImRaii.Group())
        {
            ImGui.TextUnformatted(nameof(data.Mode));
            ImGui.TextUnformatted(nameof(data.CrashTime));
            ImGui.TextUnformatted("Current Age");
            ImGui.TextUnformatted(nameof(data.Version));
            ImGui.TextUnformatted(nameof(data.GameVersion));
            ImGui.TextUnformatted(nameof(data.ExitCode));
            ImGui.TextUnformatted(nameof(data.ProcessId));
            ImGui.TextUnformatted(nameof(data.TotalModdedFilesLoaded));
            ImGui.TextUnformatted(nameof(data.TotalCharactersLoaded));
            ImGui.TextUnformatted(nameof(data.TotalVFXFuncsInvoked));
        }

        ImGui.SameLine();
        using (ImRaii.Group())
        {
            ImGui.TextUnformatted(data.Mode);
            ImGui.TextUnformatted(data.CrashTime.ToString());
            ImGui.TextUnformatted((DateTimeOffset.UtcNow - data.CrashTime).ToString(@"dd\.hh\:mm\:ss"));
            ImGui.TextUnformatted(data.Version);
            ImGui.TextUnformatted(data.GameVersion);
            ImGui.TextUnformatted(data.ExitCode.ToString());
            ImGui.TextUnformatted(data.ProcessId.ToString());
            ImGui.TextUnformatted(data.TotalModdedFilesLoaded.ToString());
            ImGui.TextUnformatted(data.TotalCharactersLoaded.ToString());
            ImGui.TextUnformatted(data.TotalVFXFuncsInvoked.ToString());
        }
    }

    public static void DrawCharacters(this CrashData data)
    {
        using var tree = ImRaii.TreeNode("Last Characters");
        if (!tree)
            return;

        using var table = ImRaii.Table("##characterTable", 6,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInner);
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
        }, ImGui.GetTextLineHeightWithSpacing());
    }

    public static void DrawFiles(this CrashData data)
    {
        using var tree = ImRaii.TreeNode("Last Files");
        if (!tree)
            return;

        using var table = ImRaii.Table("##filesTable", 8,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInner);
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
        }, ImGui.GetTextLineHeightWithSpacing());
    }

    public static void DrawVfxInvocations(this CrashData data)
    {
        using var tree = ImRaii.TreeNode("Last VFX Invocations");
        if (!tree)
            return;

        using var table = ImRaii.Table("##vfxTable", 7,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInner);
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
        }, ImGui.GetTextLineHeightWithSpacing());
    }
}
