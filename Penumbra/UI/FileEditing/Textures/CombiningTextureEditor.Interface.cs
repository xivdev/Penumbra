using ImSharp;
using Luna;
using OtterTex;
using Penumbra.Communication;
using Penumbra.Import.Textures;
using Penumbra.String.Classes;
using Penumbra.UI.Classes;
using TextureType = Penumbra.Import.Textures.TextureType;

namespace Penumbra.UI.FileEditing.Textures;

public partial class CombiningTextureEditor
{
    private static readonly (StringU8, StringU8)[] SaveAsStrings =
    {
        (new StringU8("As Is"u8),
            new StringU8("Save the current texture with its own format without additional conversion or compression, if possible."u8)),
        (new StringU8("RGBA (Uncompressed)"u8),
            new StringU8(
                "Save the current texture as an uncompressed BGRA bitmap.\nThis requires the most space but technically offers the best quality."u8)),
        (new StringU8("BC1 (Simple Compression for Opaque RGB)"u8),
            new StringU8(
                "Save the current texture compressed via BC1/DXT1 compression.\nThis offers a 8:1 compression ratio and is quick with acceptable quality, but only supports RGB, without Alpha.\n\nCan be used for diffuse maps and equipment textures to save extra space."u8)),
        (new StringU8("BC3 (Simple Compression for RGBA)"u8),
            new StringU8(
                "Save the current texture compressed via BC3/DXT5 compression.\nThis offers a 4:1 compression ratio and is quick with acceptable quality, and fully supports RGBA.\n\nGeneric format that can be used for most textures."u8)),
        (new StringU8("BC4 (Simple Compression for Opaque Grayscale)"u8),
            new StringU8(
                "Save the current texture compressed via BC4 compression.\nThis offers a 8:1 compression ratio and has almost indistinguishable quality, but only supports Grayscale, without Alpha.\n\nCan be used for face paints and legacy marks."u8)),
        (new StringU8("BC5 (Simple Compression for Opaque RG)"u8),
            new StringU8(
                "Save the current texture compressed via BC5 compression.\nThis offers a 4:1 compression ratio and has almost indistinguishable quality, but only supports RG, without B or Alpha.\n\nRecommended for index maps, unrecommended for normal maps."u8)),
        (new StringU8("BC7 (Complex Compression for RGBA)"u8),
            new StringU8(
                "Save the current texture compressed via BC7 compression.\nThis offers a 4:1 compression ratio and has almost indistinguishable quality, but may take a while.\n\nGeneric format that can be used for most textures."u8)),
    };

    private bool _overlayCollapsed = true;

    public bool DrawToolbar(bool disabled)
    {
        if (!_inModEditWindow)
            DrawOverlayCollapseButton();
        return false;
    }

    public bool DrawPanel(bool disabled)
    {
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
            if (_inModEditWindow)
            {
                using var child = Im.Child.Begin("###OutputWrapper"u8, childWidth);
                if (child)
                {
                    DrawOverlayCollapseButton();
                    DrawOutputChild(new Vector2(-1), imageSize);
                }
            }
            else
            {
                DrawOutputChild(childWidth, imageSize);
            }

            if (!_overlayCollapsed)
            {
                Im.Line.Same();
                DrawInputChild("Overlay Texture"u8, _right, childWidth, imageSize);
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Error($"Unknown Error while drawing textures:\n{e}");
        }

        return false;
    }

    private Vector2 GetChildWidth()
    {
        var windowWidth = Im.Window.MaximumContentRegion.X - Im.Window.MinimumContentRegion.X;
        if (_overlayCollapsed)
        {
            var width = windowWidth - Im.Style.ItemSpacing.X;
            return new Vector2(width / 2, -1);
        }

        return new Vector2((windowWidth - Im.Style.ItemSpacing.X * 2) / 3, -1);
    }

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
                if (tex != _left || _inModEditWindow)
                {
                    TextureDrawer.PathInputBox(_textures, tex, ref tex.TmpPath, "##input"u8, "Import Image..."u8,
                        "Can import game paths as well as your own files."u8, _context?.Mod?.ModPath.FullName, _fileDialog,
                        _config.DefaultModImportPath);
                    if (_textureSelectCombo is not null
                     && _textureSelectCombo.Draw("##combo"u8,
                            "Select the textures included in this mod on your drive or the ones they replace from the game files."u8, tex.Path,
                            _context?.Mod?.ModPath.FullName.Length + 1 ?? 0, out var newPath)
                     && newPath != tex.Path)
                        tex.Load(_textures, newPath);
                }

