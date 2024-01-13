using Lumina.Data.Parsing;
using Penumbra.GameData.Files;
using SharpGLTF.Materials;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace Penumbra.Import.Models.Export;

using ImageSharpConfiguration = SixLabors.ImageSharp.Configuration;

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
        // TODO: handle models with an underlying diffuse
        var table = material.Mtrl.Table;
        // TODO: this should probably be a dict
        var normal = material.Samplers
            .Where(s => s.Usage == TextureUsage.SamplerNormal)
            .First()
            .Texture;

        var operation = new CharacterOperation()
        {
            Table = table,
            Normal = normal,
            BaseColor = new Image<Rgba32>(normal.Width, normal.Height),
            Emissive = new Image<Rgb24>(normal.Width, normal.Height),
        };
        ParallelRowIterator.IterateRows(ImageSharpConfiguration.Default, normal.Bounds(), in operation);

        // TODO: clean up this name generation a bunch. probably a method.
        var imageName = name.Replace("/", "");

        return BuildSharedBase(material, name)
            // .WithSpecularGlossinessShader()
            // .WithDiffuse()
            // NOTE: this isn't particularly precise to game behavior, but good enough for now.
            .WithAlpha(AlphaMode.MASK, 0.5f)
            .WithBaseColor(BuildImage(operation.BaseColor, $"{imageName}_basecolor"))
            .WithNormal(BuildImage(operation.Normal, $"{imageName}_normal"))
            .WithEmissive(BuildImage(operation.Emissive, $"{imageName}_emissive"), Vector3.One, 1);
    }

    private readonly struct CharacterOperation : IRowOperation
    {
        public required MtrlFile.ColorTable Table { get; init; }

        public required Image<Rgba32> Normal { get; init; }
        public required Image<Rgba32> BaseColor { get; init; }
        public required Image<Rgb24> Emissive { get; init; }

        private Buffer2D<Rgba32> NormalBuffer => Normal.Frames.RootFrame.PixelBuffer;
        private Buffer2D<Rgba32> BaseColorBuffer => BaseColor.Frames.RootFrame.PixelBuffer;
        private Buffer2D<Rgb24> EmissiveBuffer => Emissive.Frames.RootFrame.PixelBuffer;

        public void Invoke(int y)
        {
            var normalSpan = NormalBuffer.DangerousGetRowSpan(y);
            var baseColorSpan = BaseColorBuffer.DangerousGetRowSpan(y);
            var emissiveSpan = EmissiveBuffer.DangerousGetRowSpan(y);

            for (int x = 0; x < normalSpan.Length; x++)
            {
                ref var normalPixel = ref normalSpan[x];
                ref var baseColorPixel = ref baseColorSpan[x];
                ref var emissivePixel = ref emissiveSpan[x];

                // Table row data (.a)
                var (smoothed, stepped) = GetTableRowIndices(normalPixel.A / 255f);
                var weight = smoothed % 1;
                var prevRow = Table[(int)MathF.Floor(smoothed)];
                var nextRow = Table[(int)MathF.Ceiling(smoothed)];

                // Base colour (table, .b)
                var lerpedDiffuse = Vector3.Lerp(prevRow.Diffuse, nextRow.Diffuse, weight);
                baseColorPixel.FromVector4(new Vector4(lerpedDiffuse, 1));
                baseColorPixel.A = normalPixel.B;

                // Emissive (table)
                var lerpedEmissive = Vector3.Lerp(prevRow.Emissive, nextRow.Emissive, weight);
                emissivePixel.FromVector4(new Vector4(lerpedEmissive, 1));

                // Normal (.rg)
                // TODO: we don't actually need alpha at all for normal, but _not_ using the existing rgba texture means I'll need a new one, with a new accessor. Think about it.
                normalPixel.B = byte.MaxValue;
                normalPixel.A = byte.MaxValue;
            }
        }
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

    private static ImageBuilder BuildImage(Image image, string name)
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
