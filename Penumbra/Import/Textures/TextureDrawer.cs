using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using Lumina.Data.Files;
using OtterGui;
using OtterGui.Raii;
using OtterTex;
using Penumbra.String.Classes;
using Penumbra.UI;
using Penumbra.UI.Classes;

namespace Penumbra.Import.Textures;

public static class TextureDrawer
{
    public static void Draw(Texture texture, Vector2 size)
    {
        if (texture.TextureWrap != null)
        {
            size = size.X < texture.TextureWrap.Width
                ? size with { Y = texture.TextureWrap.Height * size.X / texture.TextureWrap.Width }
                : new Vector2(texture.TextureWrap.Width, texture.TextureWrap.Height);

            ImGui.Image(texture.TextureWrap.ImGuiHandle, size);
            DrawData(texture);
        }
        else if (texture.LoadError != null)
        {
            ImGui.TextUnformatted("Could not load file:");
            ImGuiUtil.TextColored(Colors.RegexWarningBorder, texture.LoadError.ToString());
        }
    }

    public static void PathSelectBox(TextureManager textures, Texture current, string label, string tooltip, IEnumerable<(string, bool)> paths,
        int skipPrefix)
    {
        ImGui.SetNextItemWidth(-0.0001f);
        var       startPath = current.Path.Length > 0 ? current.Path : "Choose a modded texture from this mod here...";
        using var combo     = ImRaii.Combo(label, startPath);
        if (combo)
            foreach (var ((path, game), idx) in paths.WithIndex())
            {
                if (game)
                {
                    if (!textures.GameFileExists(path))
                        continue;
                }
                else if (!File.Exists(path))
                {
                    continue;
                }

                using var id = ImRaii.PushId(idx);
                using (var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.FolderExpanded.Value(), game))
                {
                    var p = game ? $"--> {path}" : path[skipPrefix..];
                    if (ImGui.Selectable(p, path == startPath) && path != startPath)
                        current.Load(textures, path);
                }

                ImGuiUtil.HoverTooltip(game
                    ? "This is a game path and refers to an unmanipulated file from your game data."
                    : "This is a path to a modded file on your file system.");
            }

        ImGuiUtil.HoverTooltip(tooltip);
    }

    public static void PathInputBox(TextureManager textures, Texture current, ref string? tmpPath, string label, string hint, string tooltip,
        string startPath, FileDialogService fileDialog, string defaultModImportPath)
    {
        tmpPath ??= current.Path;
        using var spacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing,
            new Vector2(UiHelpers.ScaleX3, ImGui.GetStyle().ItemSpacing.Y));
        ImGui.SetNextItemWidth(-2 * ImGui.GetFrameHeight() - 7 * UiHelpers.Scale);
        ImGui.InputTextWithHint(label, hint, ref tmpPath, Utf8GamePath.MaxGamePathLength);
        if (ImGui.IsItemDeactivatedAfterEdit())
            current.Load(textures, tmpPath);

        ImGuiUtil.HoverTooltip(tooltip);
        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Folder.ToIconString(), new Vector2(ImGui.GetFrameHeight()), string.Empty, false,
                true))
        {
            if (defaultModImportPath.Length > 0)
                startPath = defaultModImportPath;

            void UpdatePath(bool success, List<string> paths)
            {
                if (success && paths.Count > 0)
                    current.Load(textures, paths[0]);
            }

            fileDialog.OpenFilePicker("Open Image...", "Textures{.png,.dds,.tex}", UpdatePath, 1, startPath, false);
        }

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Recycle.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
                "Reload the currently selected path.", false,
                true))
            current.Reload(textures);
    }

    private static void DrawData(Texture texture)
    {
        using var table = ImRaii.Table("##data", 2, ImGuiTableFlags.SizingFixedFit);
        ImGuiUtil.DrawTableColumn("Width");
        ImGuiUtil.DrawTableColumn(texture.TextureWrap!.Width.ToString());
        ImGuiUtil.DrawTableColumn("Height");
        ImGuiUtil.DrawTableColumn(texture.TextureWrap!.Height.ToString());
        ImGuiUtil.DrawTableColumn("File Type");
        ImGuiUtil.DrawTableColumn(texture.Type.ToString());
        ImGuiUtil.DrawTableColumn("Bitmap Size");
        ImGuiUtil.DrawTableColumn($"{Functions.HumanReadableSize(texture.RgbaPixels.Length)} ({texture.RgbaPixels.Length} Bytes)");
        switch (texture.BaseImage.Image)
        {
            case ScratchImage s:
                ImGuiUtil.DrawTableColumn("Format");
                ImGuiUtil.DrawTableColumn(s.Meta.Format.ToString());
                ImGuiUtil.DrawTableColumn("Mip Levels");
                ImGuiUtil.DrawTableColumn(s.Meta.MipLevels.ToString());
                ImGuiUtil.DrawTableColumn("Data Size");
                ImGuiUtil.DrawTableColumn($"{Functions.HumanReadableSize(s.Pixels.Length)} ({s.Pixels.Length} Bytes)");
                ImGuiUtil.DrawTableColumn("Number of Images");
                ImGuiUtil.DrawTableColumn(s.Images.Length.ToString());
                break;
            case TexFile t:
                ImGuiUtil.DrawTableColumn("Format");
                ImGuiUtil.DrawTableColumn(t.Header.Format.ToString());
                ImGuiUtil.DrawTableColumn("Mip Levels");
                ImGuiUtil.DrawTableColumn(t.Header.MipLevels.ToString());
                ImGuiUtil.DrawTableColumn("Data Size");
                ImGuiUtil.DrawTableColumn($"{Functions.HumanReadableSize(t.ImageData.Length)} ({t.ImageData.Length} Bytes)");
                break;
        }
    }
}
