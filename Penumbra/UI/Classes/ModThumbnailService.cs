using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using ImSharp;
using Luna;
using Penumbra.Mods;
using Penumbra.Mods.Manager;
using Penumbra.Services;

namespace Penumbra.UI.Classes;

/// <summary> Loads, draws and imports mod thumbnails. </summary>
public sealed class ModThumbnailService(
    ITextureProvider textureProvider,
    ITextureReadbackProvider readbackProvider,
    IFramework framework,
    ModManager modManager,
    PenumbraMessager messager) : IUiService
{
    private const float MaxHeight      = 200;
    private const float MaxTooltipSize = 256;

    private volatile bool _pasting;

    public bool CanPaste
        => !_pasting && textureProvider.HasClipboardImage();

    /// <summary>
    /// Draw the thumbnail centered in the available width.
    /// While pressed with a mouse button, shows the thumbnail enlarged in a popup.
    /// </summary>
    public void Draw(Mod mod)
    {
        if (!TryGetWrap(mod, out var wrap))
            return;

        var available = Im.ContentRegion.Available.X;
        var scale     = Math.Min(Im.Style.GlobalScale, Math.Min(available / wrap.Width, MaxHeight * Im.Style.GlobalScale / wrap.Height));
        if (scale <= 0)
            return;

        var size = wrap.Size * scale;
        Im.Cursor.X += (available - size.X) / 2;
        Im.Image.Draw(wrap.Id, size);
        if (Im.Item.Hovered())
        {
            Im.Mouse.Cursor = MouseCursor.Hand;
            if (Im.Item.Clicked() || Im.Item.RightClicked())
                Im.Popup.Open("##enlargedThumbnail"u8);
        }

        DrawEnlarged(wrap);
    }

    /// <summary> Show the thumbnail as tooltip if <see cref="Im.Item.Hovered"/>. </summary>
    public void DrawTooltip(Mod mod)
    {
        if (!Im.Item.Hovered() || !TryGetWrap(mod, out var wrap))
            return;

        var       scale   = Im.Style.GlobalScale * Math.Min(1, MaxTooltipSize / Math.Max(wrap.Width, wrap.Height));
        using var tooltip = Im.Tooltip.Begin();
        Im.Image.Draw(wrap.Id, wrap.Size * scale);
    }

    /// <summary>
    /// Import an image file as mod thumbnail, notifying on failure.
    /// If <paramref name="file"/> is not inside the mod directory, it is copied into the mod without overwriting other files.
    /// Failure happens if <paramref name="file"/> doesn't exist, or the user doesn't have write access, etc.
    /// </summary>
    public void Import(Mod mod, string file)
    {
        try
        {
            var source = Path.GetFullPath(file);
            if (mod.IsInModDirectory(source))
                modManager.DataEditor.ChangeModImage(mod, Path.GetRelativePath(mod.ModPath.FullName, source));
            else
                WriteModImage(mod, Path.GetFileName(source), File.ReadAllBytes(source));
        }
        catch (Exception ex)
        {
            messager.NotificationMessage(ex, $"Could not set {file} as thumbnail for {mod.Name}.", NotificationType.Error, false);
        }
    }

    /// <summary> Encode the clipboard image as PNG in the background, then write it into the mod directory on the framework thread. </summary>
    public async Task Paste(Mod mod)
    {
        _pasting = true;
        try
        {
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var wrap   = await textureProvider.CreateFromClipboardAsync("Penumbra.ClipboardThumbnail", cancel.Token);
            var png = readbackProvider.GetSupportedImageEncoderInfos()
                .First(c => c.Extensions.Contains(".png", StringComparer.OrdinalIgnoreCase));
            using var stream = new MemoryStream();
            await readbackProvider.SaveToStreamAsync(wrap, png.ContainerGuid, stream, leaveWrapOpen: true, leaveStreamOpen: true,
                cancellationToken: cancel.Token);
            var data = stream.ToArray();
            await framework.RunOnFrameworkThread(() => WriteModImage(mod, "thumbnail.png", data));
        }
        catch (Exception ex)
        {
            messager.NotificationMessage(ex, $"Could not set clipboard image as thumbnail for {mod.Name}.", NotificationType.Error, false);
        }
        finally
        {
            _pasting = false;
        }
    }

    /// <summary>
    /// Write image data into the mod directory under the given file name and use it as the mod's image.
    /// If <paramref name="fileName"/> already exists (and isn't identical to the new image), finds a new unique filename.
    /// </summary>
    private void WriteModImage(Mod mod, string fileName, byte[] data)
    {
        var name      = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var target    = Path.Combine(mod.ModPath.FullName, fileName);

        // If `name.ext` exists, find a new `name (i).ext` that doesn't exist yet, mirroring typical file manager nth copy semantics.
        // Alternatively, if the file does exist, but its contents are identical to `data`, reuse it instead.
        for (var i = 2; File.Exists(target) && !File.ReadAllBytes(target).AsSpan().SequenceEqual(data); ++i)
        {
            fileName = $"{name} ({i}){extension}";
            target   = Path.Combine(mod.ModPath.FullName, fileName);
        }

        if (!File.Exists(target))
            File.WriteAllBytes(target, data);
        modManager.DataEditor.ChangeModImage(mod, fileName);
    }

    private bool TryGetWrap(Mod mod, [NotNullWhen(true)] out IDalamudTextureWrap? wrap)
    {
        wrap = null;
        return mod.GetImageFile() is { } file
         && textureProvider.GetFromFile(file).TryGetWrap(out wrap, out _)
         && wrap is { Width: > 0, Height: > 0 };
    }

    private static void DrawEnlarged(IDalamudTextureWrap wrap)
    {
        Im.Window.SetNextPosition(Im.Viewport.Main.Center, Condition.Always, new Vector2(0.5f));
        using var style = ImStyleDouble.WindowPadding.Push(Vector2.Zero);
        using var popup = Im.Popup.Begin("##enlargedThumbnail"u8,
            WindowFlags.Modal | WindowFlags.NoDecoration | WindowFlags.AlwaysAutoResize | WindowFlags.NoSavedSettings);
        if (!popup)
            return;

        var maxSize = Im.Viewport.Main.Size * 0.9f;
        Im.Image.Draw(wrap.Id, wrap.Size * Math.Min(maxSize.X / wrap.Width, maxSize.Y / wrap.Height));

        if (!Im.Mouse.IsDown(MouseButton.Left) && !Im.Mouse.IsDown(MouseButton.Right))
            Im.Popup.CloseCurrent();
    }
}
