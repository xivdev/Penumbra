using Lumina.Data.Parsing;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.MaterialStructs;
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

    /// <summary> Dependency-less material configuration, for use when no material data can be resolved. </summary>
    public static readonly MaterialBuilder Unknown = new MaterialBuilder("UNKNOWN")
        .WithMetallicRoughnessShader()
        .WithDoubleSide(true)
        .WithBaseColor(Vector4.One);

    /// <summary> Build a glTF material from a hydrated XIV model, with the provided name. </summary>
    public static MaterialBuilder Export(Material material, string name, IoNotifier notifier)
    {
        Penumbra.Log.Debug($"Exporting material \"{name}\".");
        return material.Mtrl.ShaderPackage.Name switch
        {
            // NOTE: this isn't particularly precise to game behavior (it has some fade around high opacity), but good enough for now.
            "character.shpk"       => BuildCharacter(material, name).WithAlpha(AlphaMode.MASK, 0.5f),
            "characterlegacy.shpk" => BuildCharacter(material, name).WithAlpha(AlphaMode.MASK, 0.5f),
            "characterglass.shpk"  => BuildCharacter(material, name).WithAlpha(AlphaMode.BLEND),
            "charactertattoo.shpk" => BuildCharacterTattoo(material, name),
            "hair.shpk"            => BuildHair(material, name),
            "iris.shpk"            => BuildIris(material, name),
            "skin.shpk"            => BuildSkin(material, name),
            _                      => BuildFallback(material, name, notifier),
        };
    }

    /// <summary> Build a material following the semantics of character.shpk. </summary>
    private static MaterialBuilder BuildCharacter(Material material, string name)
    {
        // Build the textures from the color table.
        var table = new ColorTable(material.Mtrl.Table!);
        var indexTexture = material.Textures[(TextureUsage)1449103320];
        var indexOperation = new ProcessCharacterIndexOperation(indexTexture, table);
        ParallelRowIterator.IterateRows(ImageSharpConfiguration.Default, indexTexture.Bounds, in indexOperation);

        var normalTexture = material.Textures[TextureUsage.SamplerNormal];
        var normalOperation = new ProcessCharacterNormalOperation(normalTexture);
        ParallelRowIterator.IterateRows(ImageSharpConfiguration.Default, normalTexture.Bounds, in normalOperation);

        // Merge in opacity from the normal.
        var baseColor = indexOperation.BaseColor;
        MultiplyOperation.Execute(baseColor, normalOperation.BaseColorOpacity);

        // Check if a full diffuse is provided, and merge in if available.
        if (material.Textures.TryGetValue(TextureUsage.SamplerDiffuse, out var diffuse))
        {
            MultiplyOperation.Execute(diffuse, indexOperation.BaseColor);
            baseColor = diffuse;
        }

        var specular = indexOperation.Specular;
        if (material.Textures.TryGetValue(TextureUsage.SamplerSpecular, out var specularTexture))
        {
            MultiplyOperation.Execute(specularTexture, indexOperation.Specular);
            specular = specularTexture;
        }

        // Pull further information from the mask.
        if (material.Textures.TryGetValue(TextureUsage.SamplerMask, out var maskTexture))
        {
            var maskOperation = new ProcessCharacterMaskOperation(maskTexture);
            ParallelRowIterator.IterateRows(ImageSharpConfiguration.Default, maskTexture.Bounds, in maskOperation);

            // TODO: consider using the occusion gltf material property.
            MultiplyOperation.Execute(baseColor, maskOperation.Occlusion);

            // Similar to base color's alpha, this is a pretty wasteful operation for a single channel.
            MultiplyOperation.Execute(specular, maskOperation.SpecularFactor);
        }

        // Specular extension puts colour on RGB and factor on A. We're already packing like that, so we can reuse the texture.
        var specularImage = BuildImage(specular, name, "specular");

        return BuildSharedBase(material, name)
            .WithBaseColor(BuildImage(baseColor,              name, "basecolor"))
            .WithNormal(BuildImage(normalOperation.Normal,    name, "normal"))
            .WithEmissive(BuildImage(indexOperation.Emissive, name, "emissive"), Vector3.One, 1)
            .WithSpecularFactor(specularImage, 1)
            .WithSpecularColor(specularImage);
    }

    private readonly struct ProcessCharacterIndexOperation(Image<Rgba32> index, ColorTable table) : IRowOperation
    {
        public Image<Rgba32> BaseColor { get; } = new(index.Width, index.Height);
        public Image<Rgba32> Specular  { get; } = new(index.Width, index.Height);
        public Image<Rgb24>  Emissive  { get; } = new(index.Width, index.Height);

        private Buffer2D<Rgba32> IndexBuffer
            => index.Frames.RootFrame.PixelBuffer;

        private Buffer2D<Rgba32> BaseColorBuffer
            => BaseColor.Frames.RootFrame.PixelBuffer;

        private Buffer2D<Rgba32> SpecularBuffer
            => Specular.Frames.RootFrame.PixelBuffer;

        private Buffer2D<Rgb24> EmissiveBuffer
            => Emissive.Frames.RootFrame.PixelBuffer;

        public void Invoke(int y)
        {
            var indexSpan = IndexBuffer.DangerousGetRowSpan(y);
            var baseColorSpan = BaseColorBuffer.DangerousGetRowSpan(y);
            var specularSpan  = SpecularBuffer.DangerousGetRowSpan(y);
            var emissiveSpan  = EmissiveBuffer.DangerousGetRowSpan(y);

            for (var x = 0; x < indexSpan.Length; x++)
            {
                ref var indexPixel = ref indexSpan[x];

                // Calculate and fetch the color table rows being used for this pixel.
                var tablePair = (int) Math.Round(indexPixel.R / 17f);
                var rowBlend = 1.0f - indexPixel.G / 255f;

                var prevRow = table[tablePair * 2];
                var nextRow = table[Math.Min(tablePair * 2 + 1, ColorTable.NumRows)];

                // Lerp between table row values to fetch final pixel values for each subtexture.
                var lerpedDiffuse = Vector3.Lerp((Vector3)prevRow.DiffuseColor, (Vector3)nextRow.DiffuseColor, rowBlend);
                baseColorSpan[x].FromVector4(new Vector4(lerpedDiffuse, 1));

                var lerpedSpecularColor = Vector3.Lerp((Vector3)prevRow.SpecularColor, (Vector3)nextRow.SpecularColor, rowBlend);
                specularSpan[x].FromVector4(new Vector4(lerpedSpecularColor, 1));

                var lerpedEmissive = Vector3.Lerp((Vector3)prevRow.EmissiveColor, (Vector3)nextRow.EmissiveColor, rowBlend);
                emissiveSpan[x].FromVector4(new Vector4(lerpedEmissive, 1));
            }
        }
    }
    
    private readonly struct ProcessCharacterNormalOperation(Image<Rgba32> normal) : IRowOperation
    {
        // TODO: Consider omitting the alpha channel here.
        public Image<Rgba32> Normal { get; } = normal.Clone();
        // TODO: We only really need the alpha here, however using A8 will result in the multiply later zeroing out the RGB channels.
        public Image<Rgba32> BaseColorOpacity { get; } = new(normal.Width, normal.Height);

        private Buffer2D<Rgba32> NormalBuffer
            => Normal.Frames.RootFrame.PixelBuffer;

        private Buffer2D<Rgba32> BaseColorOpacityBuffer
            => BaseColorOpacity.Frames.RootFrame.PixelBuffer;

        public void Invoke(int y)
        {
            var normalSpan = NormalBuffer.DangerousGetRowSpan(y);
            var baseColorOpacitySpan = BaseColorOpacityBuffer.DangerousGetRowSpan(y);

            for (var x = 0; x < normalSpan.Length; x++)
            {
                ref var normalPixel = ref normalSpan[x];

                baseColorOpacitySpan[x].FromVector4(Vector4.One);
                baseColorOpacitySpan[x].A = normalPixel.B;

                normalPixel.B = byte.MaxValue;
                normalPixel.A = byte.MaxValue;
            }
        }
    }

    private readonly struct ProcessCharacterMaskOperation(Image<Rgba32> mask) : IRowOperation
    {
        public Image<Rgba32> Occlusion { get; } = new(mask.Width, mask.Height);
        public Image<Rgba32> SpecularFactor { get; } = new(mask.Width, mask.Height);

        private Buffer2D<Rgba32> MaskBuffer
            => mask.Frames.RootFrame.PixelBuffer;

        private Buffer2D<Rgba32> OcclusionBuffer
            => Occlusion.Frames.RootFrame.PixelBuffer;

        private Buffer2D<Rgba32> SpecularFactorBuffer
            => SpecularFactor.Frames.RootFrame.PixelBuffer;

        public void Invoke(int y)
        {
            var maskSpan = MaskBuffer.DangerousGetRowSpan(y);
            var occlusionSpan = OcclusionBuffer.DangerousGetRowSpan(y);
            var specularFactorSpan = SpecularFactorBuffer.DangerousGetRowSpan(y);

            for (var x = 0; x < maskSpan.Length; x++)
            {
                ref var maskPixel = ref maskSpan[x];

                occlusionSpan[x].FromL8(new L8(maskPixel.B));
                
                specularFactorSpan[x].FromVector4(Vector4.One);
                specularFactorSpan[x].A = maskPixel.R;
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
            ParallelRowIterator.IterateRows(ImageSharpConfiguration.Default, target.Bounds, in operation);
        }
    }

    private readonly struct MultiplyOperation<TPixel1, TPixel2>(Image<TPixel1> target, Image<TPixel2> multiplier) : IRowOperation
        where TPixel1 : unmanaged, IPixel<TPixel1>
        where TPixel2 : unmanaged, IPixel<TPixel2>
    {
        public void Invoke(int y)
        {
            var targetSpan     = target.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);
            var multiplierSpan = multiplier.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(y);

            for (var x = 0; x < targetSpan.Length; x++)
                targetSpan[x].FromVector4(targetSpan[x].ToVector4() * multiplierSpan[x].ToVector4());
        }
    }

    private static readonly Vector4 DefaultTattooColor = new Vector4(38, 112, 102, 255) / new Vector4(255);

    private static MaterialBuilder BuildCharacterTattoo(Material material, string name)
    {
        var normal = material.Textures[TextureUsage.SamplerNormal];
        var baseColor = new Image<Rgba32>(normal.Width, normal.Height);

        normal.ProcessPixelRows(baseColor, (normalAccessor, baseColorAccessor) =>
        {
            for (var y = 0; y < normalAccessor.Height; y++)
            {
                var normalSpan    = normalAccessor.GetRowSpan(y);
                var baseColorSpan = baseColorAccessor.GetRowSpan(y);

                for (var x = 0; x < normalSpan.Length; x++)
                {
                    baseColorSpan[x].FromVector4(DefaultTattooColor);
                    baseColorSpan[x].A = normalSpan[x].A;

                    normalSpan[x].B = byte.MaxValue;
                    normalSpan[x].A = byte.MaxValue;
                }
            }
        });

        return BuildSharedBase(material, name)
            .WithBaseColor(BuildImage(baseColor, name, "basecolor"))
            .WithNormal(BuildImage(normal,       name, "normal"))
            .WithAlpha(AlphaMode.BLEND);
    }

    // TODO: These are hardcoded colours - I'm not keen on supporting highly customizable exports, but there's possibly some more sensible values to use here.
    private static readonly Vector4 DefaultHairColor      = new Vector4(130, 64,  13,  255) / new Vector4(255);
    private static readonly Vector4 DefaultHighlightColor = new Vector4(77,  126, 240, 255) / new Vector4(255);

    /// <summary> Build a material following the semantics of hair.shpk. </summary>
    private static MaterialBuilder BuildHair(Material material, string name)
    {
        // Trust me bro.
        const uint categoryHairType = 0x24826489;
        const uint valueFace        = 0x6E5B8F10;

        var isFace = material.Mtrl.ShaderPackage.ShaderKeys
            .Any(key => key is { Category: categoryHairType, Value: valueFace });

        var normal = material.Textures[TextureUsage.SamplerNormal];
        var mask   = material.Textures[TextureUsage.SamplerMask];

        mask.Mutate(context => context.Resize(normal.Width, normal.Height));

        var baseColor = new Image<Rgba32>(normal.Width, normal.Height);
        normal.ProcessPixelRows(mask, baseColor, (normalAccessor, maskAccessor, baseColorAccessor) =>
        {
            for (var y = 0; y < normalAccessor.Height; y++)
            {
                var normalSpan    = normalAccessor.GetRowSpan(y);
                var maskSpan      = maskAccessor.GetRowSpan(y);
                var baseColorSpan = baseColorAccessor.GetRowSpan(y);

                for (var x = 0; x < normalSpan.Length; x++)
                {
                    var color = Vector4.Lerp(DefaultHairColor, DefaultHighlightColor, normalSpan[x].B / 255f);
                    baseColorSpan[x].FromVector4(color * new Vector4(maskSpan[x].A / 255f));
                    baseColorSpan[x].A = normalSpan[x].A;

                    normalSpan[x].B = byte.MaxValue;
                    normalSpan[x].A = byte.MaxValue;
                }
            }
        });

        return BuildSharedBase(material, name)
            .WithBaseColor(BuildImage(baseColor, name, "basecolor"))
            .WithNormal(BuildImage(normal,       name, "normal"))
            .WithAlpha(isFace ? AlphaMode.BLEND : AlphaMode.MASK, 0.5f);
    }

    private static readonly Vector4 DefaultEyeColor = new Vector4(21, 176, 172, 255) / new Vector4(255);

    /// <summary> Build a material following the semantics of iris.shpk. </summary>
    // NOTE: This is largely the same as the hair material, but is also missing a few features that would cause it to diverge. Keeping separate for now.
    private static MaterialBuilder BuildIris(Material material, string name)
    {
        var normal    = material.Textures[TextureUsage.SamplerNormal];
        var mask      = material.Textures[TextureUsage.SamplerMask];
        var baseColor = material.Textures[TextureUsage.SamplerDiffuse];

        mask.Mutate(context => context.Resize(baseColor.Width, baseColor.Height));

        baseColor.ProcessPixelRows(mask, (baseColorAccessor, maskAccessor) =>
        {
            for (var y = 0; y < baseColor.Height; y++)
            {
                var baseColorSpan = baseColorAccessor.GetRowSpan(y);
                var maskSpan      = maskAccessor.GetRowSpan(y);

                for (var x = 0; x < baseColorSpan.Length; x++)
                {
                    var eyeColor = Vector4.Lerp(Vector4.One, DefaultEyeColor, maskSpan[x].B / 255f);
                    baseColorSpan[x].FromVector4(baseColorSpan[x].ToVector4() * eyeColor);
                }
            }
        });

        return BuildSharedBase(material, name)
            .WithBaseColor(BuildImage(baseColor, name, "basecolor"))
            .WithNormal(BuildImage(normal,       name, "normal"));
    }

    /// <summary> Build a material following the semantics of skin.shpk. </summary>
    private static MaterialBuilder BuildSkin(Material material, string name)
    {
        // Trust me bro.
        const uint categorySkinType = 0x380CAED0;
        const uint valueFace        = 0xF5673524;

        // Face is the default for the skin shader, so a lack of skin type category is also correct.
        var isFace = !material.Mtrl.ShaderPackage.ShaderKeys
            .Any(key => key.Category == categorySkinType && key.Value != valueFace);

        // TODO: There's more nuance to skin than this, but this should be enough for a baseline reference.
        // TODO: Specular?
        var diffuse = material.Textures[TextureUsage.SamplerDiffuse];
        var normal  = material.Textures[TextureUsage.SamplerNormal];

        // The normal also stores the skin color influence (.b) and wetness mask (.a) - remove.
        normal.ProcessPixelRows(normalAccessor =>
        {
            for (var y = 0; y < normalAccessor.Height; y++)
            {
                var normalSpan = normalAccessor.GetRowSpan(y);

                for (var x = 0; x < normalSpan.Length; x++)
                {
                    normalSpan[x].B = byte.MaxValue;
                    normalSpan[x].A = byte.MaxValue;
                }
            }
        });

        return BuildSharedBase(material, name)
            .WithBaseColor(BuildImage(diffuse, name, "basecolor"))
            .WithNormal(BuildImage(normal,     name, "normal"))
            .WithAlpha(isFace ? AlphaMode.MASK : AlphaMode.OPAQUE, 0.5f);
    }

    /// <summary> Build a material from a source with unknown semantics. </summary>
    /// <remarks> Will make a loose effort to fetch common / simple textures. </remarks>
    private static MaterialBuilder BuildFallback(Material material, string name, IoNotifier notifier)
    {
        notifier.Warning($"Unhandled shader package: {material.Mtrl.ShaderPackage.Name}");

        var materialBuilder = BuildSharedBase(material, name)
            .WithMetallicRoughnessShader()
            .WithBaseColor(Vector4.One);

        if (material.Textures.TryGetValue(TextureUsage.SamplerDiffuse, out var diffuse))
            materialBuilder.WithBaseColor(BuildImage(diffuse, name, "basecolor"));

        if (material.Textures.TryGetValue(TextureUsage.SamplerNormal, out var normal))
            materialBuilder.WithNormal(BuildImage(normal, name, "normal"));

        return materialBuilder;
    }

    /// <summary> Build a material pre-configured with settings common to all XIV materials/shaders. </summary>
    private static MaterialBuilder BuildSharedBase(Material material, string name)
    {
        // TODO: Move this and potentially the other known stuff into MtrlFile?
        const uint backfaceMask  = 0x1;
        var        showBackfaces = (material.Mtrl.ShaderPackage.Flags & backfaceMask) == 0;

        return new MaterialBuilder(name)
            .WithDoubleSide(showBackfaces);
    }

    /// <summary> Convert an ImageSharp Image into an ImageBuilder for use with SharpGLTF. </summary>
    private static ImageBuilder BuildImage(Image image, string materialName, string suffix)
    {
        var name = materialName.Replace("/", "").Replace(".mtrl", "") + $"_{suffix}";

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
