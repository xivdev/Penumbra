using ImSharp;
using Lumina.Data.Files;
using Luna;
using OtterTex;
using Penumbra.UI;
using Penumbra.UI.Classes;
using VectorExtensions = Luna.VectorExtensions;

namespace Penumbra.Import.Textures;

public static class TextureDrawer
{
    public static void Draw(Texture texture, Vector2 size)
    {
        if (texture.TextureWrap != null)
        {
            size = VectorExtensions.Contain(texture.TextureWrap.Size, size);

            Im.Image.Draw(texture.TextureWrap.Id(), size);
            DrawData(texture);
        }
        else if (texture.LoadError != null)
        {
            const string link = "https://aka.ms/vcredist";
            Im.Text("Could not load file:"u8);

            if (texture.LoadError is DllNotFoundException)
            {
                Im.Text("A texture handling dependency could not be found. Try installing a current Microsoft VC Redistributable."u8,
                    Colors.RegexWarningBorder);
                if (Im.Button("Microsoft VC Redistributables"u8))
                    Dalamud.Utility.Util.OpenLink(link);
                Im.Tooltip.OnHover($"Open {link} in your browser.");
            }

            Im.Text($"{texture.LoadError}", Colors.RegexWarningBorder);
        }
    }

    public static void PathInputBox(TextureManager textures, Texture current, ref string? tmpPath, ReadOnlySpan<byte> label,
        ReadOnlySpan<byte> hint, ReadOnlySpan<byte> tooltip,
        string startPath, FileDialogService fileDialog, string defaultModImportPath)
    {
        tmpPath ??= current.Path;
        using var spacing = ImStyleDouble.ItemSpacing.PushX(UiHelpers.ScaleX3);
        Im.Item.SetNextWidth(-2 * Im.Style.FrameHeight - 7 * Im.Style.GlobalScale);
        if (ImEx.InputOnDeactivation.Text(label, tmpPath, out tmpPath, hint))
            current.Load(textures, tmpPath);

        Im.Tooltip.OnHover(tooltip);
        Im.Line.Same();
        if (ImEx.Icon.Button(LunaStyle.FolderIcon))
        {
            if (defaultModImportPath.Length > 0)
                startPath = defaultModImportPath;

            void UpdatePath(bool success, List<string> paths)
            {
                if (success && paths.Count > 0)
                    current.Load(textures, paths[0]);
            }

            fileDialog.OpenFilePicker("Open Image...", "Textures{.png,.dds,.tex,.tga}", UpdatePath, 1, startPath, false);
        }

        Im.Line.Same();
        if (ImEx.Icon.Button(LunaStyle.RefreshIcon, "Reload the currently selected path."u8))
            current.Reload(textures);
    }

    private static void DrawData(Texture texture)
    {
        using var table = Im.Table.Begin("##data"u8, 2, TableFlags.SizingFixedFit);
        table.DrawColumn("Width"u8);
        table.DrawColumn($"{texture.TextureWrap!.Width}");
        table.DrawColumn("Height"u8);
        table.DrawColumn($"{texture.TextureWrap!.Height}");
        table.DrawColumn("File Type"u8);
        table.DrawColumn($"{texture.Type}");
        table.DrawColumn("Bitmap Size"u8);
        table.DrawColumn($"{FormattingFunctions.HumanReadableSize(texture.RgbaPixels.Length)} ({texture.RgbaPixels.Length} Bytes)");
        switch (texture.BaseImage.Image)
        {
            case ScratchImage s:
                table.DrawColumn("Format"u8);
                table.DrawColumn($"{s.Meta.Format}");
                table.DrawColumn("Mip Levels"u8);
                table.DrawColumn($"{s.Meta.MipLevels}");
                table.DrawColumn("Data Size"u8);
                table.DrawColumn($"{FormattingFunctions.HumanReadableSize(s.Pixels.Length)} ({s.Pixels.Length} Bytes)");
                table.DrawColumn("Number of Images"u8);
                table.DrawColumn($"{s.Images.Length}");
                break;
            case TexFile t:
                table.DrawColumn("Format"u8);
                table.DrawColumn($"{t.Header.Format}");
                table.DrawColumn("Mip Levels"u8);
                table.DrawColumn($"{t.Header.MipCount}");
                table.DrawColumn("Data Size"u8);
                table.DrawColumn($"{FormattingFunctions.HumanReadableSize(t.ImageData.Length)} ({t.ImageData.Length} Bytes)");
                break;
        }
    }
}
