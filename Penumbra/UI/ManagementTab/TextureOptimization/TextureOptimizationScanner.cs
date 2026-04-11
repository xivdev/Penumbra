using Lumina.Data.Files;
using OtterTex;
using Penumbra.Import.Textures;
using Penumbra.Mods;
using Penumbra.Mods.Manager;

namespace Penumbra.UI.ManagementTab;

public sealed class TextureOptimizationScanner(ModManager mods, TextureOptimizationConfig config, ManagementLog<TextureOptimization> log)
    : ModFileScanner<OptimizableTexture>(mods, log)
{
    protected override unsafe OptimizableTexture Create(string fileName, Mod mod)
    {
        try
        {
            // We have checked existence before.
            var fileInfo = new FileInfo(fileName);
            var size     = fileInfo.Length;
            if (size <= sizeof(TexFile.TexHeader))
                return new OptimizableTexture(fileName, mod);
            if (size <= config.LowerSizeLimit)
                return new OptimizableTexture(fileName, mod, true);

            using var stream = fileInfo.OpenRead();
            var       data   = TexFileParser.Parse(stream);
            if (data.Meta.Width <= config.SmallDimensionLimit
             && data.Meta.Height <= config.SmallDimensionLimit)
                return new OptimizableTexture(fileName, mod, true);

            var large = data.Meta.Width >= config.LargeDimensionLimit
             || data.Meta.Height >= config.LargeDimensionLimit;
            var uncompressed = !data.Meta.Format.IsCompressed();
            if (data.IsSolidColor(out var color))
                return new OptimizableTexture(fileName, mod, size, data.Meta.Format, color, data.Meta.Width, data.Meta.Height,
                    data.Meta.MipLevels);

            if (large || uncompressed)
                return new OptimizableTexture(fileName, mod, size, data.Meta.Format, data.Meta.Width,
                    data.Meta.Height, data.Meta.MipLevels);
        }
        catch
        {
            return new OptimizableTexture(fileName, mod);
        }

        return new OptimizableTexture(fileName, mod, true);
    }

    protected override bool DoCreateFile(string fileName, Mod mod)
    {
        if (!File.Exists(fileName))
            return false;

        if (Path.GetExtension(fileName) is not ".tex" and not ".atex")
            return false;

        return true;
    }
}
