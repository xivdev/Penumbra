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
        var table = material.Mtrl.Table;
        var normal = material.Samplers
            .Where(s => s.Usage == TextureUsage.SamplerNormal)
            .First()
            .Texture;

        var baseColorTarget = new Image<Rgba32>(normal.Width, normal.Height);
        normal.ProcessPixelRows(baseColorTarget, (sourceAccessor, targetAccessor) =>
        {
            for (int y = 0; y < sourceAccessor.Height; y++)
            {
                var sourceRow = sourceAccessor.GetRowSpan(y);
                var targetRow = targetAccessor.GetRowSpan(y);

                for (int x = 0; x < sourceRow.Length; x++)
                {
                    ref var sourcePixel = ref sourceRow[x];
                    ref var targetPixel = ref targetRow[x];

                    var (smoothed, stepped) = GetTableRowIndices(sourceRow[x].A / 255f);
                    var prevRow = table[(int)MathF.Floor(smoothed)];
                    var nextRow = table[(int)MathF.Ceiling(smoothed)];

                    // Base colour (table[.a], .b)
                    var lerpedDiffuse = Vector3.Lerp(prevRow.Diffuse, nextRow.Diffuse, smoothed % 1);
                    targetPixel.FromVector4(new Vector4(lerpedDiffuse, 1));
                    targetPixel.A = sourcePixel.B;

                    // Normal (.rg)
                    // TODO: we don't actually need alpha at all for normal, but _not_ using the existing rgba texture means I'll need a new one, with a new accessor. Think about it.
                    sourcePixel.B = byte.MaxValue;
                    sourcePixel.A = byte.MaxValue;
                }
            }
        });

        // TODO: clean up this name generation a bunch. probably a method.
        var imageName = name.Replace("/", "");
        var baseColor = BuildImage(baseColorTarget, $"{imageName}_basecolor");
        var normalThing = BuildImage(normal, $"{imageName}_normal");

        return BuildSharedBase(material, name)
            // NOTE: this isn't particularly precise to game behavior, but good enough for now.
            .WithAlpha(AlphaMode.MASK, 0.5f)
            .WithBaseColor(baseColor)
            .WithNormal(normalThing);
    }

    private static (float Smooth, float Stepped) GetTableRowIndices(float input)
    {
        // These calculations are ported from character.shpk.
        var smoothed = MathF.Floor(((input * 7.5f) % 1.0f) * 2) 
            * (-input * 15 + MathF.Floor(input * 15 + 0.5f))
            + input * 15;

        var stepped = MathF.Floor(smoothed + 0.5f);

        return (smoothed, stepped);
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
