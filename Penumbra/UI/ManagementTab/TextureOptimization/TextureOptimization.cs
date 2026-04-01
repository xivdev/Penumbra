using ImSharp;
using Lumina.Data.Files;
using Luna;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.Mods.Manager.OptionEditor;
using Penumbra.String.Classes;

namespace Penumbra.UI.ManagementTab;

public sealed class TextureOptimization(ModGroupEditor groupEditor, ManagementLog<TextureOptimization> log) : IService
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
        }
    }

    public void ReplaceWithSolidColor(string filePath, Mod mod, Rgba32 color, bool backup)
    {
        log.Information($"Replacing {filePath} with solid color {color}...");
        if (Texture.SolidTextures.TryGetValue(color, out var solidGamePath))
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