                if (tex.OriginalBaseImage.MipMaps > 1)
                {
                    Im.Item.SetNextWidthScaled(75.0f);
                    if (Im.Drag("Scaling"u8, ref tex.LevelOfDetail, $"\u00F7 {1 << tex.LevelOfDetail}", 0, tex.OriginalBaseImage.MipMaps - 1,
                            0.1f, SliderFlags.NoInput))
                        tex.SelectLevelOfDetail(_textures);
                }

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

            var canSaveInPlace = Path.IsPathRooted(_left.Path)
             && _left.Type is TextureType.Tex or TextureType.Dds or TextureType.Png
             && _writable;
            var isActive    = _config.DeleteModModifier.IsActive();
            var buttonSize2 = new Vector2((Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X) / 2,     0);
            var buttonSize3 = new Vector2((Im.ContentRegion.Available.X - Im.Style.ItemSpacing.X * 2) / 3, 0);

            if (_inModEditWindow)
            {
                if (ImEx.Button("Save in place"u8, buttonSize2,
                        isActive
                            ? "This saves the texture in place. This is not revertible."u8
                            : $"This saves the texture in place. This is not revertible. Hold {_config.DeleteModModifier} to save.",
                        !isActive
                     || !canSaveInPlace
                     || _center.IsLeftCopy && _currentSaveAs is (int)CombinedTexture.TextureSaveType.AsIs && _left.LevelOfDetail is 0))
                    SaveRequested?.Invoke();

                Im.Line.Same();
                if (Im.Button("Save as TEX"u8, buttonSize2))
                    OpenSaveAsDialog(".tex");
            }

            if (Im.Button("Export as TGA"u8, buttonSize3))
                OpenSaveAsDialog(".tga");
            Im.Line.Same();
            if (Im.Button("Export as PNG"u8, buttonSize3))
                OpenSaveAsDialog(".png");
            Im.Line.Same();
            if (Im.Button("Export as DDS"u8, buttonSize3))
                OpenSaveAsDialog(".dds");
            Im.Line.New();

            var canConvertInPlace = canSaveInPlace && _left.Type is TextureType.Tex or TextureType.Dds && _center.IsLeftCopy;

            if (ImEx.Button("Convert to BC7"u8, buttonSize3,
                    "This converts the texture to BC7 format in place. This is not revertible."u8,
                    !canConvertInPlace || _left.Format is DXGIFormat.BC7Typeless or DXGIFormat.BC7UNorm or DXGIFormat.BC7UNormSRGB))
            {
                _nextSaveAs     = CombinedTexture.TextureSaveType.BC7;
                _nextAddMipMaps = _left.MipMaps > 1;
                SaveRequested?.Invoke();
            }

            Im.Line.Same();
            if (ImEx.Button("Convert to BC3"u8, buttonSize3,
                    "This converts the texture to BC3 format in place. This is not revertible."u8,
                    !canConvertInPlace || _left.Format is DXGIFormat.BC3Typeless or DXGIFormat.BC3UNorm or DXGIFormat.BC3UNormSRGB))
            {
                _nextSaveAs     = CombinedTexture.TextureSaveType.BC3;
                _nextAddMipMaps = _left.MipMaps > 1;
                SaveRequested?.Invoke();
            }

