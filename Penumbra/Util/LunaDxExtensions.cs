using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Luna.DirectX;
using OtterTex;
using Penumbra.GameData.Files.MaterialStructs;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using LunaDxImage = Luna.DirectX.Image;
using OtterTexImage = OtterTex.Image;

namespace Penumbra.Util;

public static class LunaDxExtensions
{
    public static unsafe LunaDxImage ToDirectXImage(this ScratchImage scratch, D3D11_USAGE usage = D3D11_USAGE.D3D11_USAGE_IMMUTABLE,
        D3D11_BIND_FLAG bind = D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE)
    {
        if (scratch.Meta.Dimension is not TexDimension.Tex2D || scratch.Meta.MipLevels is 0)
            throw new ArgumentException("The given ScratchImage is not suitable for an upload to GPU as a 2D texture.");

        var desc = new D3D11_TEXTURE2D_DESC
        {
            Width          = (uint)scratch.Meta.Width,
            Height         = (uint)scratch.Meta.Height,
            MipLevels      = (uint)scratch.Meta.MipLevels,
            ArraySize      = (uint)scratch.Meta.ArraySize,
            Format         = (DXGI_FORMAT)scratch.Meta.Format,
            SampleDesc     = new DXGI_SAMPLE_DESC(1, 0),
            Usage          = usage,
            BindFlags      = (uint)bind,
            CPUAccessFlags = 0,
            MiscFlags      = (uint)scratch.Meta.MiscFlags,
        };
        var contents = stackalloc D3D11_SUBRESOURCE_DATA[scratch.Images.Length];
        for (var i = 0; i < scratch.Images.Length; ++i)
            scratch.Images[i].ToSubresourceData(out contents[i]);

        using var texture = new ComPtr<ID3D11Texture2D>();
        Marshal.ThrowExceptionForHR(CustomRenderManager.Instance.Device->CreateTexture2D(&desc, contents, texture.GetAddressOf()));
        return new LunaDxImage(texture);
    }

    public static async Task<ScratchImage> GetScratchImageAsync(this ITextureReadbackProvider readbackProvider, IDalamudTextureWrap wrap,
        bool leaveWrapOpen = false, CancellationToken cancellationToken = default)
    {
        var (mipLevels, images) = await readbackProvider.GetAllRawImagesAsync(wrap, leaveWrapOpen, cancellationToken);
        var spec0   = images[0].Specification;
        var scratch = ScratchImage.Initialize2D((DXGIFormat)spec0.DxgiFormat, spec0.Width, spec0.Height, images.Length / mipLevels, mipLevels);
        for (var i = 0; i < scratch.Images.Length; ++i)
            images[i].RawData.CopyTo(scratch.Images[i].WritableSpan);
        return scratch;
    }

    public static Sampler CreateSampler(this SamplerFlags flags, bool bilinear = true)
        => new(D3D11_SAMPLER_DESC.DEFAULT with
        {
            Filter = bilinear ? D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_LINEAR : D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_POINT,
            AddressU = (D3D11_TEXTURE_ADDRESS_MODE)((uint)flags.UAddressMode + 1),
            AddressV = (D3D11_TEXTURE_ADDRESS_MODE)((uint)flags.VAddressMode + 1),
            MinLOD = flags.MinLod,
            MipLODBias = flags.LodBias,
        });

    extension(in OtterTexImage image)
    {
        private unsafe void ToSubresourceData(out D3D11_SUBRESOURCE_DATA subresData)
        {
            subresData.pSysMem          = (void*)image.Pixels;
            subresData.SysMemPitch      = (uint)image.RowPitch;
            subresData.SysMemSlicePitch = (uint)image.SlicePitch;
        }

        private unsafe Span<byte> WritableSpan
            => new((void*)image.Pixels, image.Span.Length);
    }
}
