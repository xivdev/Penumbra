using Lumina.Data.Parsing;
using Penumbra.GameData.Files;
using SharpGLTF.Materials;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Penumbra.Import.Models.Export;

using ImageSharpConfiguration = SixLabors.ImageSharp.Configuration;

public class MaterialExporter
{
    public struct Material
    {
        public MtrlFile Mtrl;
        public Dictionary<TextureUsage, Image<Rgba32>> Textures;
        // variant?
    }

    public static MaterialBuilder Export(Material material, string name)
    {
        Penumbra.Log.Debug($"Exporting material \"{name}\".");
        return material.Mtrl.ShaderPackage.Name switch
        {
            // NOTE: this isn't particularly precise to game behavior (it has some fade around high opacity), but good enough for now.
            "character.shpk"      => BuildCharacter(material, name).WithAlpha(AlphaMode.MASK, 0.5f),
            "characterglass.shpk" => BuildCharacter(material, name).WithAlpha(AlphaMode.BLEND),
            _                     => BuildFallback(material, name),
        };
    }

    private static MaterialBuilder BuildCharacter(Material material, string name)
    {
        var table = material.Mtrl.Table;

        // TODO: there's a few normal usages i should check, i think.
        var normal = material.Textures[TextureUsage.SamplerNormal];

        var operation = new ProcessCharacterNormalOperation(normal, table);
        ParallelRowIterator.IterateRows(ImageSharpConfiguration.Default, normal.Bounds(), in operation);

        Image baseColor = operation.BaseColor;
        if (material.Textures.TryGetValue(TextureUsage.SamplerDiffuse, out var diffuse))
        {
            MultiplyOperation.Execute(diffuse, operation.BaseColor);
            baseColor = diffuse;
        }

        // TODO: what about the two specularmaps?
        Image specular = operation.Specular;
        if (material.Textures.TryGetValue(TextureUsage.SamplerSpecular, out var specularTexture))
        {
            MultiplyOperation.Execute(specularTexture, operation.Specular);
            specular = specularTexture;
        }

        Image? occlusion = null;
        if (material.Textures.TryGetValue(TextureUsage.SamplerMask, out var maskTexture))
        {
            // Extract the red channel for ambient occlusion.
            maskTexture.Mutate(context => context.Filter(new ColorMatrix(
                1f, 1f, 1f, 0f,
                0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f,
                0f, 0f, 0f, 1f,
                0f, 0f, 0f, 0f
            )));
            occlusion = maskTexture;
            
            // TODO: handle other textures stored in the mask?
        }

        // TODO: clean up this name generation a bunch. probably a method.
        var imageName = name.Replace("/", "").Replace(".mtrl", "");

        var materialBuilder = BuildSharedBase(material, name)
            .WithBaseColor(BuildImage(baseColor, $"{imageName}_basecolor"))
            .WithNormal(BuildImage(operation.Normal, $"{imageName}_normal"))
            .WithSpecularColor(BuildImage(specular, $"{imageName}_specular"))
            .WithEmissive(BuildImage(operation.Emissive, $"{imageName}_emissive"), Vector3.One, 1);

        if (occlusion != null)
            materialBuilder.WithOcclusion(BuildImage(occlusion, $"{imageName}_occlusion"));

        return materialBuilder;
    }

