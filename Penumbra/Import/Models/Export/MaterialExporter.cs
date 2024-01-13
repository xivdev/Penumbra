using Lumina.Data.Parsing;
using Penumbra.GameData.Files;
using SharpGLTF.Materials;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Penumbra.Import.Models.Export;

public class MaterialExporter
{
    // input stuff
    public struct Material
    {
        public MtrlFile Mtrl;
        public Sampler[] Samplers;
        // variant?
    }

    public struct Sampler
    {
        public TextureUsage Usage;
        public Image<Rgba32> Texture;
    }

    public static MaterialBuilder Export(Material material, string name)
    {
        return material.Mtrl.ShaderPackage.Name switch
        {
            "character.shpk" => BuildCharacter(material, name),
            _                => BuildFallback(material, name),
        };
    }

    private static MaterialBuilder BuildCharacter(Material material, string name)
    {
        // TODO: pixelbashing time
        var sampler = material.Samplers
            .Where(s => s.Usage == TextureUsage.SamplerNormal)
            .First();

        // TODO: clean up this name generation a bunch. probably a method.
        var imageName = name.Replace("/", "");
        var baseColor = BuildImage(sampler.Texture, $"{imageName}_basecolor");

        return BuildSharedBase(material, name)
            .WithBaseColor(baseColor);
    }

    private static MaterialBuilder BuildFallback(Material material, string name)
    {
        Penumbra.Log.Warning($"Unhandled shader package: {material.Mtrl.ShaderPackage.Name}");
        return BuildSharedBase(material, name)
            .WithMetallicRoughnessShader()
            .WithChannelParam(KnownChannel.BaseColor, KnownProperty.RGBA, Vector4.One);
    }

    private static MaterialBuilder BuildSharedBase(Material material, string name)
    {
        // TODO: Move this and potentially the other known stuff into MtrlFile?
        const uint backfaceMask = 0x1;
        var showBackfaces = (material.Mtrl.ShaderPackage.Flags & backfaceMask) == 0;

        return new MaterialBuilder(name)
            .WithDoubleSide(showBackfaces);
    }

    private static ImageBuilder BuildImage(Image<Rgba32> image, string name)
    {
        byte[] textureBytes;
        using (var memoryStream = new MemoryStream())
        {
            image.Save(memoryStream, PngFormat.Instance);
            textureBytes = memoryStream.ToArray();
        }

        var imageBuilder = ImageBuilder.From(textureBytes, name);
        imageBuilder.AlternateWriteFileName = $"{name}.*";
        return imageBuilder;
    }
}
