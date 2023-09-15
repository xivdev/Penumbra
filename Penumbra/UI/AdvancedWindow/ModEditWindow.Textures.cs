using System.Diagnostics.CodeAnalysis;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterTex;
using Penumbra.Import.Textures;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly TextureManager _textures;

    private readonly Texture                       _left  = new();
    private readonly Texture                       _right = new();
    private readonly CombinedTexture               _center;
    private readonly TextureDrawer.PathSelectCombo _textureSelectCombo;

    private bool _overlayCollapsed = true;
    private bool _addMipMaps       = true;
    private int  _currentSaveAs;

    private static readonly (string, string)[] SaveAsStrings =
    {
        ("As Is", "Save the current texture with its own format without additional conversion or compression, if possible."),
        ("RGBA (Uncompressed)",
            "Save the current texture as an uncompressed BGRA bitmap. This requires the most space but technically offers the best quality."),
        ("BC3 (Simple Compression)",
            "Save the current texture compressed via BC3/DXT5 compression. This offers a 4:1 compression ratio and is quick with acceptable quality."),
        ("BC7 (Complex Compression)",
            "Save the current texture compressed via BC7 compression. This offers a 4:1 compression ratio and has almost indistinguishable quality, but may take a while."),
    };

    private void DrawInputChild(string label, Texture tex, Vector2 size, Vector2 imageSize)
    {
        using (var child = ImRaii.Child(label, size, true))
        {
            if (!child)
                return;

            using var id = ImRaii.PushId(label);
            ImGuiUtil.DrawTextButton(label, new Vector2(-1, 0), ImGui.GetColorU32(ImGuiCol.FrameBg));
            ImGui.NewLine();

            using (var disabled = ImRaii.Disabled(!_center.SaveTask.IsCompleted))
            {
                TextureDrawer.PathInputBox(_textures, tex, ref tex.TmpPath, "##input", "Import Image...",
                    "Can import game paths as well as your own files.", _mod!.ModPath.FullName, _fileDialog, _config.DefaultModImportPath);
                if (_textureSelectCombo.Draw("##combo",
                        "Select the textures included in this mod on your drive or the ones they replace from the game files.", tex.Path,
                        _mod.ModPath.FullName.Length + 1, out var newPath)
                 && newPath != tex.Path)
                    tex.Load(_textures, newPath);

                if (tex == _left)
                    _center.DrawMatrixInputLeft(size.X);
                else
                    _center.DrawMatrixInputRight(size.X);
            }

            ImGui.NewLine();
            using var child2 = ImRaii.Child("image");
            if (child2)
                TextureDrawer.Draw(tex, imageSize);
        }

        if (_dragDropManager.CreateImGuiTarget("TextureDragDrop", out var files, out _) && GetFirstTexture(files, out var file))
            tex.Load(_textures, file);
    }

    private void SaveAsCombo()
    {
        var (text, desc) = SaveAsStrings[_currentSaveAs];
        ImGui.SetNextItemWidth(-ImGui.GetFrameHeight() - ImGui.GetStyle().ItemSpacing.X);
        using var combo = ImRaii.Combo("##format", text);
        ImGuiUtil.HoverTooltip(desc);
        if (!combo)
            return;

        foreach (var ((newText, newDesc), idx) in SaveAsStrings.WithIndex())
        {
            if (ImGui.Selectable(newText, idx == _currentSaveAs))
                _currentSaveAs = idx;

            ImGuiUtil.SelectableHelpMarker(newDesc);
        }
    }

    private void MipMapInput()
    {
        ImGui.Checkbox("##mipMaps", ref _addMipMaps);
        ImGuiUtil.HoverTooltip(
            "Add the appropriate number of MipMaps to the file.");
    }

    private bool _forceTextureStartPath = true;

    private void DrawOutputChild(Vector2 size, Vector2 imageSize)
    {
        using var child = ImRaii.Child("Output", size, true);
        if (!child)
            return;

        if (_center.IsLoaded)
        {
            SaveAsCombo();
            ImGui.SameLine();
            MipMapInput();

            var canSaveInPlace = Path.IsPathRooted(_left.Path) && _left.Type is TextureType.Tex or TextureType.Dds or TextureType.Png;

            var buttonSize2 = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);
            if (ImGuiUtil.DrawDisabledButton("Save in place", buttonSize2,
                    "This saves the texture in place. This is not revertible.",
                    !canSaveInPlace || _center.IsLeftCopy && _currentSaveAs == (int)CombinedTexture.TextureSaveType.AsIs))
            {
                _center.SaveAs(_left.Type, _textures, _left.Path, (CombinedTexture.TextureSaveType)_currentSaveAs, _addMipMaps);
                AddReloadTask(_left.Path, false);
            }

            ImGui.SameLine();
            if (ImGui.Button("Save as TEX", buttonSize2))
                OpenSaveAsDialog(".tex");

            if (ImGui.Button("Export as PNG", buttonSize2))
                OpenSaveAsDialog(".png");
            ImGui.SameLine();
            if (ImGui.Button("Export as DDS", buttonSize2))
                OpenSaveAsDialog(".dds");

            ImGui.NewLine();

            var canConvertInPlace = canSaveInPlace && _left.Type is TextureType.Tex && _center.IsLeftCopy;

            var buttonSize3 = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X * 2) / 3, 0);
            if (ImGuiUtil.DrawDisabledButton("Convert to BC7", buttonSize3,
                    "This converts the texture to BC7 format in place. This is not revertible.",
                    !canConvertInPlace || _left.Format is DXGIFormat.BC7Typeless or DXGIFormat.BC7UNorm or DXGIFormat.BC7UNormSRGB))
            {
                _center.SaveAsTex(_textures, _left.Path, CombinedTexture.TextureSaveType.BC7, _left.MipMaps > 1);
                AddReloadTask(_left.Path, false);
            }

            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton("Convert to BC3", buttonSize3,
                    "This converts the texture to BC3 format in place. This is not revertible.",
                    !canConvertInPlace || _left.Format is DXGIFormat.BC3Typeless or DXGIFormat.BC3UNorm or DXGIFormat.BC3UNormSRGB))
            {
                _center.SaveAsTex(_textures, _left.Path, CombinedTexture.TextureSaveType.BC3, _left.MipMaps > 1);
                AddReloadTask(_left.Path, false);
            }

            ImGui.SameLine();
            if (ImGuiUtil.DrawDisabledButton("Convert to RGBA", buttonSize3,
                    "This converts the texture to RGBA format in place. This is not revertible.",
                    !canConvertInPlace
                 || _left.Format is DXGIFormat.B8G8R8A8UNorm or DXGIFormat.B8G8R8A8Typeless or DXGIFormat.B8G8R8A8UNormSRGB))
            {
                _center.SaveAsTex(_textures, _left.Path, CombinedTexture.TextureSaveType.Bitmap, _left.MipMaps > 1);
                AddReloadTask(_left.Path, false);
            }
        }

        switch (_center.SaveTask.Status)
        {
            case TaskStatus.WaitingForActivation:
            case TaskStatus.WaitingToRun:
            case TaskStatus.Running:
                ImGuiUtil.DrawTextButton("Computing...", -Vector2.UnitX, Colors.PressEnterWarningBg);

                break;
            case TaskStatus.Canceled:
            case TaskStatus.Faulted:
            {
                ImGui.TextUnformatted("Could not save file:");
                using var color = ImRaii.PushColor(ImGuiCol.Text, 0xFF0000FF);
                ImGuiUtil.TextWrapped(_center.SaveTask.Exception?.ToString() ?? "Unknown Error");
                break;
            }
            default:
                ImGui.Dummy(new Vector2(1, ImGui.GetFrameHeight()));
                break;
        }

        ImGui.NewLine();

        using var child2 = ImRaii.Child("image");
        if (child2)
            _center.Draw(_textures, imageSize);
    }

    private void OpenSaveAsDialog(string defaultExtension)
    {
        var fileName = Path.GetFileNameWithoutExtension(_left.Path.Length > 0 ? _left.Path : _right.Path);
        _fileDialog.OpenSavePicker("Save Texture as TEX, DDS or PNG...", "Textures{.png,.dds,.tex},.tex,.dds,.png", fileName, defaultExtension, (a, b) =>
        {
            if (a)
            {
                _center.SaveAs(null, _textures, b, (CombinedTexture.TextureSaveType)_currentSaveAs, _addMipMaps);
                if (b == _left.Path)
                    AddReloadTask(_left.Path, false);
                else if (b == _right.Path)
                    AddReloadTask(_right.Path, true);
            }
        }, _mod!.ModPath.FullName, _forceTextureStartPath);
        _forceTextureStartPath = false;
    }

    private void AddReloadTask(string path, bool right)
    {
        _center.SaveTask.ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully)
                return;

            var tex = right ? _right : _left;

            if (tex.Path != path)
                return;

            _dalamud.Framework.RunOnFrameworkThread(() => tex.Reload(_textures));
        });
    }

    private Vector2 GetChildWidth()
    {
        var windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - ImGui.GetTextLineHeight();
        if (_overlayCollapsed)
        {
            var width = windowWidth - ImGui.GetStyle().FramePadding.X * 3;
            return new Vector2(width / 2, -1);
        }

        return new Vector2((windowWidth - ImGui.GetStyle().FramePadding.X * 5) / 3, -1);
    }

    private void DrawTextureTab()
    {
        using var tab = ImRaii.TabItem("Textures");
        if (!tab)
            return;

        try
        {
            _dragDropManager.CreateImGuiSource("TextureDragDrop",
                m => m.Extensions.Any(e => ValidTextureExtensions.Contains(e.ToLowerInvariant())), m =>
                {
                    if (!GetFirstTexture(m.Files, out var file))
                        return false;

                    ImGui.TextUnformatted($"Dragging texture for editing: {Path.GetFileName(file)}");
                    return true;
                });
            var childWidth = GetChildWidth();
            var imageSize  = new Vector2(childWidth.X - ImGui.GetStyle().FramePadding.X * 2);
            DrawInputChild("Input Texture", _left, childWidth, imageSize);
            ImGui.SameLine();
            DrawOutputChild(childWidth, imageSize);
            if (!_overlayCollapsed)
            {
                ImGui.SameLine();
                DrawInputChild("Overlay Texture", _right, childWidth, imageSize);
            }

            ImGui.SameLine();
            DrawOverlayCollapseButton();
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Unknown Error while drawing textures:\n{e}");
        }
    }

    private void DrawOverlayCollapseButton()
    {
        var (label, tooltip) = _overlayCollapsed
            ? (">", "Show a third panel in which you can import an additional texture as an overlay for the primary texture.")
            : ("<", "Hide the overlay texture panel and clear the currently loaded overlay texture, if any.");
        if (ImGui.Button(label, new Vector2(ImGui.GetTextLineHeight(), ImGui.GetContentRegionAvail().Y)))
            _overlayCollapsed = !_overlayCollapsed;

        ImGuiUtil.HoverTooltip(tooltip);
    }

    private static bool GetFirstTexture(IEnumerable<string> files, [NotNullWhen(true)] out string? file)
    {
        file = files.FirstOrDefault(f => ValidTextureExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        return file != null;
    }

    private static readonly string[] ValidTextureExtensions =
    {
        ".png",
        ".dds",
        ".tex",
    };
}