            Im.Line.Same();
            if (ImEx.Button("Convert to RGBA"u8, buttonSize3,
                    "This converts the texture to RGBA format in place. This is not revertible."u8,
                    !canConvertInPlace
                 || _left.Format is DXGIFormat.B8G8R8A8UNorm or DXGIFormat.B8G8R8A8Typeless or DXGIFormat.B8G8R8A8UNormSRGB))
            {
                _nextSaveAs     = CombinedTexture.TextureSaveType.Bitmap;
                _nextAddMipMaps = _left.MipMaps > 1;
                SaveRequested?.Invoke();
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

        if (_center.TryGetRgbaSolidColor(out var solidColor, out var width, out var height))
        {
            using var color = ImGuiColor.Text.Push(ImGuiColor.Text.Get().HalfBlend(Rgba32.Yellow));
            Im.TextWrapped(
                $"This texture is a solid surface of color {solidColor}.");
            if (Texture.SolidTextures.TryGetValue(solidColor, out var path))
            {
                Im.TextWrapped($"Consider using a file swap to {path}.");
                Im.Line.Same();
                color.Pop();
                if (ImEx.Icon.Button(LunaStyle.ToClipboardIcon, "Copy this path to your clipboard."u8))
                    Im.Clipboard.Set(path);
            }
            else if (width > 32 || height > 32)
            {
                Im.TextWrapped($"Consider scaling it down to at most 32 \u00D7 32 pixels.");
            }

            Im.Line.New();
        }

        using var child2 = Im.Child.Begin("image"u8);
        if (child2)
            _center.Draw(_textures, imageSize);
    }

    private void OpenSaveAsDialog(string defaultExtension)
    {
        var fileName = Path.GetFileNameWithoutExtension(_left.Path.Length > 0 ? _left.Path : _right.Path);
        _fileDialog.OpenSavePicker("Save Texture as TEX, DDS, PNG or TGA...", "Textures{.png,.dds,.tex,.atex,.tga},.tex{.tex,.atex},.dds,.png,.tga", fileName,
            defaultExtension,
            (a, b) =>
            {
                if (a)
                {
                    _center.SaveAs(null, _textures, b, (CombinedTexture.TextureSaveType)_currentSaveAs, _addMipMaps);
                    AddPostSaveTask(b);
                }
            }, _context?.Mod?.ModPath.FullName, _forceTextureStartPath);
        _forceTextureStartPath = false;
    }

    private void AddPostSaveTask(string path)
    {
        _center.SaveTask.ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully)
                return;

            if (_context?.Mod is not null
             || _left.Path.Equals(path, StringComparison.OrdinalIgnoreCase)
             || _right.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                _framework.RunOnFrameworkThread(() =>
                {
                    InvokeChange(path);
                    if (_left.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                        _left.Reload(_textures);
                    if (_right.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                        _right.Reload(_textures);
                });
        }, TaskScheduler.Default);
    }

    private void InvokeChange(string path)
    {
        if (!_modManager.TryIdentifyPath(path, out var mod, out var relPathStr) || !Utf8RelPath.FromString(relPathStr, out var relPath))
            return;

        _communicator.ModFileChanged.Invoke(new ModFileChanged.Arguments(mod, relPath,
            _context?.TryFindFileRegistry(mod, relPath)));
    }

    private void DrawOverlayCollapseButton()
    {
        var (icon, iconPosition, label, tooltip) = _overlayCollapsed
            ? RefTuple.Create(LunaStyle.CollapseLeftIcon, ImEx.Icon.IconPosition.BeforeLabel, "Show Overlay"u8,
                "Show a third panel in which you can import an additional texture as an overlay for the primary texture."u8)
            : RefTuple.Create(LunaStyle.ExpandRightIcon, ImEx.Icon.IconPosition.AfterLabel, "Hide Overlay"u8,
                "Hide the overlay texture panel and clear the currently loaded overlay texture, if any."u8);
        Im.Dummy(Im.ContentRegion.Available.X - ImEx.Icon.CalculateLabeledButtonSize(icon, label).X);
        Im.Line.NoSpacing();
        if (ImEx.Icon.LabeledButton(icon, label, tooltip, iconPosition: iconPosition))
            _overlayCollapsed = !_overlayCollapsed;
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
        ".atex",
        ".tga",
    ];
}
