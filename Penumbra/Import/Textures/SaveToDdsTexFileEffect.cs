using Dalamud.Plugin.Services;
using Luna.DirectX;
using OtterTex;

namespace Penumbra.Import.Textures;

/// <summary> An image processing effect that saves its input to a file. </summary>
/// <param name="readbackProvider"> Dalamud's texture readback provider. </param>
/// <param name="textureManager"> The texture manager service. </param>
/// <param name="asTex"> Whether to save as TEX. </param>
/// <param name="path"> The path to save at. </param>
/// <param name="textureSaveType"> Which texture compression type to use. </param>
/// <param name="mipMaps"> Whether to add mipmaps to the saved texture. </param>
public class SaveToDdsTexFileEffect(
    ITextureReadbackProvider readbackProvider,
    TextureManager textureManager,
    bool asTex,
    string path,
    CombinedTexture.TextureSaveType textureSaveType = CombinedTexture.TextureSaveType.AsIs,
    bool? mipMaps = null) : ScratchImageReadbackEffect(readbackProvider)
{
    /// <summary> Constructs a <see cref="SaveToDdsTexFileEffect"/>, inferring the format from the path's extension. </summary>
    /// <param name="readbackProvider"> Dalamud's texture readback provider. </param>
    /// <param name="textureManager"> The texture manager service. </param>
    /// <param name="path"> The path to save at. </param>
    /// <param name="textureSaveType"> Which texture compression type to use. </param>
    /// <param name="mipMaps"> Whether to add mipmaps to the saved texture. </param>
    public SaveToDdsTexFileEffect(ITextureReadbackProvider readbackProvider, TextureManager textureManager, string path,
        CombinedTexture.TextureSaveType textureSaveType = CombinedTexture.TextureSaveType.AsIs,
        bool? mipMaps = null)
        : this(readbackProvider, textureManager, IsTex(path), path, textureSaveType, mipMaps)
    { }

    /// <inheritdoc/>
    protected override async Task Run(ScratchImage scratch, CancellationToken cancellationToken)
    {
        var converted = textureSaveType switch
        {
            CombinedTexture.TextureSaveType.AsIs   => mipMaps is { } mips ? TextureManager.AddMipMaps(scratch, mips) : scratch,
            CombinedTexture.TextureSaveType.Bitmap => TextureManager.CreateUncompressed(scratch, mipMaps ?? false, cancellationToken),
            CombinedTexture.TextureSaveType.BC1 => await textureManager.CreateCompressedAsync(scratch, mipMaps ?? false, DXGIFormat.BC1UNorm,
                cancellationToken),
            CombinedTexture.TextureSaveType.BC3 => await textureManager.CreateCompressedAsync(scratch, mipMaps ?? false, DXGIFormat.BC3UNorm,
                cancellationToken),
            CombinedTexture.TextureSaveType.BC4 => await textureManager.CreateCompressedAsync(scratch, mipMaps ?? false, DXGIFormat.BC4UNorm,
                cancellationToken),
            CombinedTexture.TextureSaveType.BC5 => await textureManager.CreateCompressedAsync(scratch, mipMaps ?? false, DXGIFormat.BC5UNorm,
                cancellationToken),
            CombinedTexture.TextureSaveType.BC7 => await textureManager.CreateCompressedAsync(scratch, mipMaps ?? false, DXGIFormat.BC7UNorm,
                cancellationToken),
            _ => throw new Exception("Wrong save type."),
        };

        try
        {
            if (asTex)
                TextureManager.SaveTex(path, converted);
            else
                converted.SaveDDS(path);
        }
        finally
        {
            if (converted != scratch)
                converted.Dispose();
        }
    }

    private static bool IsTex(string? path)
        => Path.GetExtension(path)?.ToLowerInvariant() is ".tex" or ".atex";
}
