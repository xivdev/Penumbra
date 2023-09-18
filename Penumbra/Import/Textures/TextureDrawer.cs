using Dalamud.Interface;
using ImGuiNET;
using Lumina.Data.Files;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using OtterTex;
using Penumbra.Mods.Editor;
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

    public sealed class PathSelectCombo : FilterComboCache<(string, bool)>
    {
        private int _skipPrefix = 0;

        public PathSelectCombo(TextureManager textures, ModEditor editor)
            : base(() => CreateFiles(textures, editor))
        { }

        protected override string ToString((string, bool) obj)
            => obj.Item1;

        protected override bool DrawSelectable(int globalIdx, bool selected)
        {
            var (path, game) = Items[globalIdx];
            bool ret;
            using (var color = ImRaii.PushColor(ImGuiCol.Text, ColorId.FolderExpanded.Value(), game))
            {
                var equals = string.Equals(CurrentSelection.Item1, path, StringComparison.OrdinalIgnoreCase);
                var p      = game ? $"--> {path}" : path[_skipPrefix..];
                ret = ImGui.Selectable(p, selected) && !equals;
            }

            ImGuiUtil.HoverTooltip(game
                ? "This is a game path and refers to an unmanipulated file from your game data."
                : "This is a path to a modded file on your file system.");
            return ret;
        }

        private static IReadOnlyList<(string, bool)> CreateFiles(TextureManager textures, ModEditor editor)
            => editor.Files.Tex.SelectMany(f => f.SubModUsage.Select(p => (p.Item2.ToString(), true))
                    .Prepend((f.File.FullName, false)))
                .Where(p => p.Item2 ? textures.GameFileExists(p.Item1) : File.Exists(p.Item1))
                .ToList();

        public bool Draw(string label, string tooltip, string current, int skipPrefix, out string newPath)
        {
            _skipPrefix = skipPrefix;
            var startPath = current.Length > 0 ? current : "Choose a modded texture from this mod here...";
            if (!Draw(label, startPath, tooltip, -0.0001f, ImGui.GetTextLineHeightWithSpacing()))
            {
                newPath = current;
                return false;
            }

            newPath = CurrentSelection.Item1;
            return true;
        }
    }
}
