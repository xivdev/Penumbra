using OtterGui.Services;
using Penumbra.Import.Textures;
using TextureType = Penumbra.Api.Enums.TextureType;

namespace Penumbra.Api.Api;

public class EditingApi(TextureManager textureManager) : IPenumbraApiEditing, IApiService
{
    public Task ConvertTextureFile(string inputFile, string outputFile, TextureType textureType, bool mipMaps)
        => textureType switch
        {
            TextureType.Png     => textureManager.SavePng(inputFile, outputFile),
            TextureType.Targa   => textureManager.SaveTga(inputFile, outputFile),
            TextureType.AsIsTex => textureManager.SaveAs(CombinedTexture.TextureSaveType.AsIs,   mipMaps, true,  inputFile, outputFile),
            TextureType.AsIsDds => textureManager.SaveAs(CombinedTexture.TextureSaveType.AsIs,   mipMaps, false, inputFile, outputFile),
            TextureType.RgbaTex => textureManager.SaveAs(CombinedTexture.TextureSaveType.Bitmap, mipMaps, true,  inputFile, outputFile),
            TextureType.RgbaDds => textureManager.SaveAs(CombinedTexture.TextureSaveType.Bitmap, mipMaps, false, inputFile, outputFile),
            TextureType.Bc3Tex  => textureManager.SaveAs(CombinedTexture.TextureSaveType.BC3,    mipMaps, true,  inputFile, outputFile),
            TextureType.Bc3Dds  => textureManager.SaveAs(CombinedTexture.TextureSaveType.BC3,    mipMaps, false, inputFile, outputFile),
            TextureType.Bc7Tex  => textureManager.SaveAs(CombinedTexture.TextureSaveType.BC7,    mipMaps, true,  inputFile, outputFile),
            TextureType.Bc7Dds  => textureManager.SaveAs(CombinedTexture.TextureSaveType.BC7,    mipMaps, false, inputFile, outputFile),
            _                   => Task.FromException(new Exception($"Invalid input value {textureType}.")),
        };

    // @formatter:off
    public Task ConvertTextureData(byte[] rgbaData, int width, string outputFile, TextureType textureType, bool mipMaps)
        => textureType switch
        {
            TextureType.Png     => textureManager.SavePng(new BaseImage(), outputFile, rgbaData, width, rgbaData.Length / 4 / width),
            TextureType.Targa   => textureManager.SaveTga(new BaseImage(), outputFile, rgbaData, width, rgbaData.Length / 4 / width),
            TextureType.AsIsTex => textureManager.SaveAs(CombinedTexture.TextureSaveType.AsIs,   mipMaps, true,  new BaseImage(), outputFile, rgbaData, width, rgbaData.Length / 4 / width),
            TextureType.AsIsDds => textureManager.SaveAs(CombinedTexture.TextureSaveType.AsIs,   mipMaps, false, new BaseImage(), outputFile, rgbaData, width, rgbaData.Length / 4 / width),
            TextureType.RgbaTex => textureManager.SaveAs(CombinedTexture.TextureSaveType.Bitmap, mipMaps, true,  new BaseImage(), outputFile, rgbaData, width, rgbaData.Length / 4 / width),
            TextureType.RgbaDds => textureManager.SaveAs(CombinedTexture.TextureSaveType.Bitmap, mipMaps, false, new BaseImage(), outputFile, rgbaData, width, rgbaData.Length / 4 / width),
            TextureType.Bc3Tex  => textureManager.SaveAs(CombinedTexture.TextureSaveType.BC3,    mipMaps, true,  new BaseImage(), outputFile, rgbaData, width, rgbaData.Length / 4 / width),
            TextureType.Bc3Dds  => textureManager.SaveAs(CombinedTexture.TextureSaveType.BC3,    mipMaps, false, new BaseImage(), outputFile, rgbaData, width, rgbaData.Length / 4 / width),
            TextureType.Bc7Tex  => textureManager.SaveAs(CombinedTexture.TextureSaveType.BC7,    mipMaps, true,  new BaseImage(), outputFile, rgbaData, width, rgbaData.Length / 4 / width),
            TextureType.Bc7Dds  => textureManager.SaveAs(CombinedTexture.TextureSaveType.BC7,    mipMaps, false, new BaseImage(), outputFile, rgbaData, width, rgbaData.Length / 4 / width),
            _                   => Task.FromException(new Exception($"Invalid input value {textureType}.")),
        };
    // @formatter:on
}
