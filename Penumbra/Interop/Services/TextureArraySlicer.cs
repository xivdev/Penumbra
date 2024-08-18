using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using OtterGui.Services;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

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
    public nint GetImGuiHandle(Texture* texture, byte sliceIndex)
    {
        if (texture == null)
            throw new ArgumentNullException(nameof(texture));
        if (sliceIndex >= texture->ArraySize)
            throw new ArgumentOutOfRangeException(nameof(sliceIndex), $"Slice index ({sliceIndex}) is greater than or equal to the texture array size ({texture->ArraySize})");
        if (_activeSlices.TryGetValue(((nint)texture, sliceIndex), out var state))
        {
            state.Refresh();
            return (nint)state.ShaderResourceView;
        }
        var srv = (ShaderResourceView)(nint)texture->D3D11ShaderResourceView;
        var description = srv.Description;
        switch (description.Dimension)
        {
            case ShaderResourceViewDimension.Texture1D:
            case ShaderResourceViewDimension.Texture2D:
            case ShaderResourceViewDimension.Texture2DMultisampled:
            case ShaderResourceViewDimension.Texture3D:
            case ShaderResourceViewDimension.TextureCube:
                // This function treats these as single-slice arrays.
                // As per the range check above, the only valid slice (i. e. 0) has been requested, therefore there is nothing to do.
                break;
            case ShaderResourceViewDimension.Texture1DArray:
                description.Texture1DArray.FirstArraySlice = sliceIndex;
                description.Texture2DArray.ArraySize = 1;
                break;
            case ShaderResourceViewDimension.Texture2DArray:
                description.Texture2DArray.FirstArraySlice = sliceIndex;
                description.Texture2DArray.ArraySize = 1;
                break;
            case ShaderResourceViewDimension.Texture2DMultisampledArray:
                description.Texture2DMSArray.FirstArraySlice = sliceIndex;
                description.Texture2DMSArray.ArraySize = 1;
                break;
            case ShaderResourceViewDimension.TextureCubeArray:
                description.TextureCubeArray.First2DArrayFace = sliceIndex * 6;
                description.TextureCubeArray.CubeCount = 1;
                break;
            default:
                throw new NotSupportedException($"{nameof(TextureArraySlicer)} does not support dimension {description.Dimension}");
        }
        state = new SliceState(new ShaderResourceView(srv.Device, srv.Resource, description));
        _activeSlices.Add(((nint)texture, sliceIndex), state);
        return (nint)state.ShaderResourceView;
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
            {
                _activeSlices.Remove(key);
            }
        }
        finally
        {
            _expiredKeys.Clear();
        }
    }

    public void Dispose()
    {
        foreach (var slice in _activeSlices.Values)
        {
            slice.Dispose();
        }
    }

    private sealed class SliceState(ShaderResourceView shaderResourceView) : IDisposable
    {
        public readonly ShaderResourceView ShaderResourceView = shaderResourceView;

        private uint _timeToLive = InitialTimeToLive;

        public void Refresh()
        {
            _timeToLive = InitialTimeToLive;
        }

        public bool Tick()
        {
            if (unchecked(_timeToLive--) > 0)
                return true;

            ShaderResourceView.Dispose();
            return false;
        }

        public void Dispose()
        {
            ShaderResourceView.Dispose();
        }
    }
}
