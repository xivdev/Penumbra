using ImSharp;
using Lumina.Data.Files;
using Luna;
using OtterTex;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.String.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class TextureOptimization(ModGroupEditor groupEditor, TextureManager textures, ManagementLog<TextureOptimization> log) : IService
{
    public static unsafe byte[] WriteSolidColorTex(Rgba32 color)
    {
        var header = new TexFile.TexHeader
        {
            Height   = 1,
            Width    = 1,
            Depth    = 1,
            MipCount = 1,
            Format   = TexFile.TextureFormat.B8G8R8A8,
            Type     = TexFile.Attribute.TextureType1D,
        };
        header.OffsetToSurface[0] = 80;
        for (var i = 1; i < 13; ++i)
            header.OffsetToSurface[i] = 0;
        header.LodOffset[0] = 0;
        header.LodOffset[1] = 1;
        header.LodOffset[2] = 2;
        var       ret = new byte[84];
        using var s   = new MemoryStream(ret);
        using var w   = new BinaryWriter(s);
        header.Write(w);
        w.Write(color.B);
        w.Write(color.G);
        w.Write(color.R);
        w.Write(color.A);
        return ret;
    }

    public CombinedTexture.TextureSaveType GetTargetFormat(string filePath, Mod? mod)
    {
        if (filePath.EndsWith("_id.tex", StringComparison.OrdinalIgnoreCase))
            return CombinedTexture.TextureSaveType.BC5;

        if (mod is not null)
            foreach (var dataContainer in mod.AllDataContainers)
            {
                foreach (var (gamePath, _) in dataContainer.Files.Where(kvp
                             => kvp.Value.FullName.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                {
                    return gamePath.Path.EndsWith("_id.tex"u8)
                        ? CombinedTexture.TextureSaveType.BC5
                        : CombinedTexture.TextureSaveType.BC7;
                }
            }

        return CombinedTexture.TextureSaveType.BC7;
    }

    public Task RestrictDimensions(string filePath, int maxWidth, int maxHeight, bool backup,
        CombinedTexture.TextureSaveType saveType = CombinedTexture.TextureSaveType.AsIs)
    {
        var tmpPath = filePath + ".tmp";
        log.Information($"Reading texture file {filePath} to restrict dimensions...");
        return Task.Run(() =>
        {
            try
            {
                var scratchImage = textures.LoadTex(filePath);
                var targetWidth  = scratchImage.Width;
                var targetHeight = scratchImage.Height;
                var lodLevel     = 0;
                if (scratchImage.Width <= maxWidth && scratchImage.Height <= maxHeight)
                    return;

                // Preserve ratio
                while (targetWidth > maxWidth || targetHeight > maxHeight)
                {
                    targetWidth  /= 2;
                    targetHeight /= 2;
                    ++lodLevel;
                }

                BaseImage result;
                if (scratchImage.MipMaps > lodLevel)
                {
                    result = scratchImage.AtLevelOfDetail(lodLevel);
                    log.Information($"Successfully wrote {targetWidth}x{targetHeight} texture by reducing LoD by {lodLevel}.");
                }
                else
                {
                    var dds = scratchImage.AsDds!;
                    if (scratchImage.Format.IsCompressed())
                    {
                        dds = scratchImage.AsDds!.Decompress(DXGIFormat.B8G8R8A8UNorm);
                        log.Information($"Successfully decompressed {scratchImage.Format} texture.");
                    }

                    result = dds.Resize(targetWidth, targetHeight, FilterFlags.Box);
                    log.Information($"Successfully resized texture to {targetWidth}x{targetHeight}.");
                    if (saveType is CombinedTexture.TextureSaveType.AsIs)
                        (saveType, result) = scratchImage.Format switch
                        {
                            DXGIFormat.BC1UNorm or DXGIFormat.BC1Typeless or DXGIFormat.BC1UNormSRGB => (CombinedTexture.TextureSaveType.BC1,
                                result),
                            DXGIFormat.BC3UNorm or DXGIFormat.BC3Typeless or DXGIFormat.BC3UNormSRGB => (CombinedTexture.TextureSaveType.BC3,
                                result),
                            DXGIFormat.BC4UNorm or DXGIFormat.BC4Typeless or DXGIFormat.BC4SNorm => (CombinedTexture.TextureSaveType.BC4,
                                result),
                            DXGIFormat.BC5UNorm or DXGIFormat.BC5Typeless or DXGIFormat.BC5SNorm => (CombinedTexture.TextureSaveType.BC5,
                                result),
                            DXGIFormat.BC7UNorm or DXGIFormat.BC7Typeless or DXGIFormat.BC7UNormSRGB => (CombinedTexture.TextureSaveType.BC7,
                                result),
                            DXGIFormat.R8G8B8A8UNorm or DXGIFormat.R8G8B8A8Typeless or DXGIFormat.R8G8B8A8SInt or DXGIFormat.R8G8B8A8SNorm
                                or DXGIFormat.R8G8B8A8UInt or DXGIFormat.B8G8R8A8Typeless or DXGIFormat.B8G8R8A8UNorm
                                or DXGIFormat.B8G8R8A8UNormSRGB or DXGIFormat.B8G8R8X8Typeless or DXGIFormat.B8G8R8X8UNorm
                                or DXGIFormat.B8G8R8X8UNormSRGB => (CombinedTexture.TextureSaveType.Bitmap, result),
                            _ => Convert(),
                        };

                    (CombinedTexture.TextureSaveType, BaseImage) Convert()
                    {
                        var ret = result.AsDds!.Convert(scratchImage.Format);
                        log.Information($"Successfully converted resized texture back to {scratchImage.Format}.");
                        return (CombinedTexture.TextureSaveType.AsIs, ret);
                    }
                }

                textures.SaveAs(saveType, true, true, result, tmpPath).Wait();
                if (backup)
                    MoveToBackup(filePath);
                File.Move(tmpPath, filePath, true);
                log.Information($"Moved temporary file to {filePath}.");
            }
            catch (Exception ex)
            {
                log.Error($"Failed to resize texture at {filePath}:\n{ex}");
                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(tmpPath))
                        File.Delete(tmpPath);
                }
                catch (Exception ex)
                {
                    log.Error($"Failed to clean up temporary file at {tmpPath} after failure:\n{ex}");
                }
            }
        });
    }

    public Task Compress(string filePath, Mod mod, bool backup)
    {
        var target  = GetTargetFormat(filePath, mod);
        var tmpPath = filePath + ".tmp";

        log.Information($"Compressing texture {filePath} in {mod.Name} to {target}...");
        return textures.SaveAs(target, true, true, filePath, tmpPath).ContinueWith(state =>
        {
            if (state.IsCompletedSuccessfully)
            {
                log.Information($"Successfully wrote {target} compressed texture to temporary file.");
                try
                {
                    if (backup)
                        MoveToBackup(filePath);

                    File.Move(tmpPath, filePath, true);
                    log.Information($"Moved temporary file to {filePath}.");
                }
                catch (Exception ex)
                {
                    log.Error($"Could not move compressed texture to {filePath}:\n{ex}");
                    throw;
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tmpPath))
                            File.Delete(tmpPath);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Could not clean up temporary file {tmpPath} after failure:\n{ex}");
                    }
                }
            }
            else
            {
                log.Error($"Failed to write {target} compressed texture to temporary file.");
            }
        });
    }

    private void MoveToBackup(string filePath)
    {
        File.Move(filePath, filePath + ".bak");
        log.Information($"Moved {filePath} to backup.");
    }

    private void Delete(string filePath)
    {
        File.Delete(filePath);
        log.Information($"Deleted {filePath}.");
    }

    private void RemoveFile(string filePath, bool backup)
    {
        try
        {
            if (backup)
                MoveToBackup(filePath);
            else
                Delete(filePath);
        }
        catch (Exception ex)
        {
            log.Error($"Failed to {(backup ? "remove" : "backup")} the file {filePath}:\n{ex}");
            throw;
        }
    }

    public bool CanReplaceWithSwap(string filePath, Mod? mod, Rgba32 color, out StringU8 gamePath)
    {
        if (!Texture.SolidTextures.TryGetValue(color, out gamePath))
            return false;

        if (mod is null)
            return true;

        // Do not replace UI files with swaps.
        return !mod.AllDataContainers.SelectMany(c => c.Files).Any(kvp
            => kvp.Value.FullName.Equals(filePath, StringComparison.OrdinalIgnoreCase) && kvp.Key.Path.StartsWith("ui/"u8));
    }

    public void ReplaceWithSolidColor(string filePath, Mod mod, Rgba32 color, bool backup)
    {
        log.Information($"Replacing {filePath} with solid color {color}...");
        if (CanReplaceWithSwap(filePath, mod, color, out var solidGamePath))
        {
            // Replace all redirections to the file with a swap to the game's solid color texture.
            var fullSolidGamePath = new FullPath(solidGamePath.ToString());
            foreach (var container in mod.AllDataContainers)
            {
                var files = container.Files.ToDictionary();
                var swaps = container.FileSwaps.ToDictionary();
                foreach (var (gamePath, fullPath) in container.Files)
                {
                    if (!string.Equals(fullPath.FullName, filePath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    files.Remove(gamePath);
                    swaps[gamePath] = fullSolidGamePath;
                    log.Information(
                        $"Replaced redirection from {gamePath} with swap to {fullSolidGamePath} in {mod.Name} - {container.GetFullName()}.");
                }

                if (files.Count < container.Files.Count)
                {
                    groupEditor.SetFiles(container, files);
                    groupEditor.SetFileSwaps(container, swaps);
                    log.Information($"Replaced {container.Files.Count - files.Count} redirections with swaps.");
                }
            }

            RemoveFile(filePath, backup);
        }
        else
        {
            var tmpPath = filePath + ".tmp";
            try
            {
                var bytes = WriteSolidColorTex(color);
                File.WriteAllBytes(tmpPath, bytes);
                log.Information("Wrote new solid color texture to temporary file.");
                if (backup)
                    MoveToBackup(filePath);

                File.Move(tmpPath, filePath, true);
                log.Information($"Moved temporary file to {filePath}.");
            }
            catch (Exception ex)
            {
                log.Error($"Could not replace {filePath} with solid color texture for {color}:\n{ex}");
                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(tmpPath))
                        File.Delete(tmpPath);
                }
                catch (Exception ex)
                {
                    log.Error($"Could not clean up temporary file {tmpPath} after failure:\n{ex}");
                }
            }
        }
    }
}
