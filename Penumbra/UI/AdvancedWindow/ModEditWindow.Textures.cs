using ImSharp;
using Luna;
using OtterTex;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly TextureManager _textures;

    private readonly Texture            _left  = new();
    private readonly Texture            _right = new();
    private readonly CombinedTexture    _center;
    private readonly TextureSelectCombo _textureSelectCombo;

    private bool _overlayCollapsed = true;
    private bool _addMipMaps       = true;
    private int  _currentSaveAs;

    private static readonly (string, string)[] SaveAsStrings =
    {
        ("As Is", "Save the current texture with its own format without additional conversion or compression, if possible."),
        ("RGBA (Uncompressed)",
            "Save the current texture as an uncompressed BGRA bitmap.\nThis requires the most space but technically offers the best quality."),
        ("BC1 (Simple Compression for Opaque RGB)",
            "Save the current texture compressed via BC1/DXT1 compression.\nThis offers a 8:1 compression ratio and is quick with acceptable quality, but only supports RGB, without Alpha.\n\nCan be used for diffuse maps and equipment textures to save extra space."),
        ("BC3 (Simple Compression for RGBA)",
            "Save the current texture compressed via BC3/DXT5 compression.\nThis offers a 4:1 compression ratio and is quick with acceptable quality, and fully supports RGBA.\n\nGeneric format that can be used for most textures."),
        ("BC4 (Simple Compression for Opaque Grayscale)",
            "Save the current texture compressed via BC4 compression.\nThis offers a 8:1 compression ratio and has almost indistinguishable quality, but only supports Grayscale, without Alpha.\n\nCan be used for face paints and legacy marks."),
        ("BC5 (Simple Compression for Opaque RG)",
            "Save the current texture compressed via BC5 compression.\nThis offers a 4:1 compression ratio and has almost indistinguishable quality, but only supports RG, without B or Alpha.\n\nRecommended for index maps, unrecommended for normal maps."),
        ("BC7 (Complex Compression for RGBA)",
            "Save the current texture compressed via BC7 compression.\nThis offers a 4:1 compression ratio and has almost indistinguishable quality, but may take a while.\n\nGeneric format that can be used for most textures."),
    };

    private void DrawInputChild(ReadOnlySpan<byte> label, Texture tex, Vector2 size, Vector2 imageSize)
    {
        using (var child = Im.Child.Begin(label, size, true))
        {
            if (!child)
                return;

            using var id = Im.Id.Push(label);
            ImEx.TextFramed(label, Im.ContentRegion.Available with { Y = 0 }, ImGuiColor.FrameBackground.Get());
            Im.Line.New();

            using (Im.Disabled(!_center.SaveTask.IsCompleted))
            {
                TextureDrawer.PathInputBox(_textures, tex, ref tex.TmpPath, "##input"u8, "Import Image..."u8,
                    "Can import game paths as well as your own files."u8, Mod!.ModPath.FullName, _fileDialog, _config.DefaultModImportPath);
                if (_textureSelectCombo.Draw("##combo"u8,
                        "Select the textures included in this mod on your drive or the ones they replace from the game files."u8, tex.Path,
                        Mod.ModPath.FullName.Length + 1, out var newPath)
                 && newPath != tex.Path)
                    tex.Load(_textures, newPath);

                if (tex == _left)
                    _center.DrawMatrixInputLeft(size.X);
                else
                    _center.DrawMatrixInputRight(size.X);
            }

            Im.Line.New();
            using var child2 = Im.Child.Begin("image"u8);
            if (child2)
                TextureDrawer.Draw(tex, imageSize);
        }

        if (_dragDropManager.CreateImGuiTarget("TextureDragDrop", out var files, out _) && GetFirstTexture(files, out var file))
            tex.Load(_textures, file);
    }

    private void SaveAsCombo()
    {
        var (text, desc) = SaveAsStrings[_currentSaveAs];
        Im.Item.SetNextWidth(-Im.Style.FrameHeight - Im.Style.ItemSpacing.X);
        using var combo = Im.Combo.Begin("##format"u8, text);
        Im.Tooltip.OnHover(desc);
        if (!combo)
            return;

        foreach (var (idx, (newText, newDesc)) in SaveAsStrings.Index())
        {
            if (Im.Selectable(newText, idx == _currentSaveAs))
                _currentSaveAs = idx;

            LunaStyle.DrawRightAlignedHelpMarker(newDesc);
        }
    }

    private void RedrawOnSaveBox()
    {
        var redraw = _config.Ephemeral.ForceRedrawOnFileChange;
        if (Im.Checkbox("Redraw on Save"u8, ref redraw))
        {
            _config.Ephemeral.ForceRedrawOnFileChange = redraw;
            _config.Ephemeral.Save();
        }

        Im.Tooltip.OnHover("Force a redraw of your player character whenever you save a file here."u8);
    }

    private void MipMapInput()
    {
        Im.Checkbox("##mipMaps"u8, ref _addMipMaps);
        Im.Tooltip.OnHover("Add the appropriate number of MipMaps to the file."u8);
    }

    private bool _forceTextureStartPath = true;

    private void DrawOutputChild(Vector2 size, Vector2 imageSize)
    {
        using var child = Im.Child.Begin("Output"u8, size, true);
        if (!child)
            return;

        if (_center.IsLoaded)
        {
            RedrawOnSaveBox();
            Im.Line.Same();
            SaveAsCombo();
            Im.Line.Same();
            MipMapInput();

            var canSaveInPlace = Path.IsPathRooted(_left.Path) && _left.Type is TextureType.Tex or TextureType.Dds or TextureType.Png;
            var isActive       = _config.DeleteModModifier.IsActive();
            var buttonSize2 = new Vector2((Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X) / 2,     0);
            var buttonSize3 = new Vector2((Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X * 2) / 3, 0);
            if (ImEx.Button("Save in place"u8, buttonSize2,
                    isActive
                        ? "This saves the texture in place. This is not revertible."u8
                        : $"This saves the texture in place. This is not revertible. Hold {_config.DeleteModModifier} to save.", 
                    !isActive || !canSaveInPlace || _center.IsLeftCopy && _currentSaveAs is (int)CombinedTexture.TextureSaveType.AsIs))
            {
                _center.SaveAs(_left.Type, _textures, _left.Path, (CombinedTexture.TextureSaveType)_currentSaveAs, _addMipMaps);
                AddChangeTask(_left.Path);
                AddReloadTask(_left.Path, false);
            }

            Im.Line.Same();
            if (Im.Button("Save as TEX"u8, buttonSize2))
                OpenSaveAsDialog(".tex");

            if (Im.Button("Export as TGA"u8, buttonSize3))
                OpenSaveAsDialog(".tga");
            Im.Line.Same();
            if (Im.Button("Export as PNG"u8, buttonSize3))
                OpenSaveAsDialog(".png");
            Im.Line.Same();
            if (Im.Button("Export as DDS"u8, buttonSize3))
                OpenSaveAsDialog(".dds");
            Im.Line.New();

            var canConvertInPlace = canSaveInPlace && _left.Type is TextureType.Tex && _center.IsLeftCopy;

            if (ImEx.Button("Convert to BC7"u8, buttonSize3,
                    "This converts the texture to BC7 format in place. This is not revertible."u8,
                    !canConvertInPlace || _left.Format is DXGIFormat.BC7Typeless or DXGIFormat.BC7UNorm or DXGIFormat.BC7UNormSRGB))
            {
                _center.SaveAsTex(_textures, _left.Path, CombinedTexture.TextureSaveType.BC7, _left.MipMaps > 1);
                AddChangeTask(_left.Path);
                AddReloadTask(_left.Path, false);
            }

            Im.Line.Same();
            if (ImEx.Button("Convert to BC3"u8, buttonSize3,
                    "This converts the texture to BC3 format in place. This is not revertible."u8,
                    !canConvertInPlace || _left.Format is DXGIFormat.BC3Typeless or DXGIFormat.BC3UNorm or DXGIFormat.BC3UNormSRGB))
            {
                _center.SaveAsTex(_textures, _left.Path, CombinedTexture.TextureSaveType.BC3, _left.MipMaps > 1);
                AddChangeTask(_left.Path);
                AddReloadTask(_left.Path, false);
            }

            Im.Line.Same();
            if (ImEx.Button("Convert to RGBA"u8, buttonSize3,
                    "This converts the texture to RGBA format in place. This is not revertible."u8,
                    !canConvertInPlace
                 || _left.Format is DXGIFormat.B8G8R8A8UNorm or DXGIFormat.B8G8R8A8Typeless or DXGIFormat.B8G8R8A8UNormSRGB))
            {
                _center.SaveAsTex(_textures, _left.Path, CombinedTexture.TextureSaveType.Bitmap, _left.MipMaps > 1);
                AddChangeTask(_left.Path);
                AddReloadTask(_left.Path, false);
            }
        }

        switch (_center.SaveTask.Status)
        {
            case TaskStatus.WaitingForActivation:
            case TaskStatus.WaitingToRun:
            case TaskStatus.Running:
                ImEx.TextFramed("Computing..."u8, Im.ContentRegion.Available with { Y = 0 }, Colors.PressEnterWarningBg);
                break;
            case TaskStatus.Canceled:
            case TaskStatus.Faulted:
            {
                Im.Text("Could not save file:"u8);
                using var color = ImGuiColor.Text.Push(new Vector4(1, 0, 0, 1));
                Im.TextWrapped(_center.SaveTask.Exception?.ToString() ?? "Unknown Error");
                break;
            }
            default: Im.Dummy(new Vector2(1, Im.Style.FrameHeight)); break;
        }

        Im.Line.New();

        using var child2 = Im.Child.Begin("image"u8);
        if (child2)
            _center.Draw(_textures, imageSize);
    }

    private void InvokeChange(Mod? mod, string path)
    {
        if (mod == null)
            return;

        if (!_editor.Files.Tex.FindFirst(r => string.Equals(r.File.FullName, path, StringComparison.OrdinalIgnoreCase),
                out var registry))
            return;

        _communicator.ModFileChanged.Invoke(new ModFileChanged.Arguments(mod, registry));
    }

    private void OpenSaveAsDialog(string defaultExtension)
    {
        var fileName = Path.GetFileNameWithoutExtension(_left.Path.Length > 0 ? _left.Path : _right.Path);
        _fileDialog.OpenSavePicker("Save Texture as TEX, DDS, PNG or TGA...", "Textures{.png,.dds,.tex,.tga},.tex,.dds,.png,.tga", fileName,
            defaultExtension,
            (a, b) =>
            {
                if (a)
                {
                    _center.SaveAs(null, _textures, b, (CombinedTexture.TextureSaveType)_currentSaveAs, _addMipMaps);
                    AddChangeTask(b);
                    if (b == _left.Path)
                        AddReloadTask(_left.Path, false);
                    else if (b == _right.Path)
                        AddReloadTask(_right.Path, true);
                }
            }, Mod!.ModPath.FullName, _forceTextureStartPath);
        _forceTextureStartPath = false;
    }

    private void AddChangeTask(string path)
    {
        _center.SaveTask.ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully)
                return;

            _framework.RunOnFrameworkThread(() => InvokeChange(Mod, path));
        }, TaskScheduler.Default);
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

            _framework.RunOnFrameworkThread(() => tex.Reload(_textures));
        }, TaskScheduler.Default);
    }

    private Vector2 GetChildWidth()
    {
        var windowWidth = Im.Window.MaximumContentRegion.X - Im.Window.MinimumContentRegion.X - Im.Style.TextHeight;
        if (_overlayCollapsed)
        {
            var width = windowWidth - Im.Style.FramePadding.X * 3;
            return new Vector2(width / 2, -1);
        }

        return new Vector2((windowWidth - Im.Style.FramePadding.X * 5) / 3, -1);
    }

    private void DrawTextureTab()
    {
        using var tab = Im.TabBar.BeginItem("Textures"u8);
        if (!tab)
            return;

        try
        {
            _dragDropManager.CreateImGuiSource("TextureDragDrop",
                m => m.Extensions.Any(e => ValidTextureExtensions.Contains(e.ToLowerInvariant())), m =>
                {
                    if (!GetFirstTexture(m.Files, out var file))
                        return false;

                    Im.Text($"Dragging texture for editing: {Path.GetFileName(file)}");
                    return true;
                });
            var childWidth = GetChildWidth();
            var imageSize  = new Vector2(childWidth.X - Im.Style.FramePadding.X * 2);
            DrawInputChild("Input Texture"u8, _left, childWidth, imageSize);
            Im.Line.Same();
            DrawOutputChild(childWidth, imageSize);
            if (!_overlayCollapsed)
            {
                Im.Line.Same();
                DrawInputChild("Overlay Texture"u8, _right, childWidth, imageSize);
            }

            Im.Line.Same();
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
            ? RefTuple.Create(">"u8, "Show a third panel in which you can import an additional texture as an overlay for the primary texture."u8)
            : RefTuple.Create("<"u8, "Hide the overlay texture panel and clear the currently loaded overlay texture, if any."u8);
        if (Im.Button(label, Im.ContentRegion.Available with { X = Im.Style.TextHeight }))
            _overlayCollapsed = !_overlayCollapsed;

        Im.Tooltip.OnHover(tooltip);
    }

    private static bool GetFirstTexture(IEnumerable<string> files, [NotNullWhen(true)] out string? file)
    {
        file = files.FirstOrDefault(f => ValidTextureExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        return file != null;
    }

    private static readonly string[] ValidTextureExtensions =
    [
        ".png",
        ".dds",
        ".tex",
        ".tga",
    ];
}
