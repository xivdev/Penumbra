using Lumina.Data.Parsing;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.Import.Textures;
using Penumbra.UI.FileEditing.Materials;
using SharpGLTF.Materials;

namespace Penumbra.Import.Models.Export;

public static class MaterialExporter
{
    public struct Material
    {
        public MtrlFile Mtrl;

        public Dictionary<TextureUsage, CustomBitmap> Textures;
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
        var indexTexture = ReadTexture(material.Textures[(TextureUsage)1449103320]);
        var indexOperation = new ProcessCharacterIndexOperation(indexTexture, table);
        Parallel.For(0, indexTexture.Height, indexOperation.Invoke);

        var normalTexture = ReadTexture(material.Textures[TextureUsage.SamplerNormal]);
        var normalOperation = new ProcessCharacterNormalOperation(normalTexture);
        Parallel.For(0, normalTexture.Height, normalOperation.Invoke);

        // Merge in opacity from the normal.
        var baseColor = indexOperation.BaseColor;
        MultiplyOperation.Execute(baseColor, normalOperation.BaseColorOpacity);

        // Check if a full diffuse is provided, and merge in if available.
        if (material.Textures.TryGetValue(TextureUsage.SamplerDiffuse, out var diffuse))
        {
            baseColor = ReadTexture(diffuse);
            MultiplyOperation.Execute(baseColor, indexOperation.BaseColor);
        }

        var specular = indexOperation.Specular;
        if (material.Textures.TryGetValue(TextureUsage.SamplerSpecular, out var specularTexture))
        {
            specular = ReadTexture(specularTexture);
            MultiplyOperation.Execute(specular, indexOperation.Specular);
        }

