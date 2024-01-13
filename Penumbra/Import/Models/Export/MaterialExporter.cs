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
            "character.shpk" => BuildCharacter(material, name),
            _                => BuildFallback(material, name),
        };
    }

    private static MaterialBuilder BuildCharacter(Material material, string name)
    {
        // TODO: handle models with an underlying diffuse
        var table = material.Mtrl.Table;

        // TODO: there's a few normal usages i should check, i think.
        // TODO: tryget
        var normal = material.Textures[TextureUsage.SamplerNormal];

        var operation = new ProcessCharacterNormalOperation(normal, table);
        ParallelRowIterator.IterateRows(ImageSharpConfiguration.Default, normal.Bounds(), in operation);

        // TODO: clean up this name generation a bunch. probably a method.
        var imageName = name.Replace("/", "").Replace(".mtrl", "");

        return BuildSharedBase(material, name)
            // NOTE: this isn't particularly precise to game behavior, but good enough for now.
            .WithAlpha(AlphaMode.MASK, 0.5f)
            .WithBaseColor(BuildImage(operation.BaseColor, $"{imageName}_basecolor"))
            .WithNormal(BuildImage(operation.Normal, $"{imageName}_normal"))
            .WithSpecularColor(BuildImage(operation.Specular, $"{imageName}_specular"))
            .WithEmissive(BuildImage(operation.Emissive, $"{imageName}_emissive"), Vector3.One, 1);
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
