using System;
using System.IO;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
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

        tex.PathInputBox("##input", "Import Image...", "Can import game paths as well as your own files.", _mod!.ModPath.FullName,
            _fileDialog);
        var files = _editor.Files.Tex.SelectMany(f => f.SubModUsage.Select(p => (p.Item2.ToString(), true))
            .Prepend((f.File.FullName, false)));
        tex.PathSelectBox("##combo", "Select the textures included in this mod on your drive or the ones they replace from the game files.",
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
                }, _mod!.ModPath.FullName, false);
            }

            if (ImGui.Button("Save as DDS", -Vector2.UnitX))
            {
                var fileName = Path.GetFileNameWithoutExtension(_right.Path.Length > 0 ? _right.Path : _left.Path);
                _fileDialog.OpenSavePicker("Save Texture as DDS...", ".dds", fileName, ".dds", (a, b) =>
                {
                    if (a)
                        _center.SaveAsDds(b, (CombinedTexture.TextureSaveType)_currentSaveAs, _addMipMaps);
                }, _mod!.ModPath.FullName, false);
            }

            ImGui.NewLine();

            if (ImGui.Button("Save as PNG", -Vector2.UnitX))
            {
                var fileName = Path.GetFileNameWithoutExtension(_right.Path.Length > 0 ? _right.Path : _left.Path);
                _fileDialog.OpenSavePicker("Save Texture as PNG...", ".png", fileName, ".png", (a, b) =>
                {
                    if (a)
                        _center.SaveAsPng(b);
                }, _mod!.ModPath.FullName, false);
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
            _center.Draw(imageSize);
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
        using var tab = ImRaii.TabItem("Texture Import/Export");
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
