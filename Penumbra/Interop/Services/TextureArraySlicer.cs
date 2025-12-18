using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using OtterGui.Services;
using TerraFX.Interop.DirectX;

namespace Penumbra.Interop.Services;

/// <summary>
/// Creates ImGui handles over slices of array textures, and manages their lifetime.
/// </summary>
public sealed unsafe class TextureArraySlicer : IUiService, IDisposable
{
    private const uint InitialTimeToLive = 2;

    private readonly Dictionary<(nint XivTexture, byte SliceIndex), SliceState> _activeSlices = [];
    private readonly HashSet<(nint XivTexture, byte SliceIndex)>                _expiredKeys  = [];

    /// <remarks> Caching this across frames will cause a crash to desktop. </remarks>
    public ImTextureID GetImGuiHandle(Texture* texture, byte sliceIndex)
    {
        if (texture == null)
            throw new ArgumentNullException(nameof(texture));
        if (sliceIndex >= texture->ArraySize)
            throw new ArgumentOutOfRangeException(nameof(sliceIndex),
                $"Slice index ({sliceIndex}) is greater than or equal to the texture array size ({texture->ArraySize})");

        if (_activeSlices.TryGetValue(((nint)texture, sliceIndex), out var state))
        {
            state.Refresh();
            return new ImTextureID((nint)state.ShaderResourceView);
        }

        ref var srv = ref *(ID3D11ShaderResourceView*)(nint)texture->D3D11ShaderResourceView;
        srv.AddRef();
        try
        {
            D3D11_SHADER_RESOURCE_VIEW_DESC description;
            srv.GetDesc(&description);
            switch (description.ViewDimension)
            {
                case D3D_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURE1D:
                case D3D_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURE2D:
                case D3D_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURE2DMS:
                case D3D_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURE3D:
                case D3D_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURECUBE:
                    // This function treats these as single-slice arrays.
                    // As per the range check above, the only valid slice (i. e. 0) has been requested, therefore there is nothing to do.
                    break;
                case D3D_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURE1DARRAY:
                    description.Texture1DArray.FirstArraySlice = sliceIndex;
                    description.Texture1DArray.ArraySize       = 1;
                    break;
                case D3D_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURE2DARRAY:
                    description.Texture2DArray.FirstArraySlice = sliceIndex;
                    description.Texture2DArray.ArraySize       = 1;
                    break;
                case D3D_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURE2DMSARRAY:
                    description.Texture2DMSArray.FirstArraySlice = sliceIndex;
                    description.Texture2DMSArray.ArraySize       = 1;
                    break;
                case D3D_SRV_DIMENSION.D3D11_SRV_DIMENSION_TEXTURECUBEARRAY:
                    description.TextureCubeArray.First2DArrayFace = sliceIndex * 6u;
                    description.TextureCubeArray.NumCubes         = 1;
                    break;
                default:
                    throw new NotSupportedException($"{nameof(TextureArraySlicer)} does not support dimension {description.ViewDimension}");
            }

            ID3D11Device* device = null;
            srv.GetDevice(&device);
            ID3D11Resource* resource = null;
            srv.GetResource(&resource);
            try
            {
                ID3D11ShaderResourceView* slicedSrv = null;
                Marshal.ThrowExceptionForHR(device->CreateShaderResourceView(resource, &description, &slicedSrv));
                resource->Release();
                device->Release();

                state = new SliceState(slicedSrv);
                _activeSlices.Add(((nint)texture, sliceIndex), state);
                return new ImTextureID((nint)state.ShaderResourceView);
            }
            finally
            {
                if (resource is not null)
                    resource->Release();
                if (device is not null)
                    device->Release();
            }
        }
        finally
        {
            srv.Release();
        }
    }

    public void Tick()
    {
        try
        {
            foreach (var (key, slice) in _activeSlices)
            {
                if (!slice.Tick())
                    _expiredKeys.Add(key);
            }

            foreach (var key in _expiredKeys)
                _activeSlices.Remove(key);
        }
        finally
        {
            _expiredKeys.Clear();
        }
    }

    public void Dispose()
    {
        foreach (var slice in _activeSlices.Values)
            slice.Dispose();
    }

    private sealed class SliceState(ID3D11ShaderResourceView* shaderResourceView) : IDisposable
    {
        public readonly ID3D11ShaderResourceView* ShaderResourceView = shaderResourceView;

        private uint _timeToLive = InitialTimeToLive;

        public void Refresh()
        {
            _timeToLive = InitialTimeToLive;
        }

        public bool Tick()
        {
            if (unchecked(_timeToLive--) > 0)
                return true;

            if (ShaderResourceView is not null)
                ShaderResourceView->Release();
            return false;
        }

        public void Dispose()
        {
            if (ShaderResourceView is not null)
                ShaderResourceView->Release();
        }
    }
}