    // TODO: It feels a little silly to request the entire normal here when extrating the normal only needs some of the components.
    //       As a future refactor, it would be neat to accept a single-channel field here, and then do composition of other stuff later.
    private readonly struct ProcessCharacterNormalOperation(Image<Rgba32> normal, MtrlFile.ColorTable table) : IRowOperation
    {
        public Image<Rgba32> Normal { get; private init; } = normal.Clone();
        public Image<Rgba32> BaseColor { get; private init; } = new Image<Rgba32>(normal.Width, normal.Height);
        public Image<Rgb24> Specular { get; private init; } = new Image<Rgb24>(normal.Width, normal.Height);
        public Image<Rgb24> Emissive { get; private init; } = new Image<Rgb24>(normal.Width, normal.Height);

        private Buffer2D<Rgba32> NormalBuffer => Normal.Frames.RootFrame.PixelBuffer;
        private Buffer2D<Rgba32> BaseColorBuffer => BaseColor.Frames.RootFrame.PixelBuffer;
        private Buffer2D<Rgb24> SpecularBuffer => Specular.Frames.RootFrame.PixelBuffer;
        private Buffer2D<Rgb24> EmissiveBuffer => Emissive.Frames.RootFrame.PixelBuffer;

        public void Invoke(int y)
        {
            var normalSpan = NormalBuffer.DangerousGetRowSpan(y);
            var baseColorSpan = BaseColorBuffer.DangerousGetRowSpan(y);
            var specularSpan = SpecularBuffer.DangerousGetRowSpan(y);
            var emissiveSpan = EmissiveBuffer.DangerousGetRowSpan(y);

            for (int x = 0; x < normalSpan.Length; x++)
            {
                ref var normalPixel = ref normalSpan[x];

                // Table row data (.a)
                var tableRow = GetTableRowIndices(normalPixel.A / 255f);
                var prevRow = table[tableRow.Previous];
                var nextRow = table[tableRow.Next];

                // Base colour (table, .b)
                var lerpedDiffuse = Vector3.Lerp(prevRow.Diffuse, nextRow.Diffuse, tableRow.Weight);
                baseColorSpan[x].FromVector4(new Vector4(lerpedDiffuse, 1));
                baseColorSpan[x].A = normalPixel.B;

                // Specular (table)
                var lerpedSpecularColor = Vector3.Lerp(prevRow.Specular, nextRow.Specular, tableRow.Weight);
                specularSpan[x].FromVector4(new Vector4(lerpedSpecularColor, 1));

                // Emissive (table)
                var lerpedEmissive = Vector3.Lerp(prevRow.Emissive, nextRow.Emissive, tableRow.Weight);
                emissiveSpan[x].FromVector4(new Vector4(lerpedEmissive, 1));

                // Normal (.rg)
                // TODO: we don't actually need alpha at all for normal, but _not_ using the existing rgba texture means I'll need a new one, with a new accessor. Think about it.
                normalPixel.B = byte.MaxValue;
                normalPixel.A = byte.MaxValue;
            }
        }
    }

    private readonly struct MultiplyOperation
    {
        public static void Execute<TPixel1, TPixel2>(Image<TPixel1> target, Image<TPixel2> multiplier)
            where TPixel1 : unmanaged, IPixel<TPixel1>
            where TPixel2 : unmanaged, IPixel<TPixel2>
        {
            // Ensure the images are the same size
            var (small, large) = target.Width < multiplier.Width && target.Height < multiplier.Height
                ? ((Image)target, (Image)multiplier)
                : (multiplier, target);
            small.Mutate(context => context.Resize(large.Width, large.Height));

            var operation = new MultiplyOperation<TPixel1, TPixel2>(target, multiplier);
            ParallelRowIterator.IterateRows(ImageSharpConfiguration.Default, target.Bounds(), in operation);
        }
    }

    private readonly struct MultiplyOperation<TPixel1, TPixel2>(Image<TPixel1> target, Image<TPixel2> multiplier) : IRowOperation
        where TPixel1 : unmanaged, IPixel<TPixel1>
        where TPixel2 : unmanaged, IPixel<TPixel2>
    {

        public void Invoke(int y)
        {
            var targetSpan = target.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            var multiplierSpan = multiplier.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);

            for (int x = 0; x < targetSpan.Length; x++)
            {
                targetSpan[x].FromVector4(targetSpan[x].ToVector4() * multiplierSpan[x].ToVector4());
            }
        }
    }

    private static TableRow GetTableRowIndices(float input)
    {
        // These calculations are ported from character.shpk.
        var smoothed = MathF.Floor(((input * 7.5f) % 1.0f) * 2) 
            * (-input * 15 + MathF.Floor(input * 15 + 0.5f))
            + input * 15;

        var stepped = MathF.Floor(smoothed + 0.5f);

        return new TableRow
        {
            Stepped  = (int)stepped,
            Previous = (int)MathF.Floor(smoothed),
            Next     = (int)MathF.Ceiling(smoothed),
            Weight   = smoothed % 1,
        };
    }

    private ref struct TableRow
    {
        public int Stepped;
        public int Previous;
        public int Next;
        public float Weight;
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