        // Pull further information from the mask.
        if (material.Textures.TryGetValue(TextureUsage.SamplerMask, out var maskTexture))
        {
            var maskOperation = new ProcessCharacterMaskOperation(ReadTexture(maskTexture));
            Parallel.For(0, maskTexture.Height, maskOperation.Invoke);

            // TODO 20260824 consider using the occusion gltf material property.
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

    private readonly struct ProcessCharacterIndexOperation(RgbaTexture index, ColorTable table)
    {
        public RgbaTexture BaseColor { get; } = new(index.Width, index.Height);
        public RgbaTexture Specular  { get; } = new(index.Width, index.Height);
        public RgbaTexture Emissive  { get; } = new(index.Width, index.Height);

        public void Invoke(int y)
        {
            var indexSpan = index.GetRowSpan(y);
            var baseColorSpan = BaseColor.GetRowSpan(y);
            var specularSpan  = Specular.GetRowSpan(y);
            var emissiveSpan  = Emissive.GetRowSpan(y);

            for (var x = 0; x < indexSpan.Length; x++)
            {
                ref var indexPixel = ref indexSpan[x];

                // Calculate and fetch the color table rows being used for this pixel.
                var tablePair = (int)Math.Round(indexPixel.R / 17f);
                var rowBlend = 1.0f - indexPixel.G / 255f;

                var prevRow = table[tablePair * 2];
                var nextRow = table[Math.Min(tablePair * 2 + 1, ColorTable.NumRows)];

                // Lerp between table row values to fetch final pixel values for each subtexture.
                var lerpedDiffuse = Vector3.Lerp((Vector3)prevRow.DiffuseColor, (Vector3)nextRow.DiffuseColor, rowBlend);
                baseColorSpan[x].FromVector4(new Vector4(MaterialEditor.PseudoSqrtRgb(lerpedDiffuse), 1));

                var lerpedSpecularColor = Vector3.Lerp((Vector3)prevRow.SpecularColor, (Vector3)nextRow.SpecularColor, rowBlend);
                specularSpan[x].FromVector4(new Vector4(MaterialEditor.PseudoSqrtRgb(lerpedSpecularColor), 1));

                var lerpedEmissive = Vector3.Lerp((Vector3)prevRow.EmissiveColor, (Vector3)nextRow.EmissiveColor, rowBlend);
                emissiveSpan[x].FromVector4(new Vector4(MaterialEditor.PseudoSqrtRgb(lerpedEmissive), 1));
            }
        }
    }
    
    private readonly struct ProcessCharacterNormalOperation(RgbaTexture normal)
    {
        // TODO 20260824 Consider omitting the alpha channel here.
        public RgbaTexture Normal { get; } = normal.Clone();
        // TODO 20260824 We only really need the alpha here, however using A8 will result in the multiply later zeroing out the RGB channels.
        public RgbaTexture BaseColorOpacity { get; } = new(normal.Width, normal.Height);

        public void Invoke(int y)
        {
            var normalSpan = Normal.GetRowSpan(y);
            var baseColorOpacitySpan = BaseColorOpacity.GetRowSpan(y);

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

    private readonly struct ProcessCharacterMaskOperation(RgbaTexture mask)
    {
        public RgbaTexture Occlusion { get; } = new(mask.Width, mask.Height);
        public RgbaTexture SpecularFactor { get; } = new(mask.Width, mask.Height);

        public void Invoke(int y)
        {
            var maskSpan = mask.GetRowSpan(y);
            var occlusionSpan = Occlusion.GetRowSpan(y);
            var specularFactorSpan = SpecularFactor.GetRowSpan(y);

            for (var x = 0; x < maskSpan.Length; x++)
            {
                ref var maskPixel = ref maskSpan[x];

                occlusionSpan[x] = new RgbaPixel(maskPixel.B, maskPixel.B, maskPixel.B, byte.MaxValue);
                
                specularFactorSpan[x].FromVector4(Vector4.One);
                specularFactorSpan[x].A = maskPixel.R;
            }
        }
    }

    private readonly struct MultiplyOperation
    {
        public static void Execute(RgbaTexture target, RgbaTexture multiplier)
        {
            // Match both axes, including textures with different aspect ratios.
            var width  = Math.Max(target.Width, multiplier.Width);
            var height = Math.Max(target.Height, multiplier.Height);
            target.Resize(width, height);
            multiplier.Resize(width, height);

            Parallel.For(0, height, y =>
            {
                var targetSpan     = target.GetRowSpan(y);
                var multiplierSpan = multiplier.GetRowSpan(y);
                for (var x = 0; x < targetSpan.Length; ++x)
                    targetSpan[x].FromVector4(targetSpan[x].ToVector4() * multiplierSpan[x].ToVector4());
            });
        }
    }

    private static readonly Vector4 DefaultTattooColor = new Vector4(38, 112, 102, 255) / new Vector4(255);

    private static MaterialBuilder BuildCharacterTattoo(Material material, string name)
    {
        var normal = ReadTexture(material.Textures[TextureUsage.SamplerNormal]);
        var baseColor = new RgbaTexture(normal.Width, normal.Height);

        Parallel.For(0, normal.Height, y =>
        {
            var normalSpan    = normal.GetRowSpan(y);
            var baseColorSpan = baseColor.GetRowSpan(y);

            for (var x = 0; x < normalSpan.Length; x++)
            {
                baseColorSpan[x].FromVector4(DefaultTattooColor);
                baseColorSpan[x].A = normalSpan[x].A;

                normalSpan[x].B = byte.MaxValue;
                normalSpan[x].A = byte.MaxValue;
            }
        });

        return BuildSharedBase(material, name)
            .WithBaseColor(BuildImage(baseColor, name, "basecolor"))
            .WithNormal(BuildImage(normal,       name, "normal"))
            .WithAlpha(AlphaMode.BLEND);
    }

    // TODO 20260824 These are hardcoded colours - I'm not keen on supporting highly customizable exports, but there's possibly some more sensible values to use here.
    private static readonly Vector4 DefaultHairColor      = new Vector4(130, 64,  13,  255) / new Vector4(255);
    private static readonly Vector4 DefaultHighlightColor = new Vector4(77,  126, 240, 255) / new Vector4(255);

    /// <summary> Build a material following the semantics of hair.shpk. </summary>
    private static MaterialBuilder BuildHair(Material material, string name)
    {
        // Trust me bro.
        const uint categoryHairType = 0x24826489;
        const uint valueFace        = 0x6E5B8F10;

        var isFace = material.Mtrl.ShaderPackage.ShaderKeys
            .Any(key => key is { Key: categoryHairType, Value: valueFace });

        var normal = ReadTexture(material.Textures[TextureUsage.SamplerNormal]);
        var mask   = ReadTexture(material.Textures[TextureUsage.SamplerMask]);

        mask.Resize(normal.Width, normal.Height);

        var baseColor = new RgbaTexture(normal.Width, normal.Height);
        Parallel.For(0, normal.Height, y =>
        {
            var normalSpan    = normal.GetRowSpan(y);
            var maskSpan      = mask.GetRowSpan(y);
            var baseColorSpan = baseColor.GetRowSpan(y);

            for (var x = 0; x < normalSpan.Length; x++)
            {
                var color = Vector4.Lerp(DefaultHairColor, DefaultHighlightColor, normalSpan[x].B / 255f);
                baseColorSpan[x].FromVector4(color * new Vector4(maskSpan[x].A / 255f));
                baseColorSpan[x].A = normalSpan[x].A;

                normalSpan[x].B = byte.MaxValue;
                normalSpan[x].A = byte.MaxValue;
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
        var normal    = ReadTexture(material.Textures[TextureUsage.SamplerNormal]);
        var mask      = ReadTexture(material.Textures[TextureUsage.SamplerMask]);
        var baseColor = ReadTexture(material.Textures[TextureUsage.SamplerDiffuse]);

        mask.Resize(baseColor.Width, baseColor.Height);

        Parallel.For(0, baseColor.Height, y =>
        {
            var baseColorSpan = baseColor.GetRowSpan(y);
            var maskSpan      = mask.GetRowSpan(y);

            for (var x = 0; x < baseColorSpan.Length; x++)
            {
                var eyeColor = Vector4.Lerp(Vector4.One, DefaultEyeColor, maskSpan[x].B / 255f);
                baseColorSpan[x].FromVector4(baseColorSpan[x].ToVector4() * eyeColor);
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
            .Any(key => key.Key == categorySkinType && key.Value != valueFace);

        // TODO 20260824 There's more nuance to skin than this, but this should be enough for a baseline reference.
        // TODO 20260824 Specular?
        var diffuse = ReadTexture(material.Textures[TextureUsage.SamplerDiffuse]);
        var normal  = ReadTexture(material.Textures[TextureUsage.SamplerNormal]);

        // The normal also stores the skin color influence (.b) and wetness mask (.a) - remove.
        Parallel.For(0, normal.Height, y =>
        {
            var normalSpan = normal.GetRowSpan(y);

            for (var x = 0; x < normalSpan.Length; x++)
            {
                normalSpan[x].B = byte.MaxValue;
                normalSpan[x].A = byte.MaxValue;
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
        // TODO 20260824 Move this and potentially the other known stuff into MtrlFile?
        const uint backfaceMask  = 0x1;
        var        showBackfaces = (material.Mtrl.ShaderPackage.Flags & backfaceMask) == 0;

        return new MaterialBuilder(name)
            .WithDoubleSide(showBackfaces);
    }

    private static RgbaTexture ReadTexture(CustomBitmap image)
    {
        var (pixels, width, height) = image.GetPixelData();
        return new RgbaTexture(width, height, pixels);
    }

    private static ImageBuilder BuildImage(RgbaTexture image, string materialName, string suffix)
    {
        using var bitmap = CustomBitmap.FromPixelData(new RgbaPixelData(image.Width, image.Height, image.Data));
        return BuildImage(bitmap, materialName, suffix);
    }

    /// <summary> Encode a bitmap as PNG for use with SharpGLTF. </summary>
    private static ImageBuilder BuildImage(CustomBitmap image, string materialName, string suffix)
    {
        var name = materialName.Replace("/", "").Replace(".mtrl", "") + $"_{suffix}";
        var imageBuilder = ImageBuilder.From(image.EncodePng(), name);
        imageBuilder.AlternateWriteFileName = $"{name}.*";
        return imageBuilder;
    }

    /// <summary> Owned straight-alpha RGBA data for channel processing, resized through CustomBitmap. </summary>
    private sealed class RgbaTexture(int width, int height, byte[] data)
    {
        public int Width { get; private set; } = width;
        public int Height { get; private set; } = height;
        public byte[] Data { get; private set; } = data;

        public RgbaTexture(int width, int height)
            : this(width, height, RgbaPixelData.NewPixelData((width, height)))
        { }

        public Span<RgbaPixel> GetRowSpan(int y)
            => MemoryMarshal.Cast<byte, RgbaPixel>(Data.AsSpan(y * Width * 4, Width * 4));

        public RgbaTexture Clone()
            => new(Width, Height, Data.ToArray());

        public void Resize(int width, int height)
        {
            if (Width == width && Height == height)
                return;

            var resized = CustomBitmap.Resize(new RgbaPixelData(Width, Height, Data), (width, height));
            Width  = resized.Width;
            Height = resized.Height;
            Data   = resized.PixelData;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct RgbaPixel(byte r, byte g, byte b, byte a)
    {
        public byte R = r;
        public byte G = g;
        public byte B = b;
        public byte A = a;

        public readonly Vector4 ToVector4()
            => new Vector4(R, G, B, A) / 255f;

        public void FromVector4(Vector4 value)
        {
            R = ToByte(value.X);
            G = ToByte(value.Y);
            B = ToByte(value.Z);
            A = ToByte(value.W);
        }

        private static byte ToByte(float value)
            => (byte)Math.Clamp(value * 255f + 0.5f, 0, 255);
    }
}
