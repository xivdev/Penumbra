using System;
using System.IO;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterTex;
using Penumbra.Import.Textures;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly Texture         _left  = new();
    private readonly Texture         _right = new();
    private readonly CombinedTexture _center;

    private bool _overlayCollapsed = true;

    private bool _addMipMaps = true;
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
        using var child = ImRaii.Child(label, size, true);
        if (!child)
            return;

        using var id = ImRaii.PushId(label);
        ImGuiUtil.DrawTextButton(label, new Vector2(-1, 0), ImGui.GetColorU32(ImGuiCol.FrameBg));
        ImGui.NewLine();

        tex.PathInputBox(_dalamud, "##input", "Import Image...", "Can import game paths as well as your own files.", _mod!.ModPath.FullName,
            _fileDialog, _config.DefaultModImportPath);
        var files = _editor.Files.Tex.SelectMany(f => f.SubModUsage.Select(p => (p.Item2.ToString(), true))
            .Prepend((f.File.FullName, false)));
        tex.PathSelectBox(_dalamud, "##combo",
            "Select the textures included in this mod on your drive or the ones they replace from the game files.",
            files, _mod.ModPath.FullName.Length + 1);

        if (tex == _left)
            _center.DrawMatrixInputLeft(size.X);
        else
            _center.DrawMatrixInputRight(size.X);

        ImGui.NewLine();
        using var child2 = ImRaii.Child("image");
        if (child2)
            tex.Draw(imageSize);
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

            ImGuiUtil.HoverTooltip(newDesc);
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
            if (ImGui.Button("Save as TEX", -Vector2.UnitX))
            {
                var fileName = Path.GetFileNameWithoutExtension(_left.Path.Length > 0 ? _left.Path : _right.Path);
                _fileDialog.OpenSavePicker("Save Texture as TEX...", ".tex", fileName, ".tex", (a, b) =>
                {
                    if (a)
                        _center.SaveAsTex(b, (CombinedTexture.TextureSaveType)_currentSaveAs, _addMipMaps);
                }, _mod!.ModPath.FullName, _forceTextureStartPath);
                _forceTextureStartPath = false;
            }

            if (ImGui.Button("Save as DDS", -Vector2.UnitX))
            {
                var fileName = Path.GetFileNameWithoutExtension(_right.Path.Length > 0 ? _right.Path : _left.Path);
                _fileDialog.OpenSavePicker("Save Texture as DDS...", ".dds", fileName, ".dds", (a, b) =>
                {
                    if (a)
                        _center.SaveAsDds(b, (CombinedTexture.TextureSaveType)_currentSaveAs, _addMipMaps);
                }, _mod!.ModPath.FullName, _forceTextureStartPath);
                _forceTextureStartPath = false;
            }

            ImGui.NewLine();

            if (ImGui.Button("Save as PNG", -Vector2.UnitX))
            {
                var fileName = Path.GetFileNameWithoutExtension(_right.Path.Length > 0 ? _right.Path : _left.Path);
                _fileDialog.OpenSavePicker("Save Texture as PNG...", ".png", fileName, ".png", (a, b) =>
                {
                    if (a)
                        _center.SaveAsPng(b);
                }, _mod!.ModPath.FullName, _forceTextureStartPath);
                _forceTextureStartPath = false;
            }

            if (_left.Type is Texture.FileType.Tex && _center.IsLeftCopy)
            {
                var buttonSize = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X * 2) / 3, 0);
                if (ImGuiUtil.DrawDisabledButton("Convert to BC7", buttonSize,
                        "This converts the texture to BC7 format in place. This is not revertible.",
                        _left.Format is DXGIFormat.BC7Typeless or DXGIFormat.BC7UNorm or DXGIFormat.BC7UNormSRGB))
                {
                    _center.SaveAsTex(_left.Path, CombinedTexture.TextureSaveType.BC7, _left.MipMaps > 1);
                    _left.Reload(_dalamud);
                }

                ImGui.SameLine();
                if (ImGuiUtil.DrawDisabledButton("Convert to BC3", buttonSize,
                        "This converts the texture to BC3 format in place. This is not revertible.",
                        _left.Format is DXGIFormat.BC3Typeless or DXGIFormat.BC3UNorm or DXGIFormat.BC3UNormSRGB))
                {
                    _center.SaveAsTex(_left.Path, CombinedTexture.TextureSaveType.BC3, _left.MipMaps > 1);
                    _left.Reload(_dalamud);
                }

                ImGui.SameLine();
                if (ImGuiUtil.DrawDisabledButton("Convert to RGBA", buttonSize,
                        "This converts the texture to RGBA format in place. This is not revertible.",
                        _left.Format is DXGIFormat.B8G8R8A8UNorm or DXGIFormat.B8G8R8A8Typeless or DXGIFormat.B8G8R8A8UNormSRGB))
                {
                    _center.SaveAsTex(_left.Path, CombinedTexture.TextureSaveType.Bitmap, _left.MipMaps > 1);
                    _left.Reload(_dalamud);
                }
            }
            else
            {
                ImGui.NewLine();
            }
            ImGui.NewLine();
        }

        if (_center.SaveException != null)
        {
            ImGui.TextUnformatted("Could not save file:");
            using var color = ImRaii.PushColor(ImGuiCol.Text, 0xFF0000FF);
            ImGuiUtil.TextWrapped(_center.SaveException.ToString());
        }

        using var child2 = ImRaii.Child("image");
        if (child2)
            _center.Draw(_dalamud.UiBuilder, imageSize);
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
}
