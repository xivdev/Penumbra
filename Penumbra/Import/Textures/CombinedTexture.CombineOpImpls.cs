using System.Buffers;
using ImSharp;
using Rgba32 = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace Penumbra.Import.Textures;

public partial class CombinedTexture
{
    private Vector4 DataLeft(int offset)
        => CappedVector(_leftPixels.PixelData, offset, _multiplierLeft, _constantLeft);

    private Vector4 DataRight(int offset)
        => CappedVector(_rightPixels.PixelData, offset, _multiplierRight, _constantRight);

    private Vector4 DataRight(int x, int y)
    {
        x += _offsetX;
        y += _offsetY;
        if (x < 0 || x >= _rightPixels.Width || y < 0 || y >= _rightPixels.Height)
            return Vector4.Zero;

        var offset = (y * _rightPixels.Width + x) * 4;
        return CappedVector(_rightPixels.PixelData, offset, _multiplierRight, _constantRight);
    }

    private Span<Rgba32> CenterRow(int width, int y)
        => MemoryMarshal.Cast<byte, Rgba32>(_centerStorage.RgbaPixels.AsSpan(width * 4 * y, width * 4));

    private static Vector4 Over(Vector4 over, Vector4 under)
    {
        var alpha = over.W + under.W * (1 - over.W);
        return alpha == 0
            ? default
            : ((over * over.W + under * under.W * (1 - over.W)) / alpha) with { W = alpha };
    }

    /// <remarks> https://en.wikipedia.org/wiki/Blend_modes#Overlay </remarks>
    private static float BlendOverlay(float primary, float secondary)
        => primary < 0.5f
            ? 2.0f * primary * secondary
            : 1.0f - 2.0f * (1.0f - primary) * (1.0f - secondary);

    private void MultiplyPixelsLeft(int y, ParallelLoopState _)
    {
        var output = CenterRow(_leftPixels.Width, y);
        var offset = _leftPixels.Width * y * 4;
        for (var x = 0; x < _leftPixels.Width; ++x, offset += 4)
            output[x] = new Rgba32(DataLeft(offset));
    }

    private void MultiplyPixelsRight(int y, ParallelLoopState _)
    {
        var output = CenterRow(_rightPixels.Width, y);
        var offset = _rightPixels.Width * y * 4;
        for (var x = 0; x < _rightPixels.Width; ++x, offset += 4)
            output[x] = new Rgba32(DataRight(offset));
    }

    private abstract class CombineOpImpl(CombinedTexture owner)
    {
        /// <remarks> When applicable, should honor both alpha values. For example using https://drafts.csswg.org/compositing/#generalformula </remarks>
        protected abstract void CombinePixels(Span<Rgba32> outputSpan, ReadOnlySpan<Vector4> leftSpan, ReadOnlySpan<Vector4> rightSpan);

        public void ProcessRow(int y, ParallelLoopState _)
        {
            var       width      = owner._leftPixels.Width;
            using var lease      = ArrayPool<byte>.Shared.RentLease<Vector4>(width * 2);
            var       bufferSpan = lease.Span;
            var       leftSpan   = bufferSpan[..width];
            var       rightSpan  = bufferSpan[width..];
            var       outputSpan = owner.CenterRow(width, y);
            var       offset     = width * y * 4;
            for (var x = 0; x < width; ++x, offset += 4)
            {
                leftSpan[x]  = owner.DataLeft(offset);
                rightSpan[x] = owner.DataRight(x, y);
            }

            CombinePixels(outputSpan, leftSpan, rightSpan);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Action<int, ParallelLoopState>(CombineOpImpl op)
            => op.ProcessRow;
    }

    private sealed class OverImpl(CombinedTexture owner, bool rightOver) : CombineOpImpl(owner)
    {
        protected override void CombinePixels(Span<Rgba32> outputSpan, ReadOnlySpan<Vector4> leftSpan, ReadOnlySpan<Vector4> rightSpan)
        {
            var overSpan  = rightOver ? rightSpan : leftSpan;
            var underSpan = rightOver ? leftSpan : rightSpan;
            for (var x = 0; x < outputSpan.Length; ++x)
                outputSpan[x] = new Rgba32(Over(overSpan[x], underSpan[x]));
        }
    }

    private sealed class CopyChannelsImpl(CombinedTexture owner, Channels channels) : CombineOpImpl(owner)
    {
        protected override void CombinePixels(Span<Rgba32> outputSpan, ReadOnlySpan<Vector4> leftSpan, ReadOnlySpan<Vector4> rightSpan)
        {
            for (var x = 0; x < outputSpan.Length; ++x)
            {
                var left  = leftSpan[x];
                var right = rightSpan[x];
                outputSpan[x] = new Rgba32(
                    (channels & Channels.Red) != 0 ? right.X : left.X,
                    (channels & Channels.Green) != 0 ? right.Y : left.Y,
                    (channels & Channels.Blue) != 0 ? right.Z : left.Z,
                    (channels & Channels.Alpha) != 0 ? right.W : left.W);
            }
        }
    }

    private sealed class BlendMultiplyImpl(CombinedTexture owner, bool resultOver) : CombineOpImpl(owner)
    {
        protected override void CombinePixels(Span<Rgba32> outputSpan, ReadOnlySpan<Vector4> leftSpan, ReadOnlySpan<Vector4> rightSpan)
        {
            for (var x = 0; x < outputSpan.Length; ++x)
            {
                var left   = leftSpan[x];
                var right  = rightSpan[x];
                var result = Vector4.Lerp(right, left * right, left.W) with { W = right.W };
                outputSpan[x] = new Rgba32(resultOver ? Over(result, left) : Over(left, result));
            }
        }
    }

    private sealed class BlendMultiplyRgbaImpl(CombinedTexture owner) : CombineOpImpl(owner)
    {
        protected override void CombinePixels(Span<Rgba32> outputSpan, ReadOnlySpan<Vector4> leftSpan, ReadOnlySpan<Vector4> rightSpan)
        {
            for (var x = 0; x < outputSpan.Length; ++x)
                outputSpan[x] = new Rgba32(leftSpan[x] * rightSpan[x]);
        }
    }

    private sealed class BlendScreenImpl(CombinedTexture owner, bool resultOver) : CombineOpImpl(owner)
    {
        protected override void CombinePixels(Span<Rgba32> outputSpan, ReadOnlySpan<Vector4> leftSpan, ReadOnlySpan<Vector4> rightSpan)
        {
            for (var x = 0; x < outputSpan.Length; ++x)
            {
                var left   = leftSpan[x];
                var right  = rightSpan[x];
                var result = Vector4.Lerp(right, left + right - left * right, left.W) with { W = right.W };
                outputSpan[x] = new Rgba32(resultOver ? Over(result, left) : Over(left, result));
            }
        }
    }

    private sealed class BlendScreenRgbaImpl(CombinedTexture owner) : CombineOpImpl(owner)
    {
        protected override void CombinePixels(Span<Rgba32> outputSpan, ReadOnlySpan<Vector4> leftSpan, ReadOnlySpan<Vector4> rightSpan)
        {
            for (var x = 0; x < outputSpan.Length; ++x)
            {
                var left  = leftSpan[x];
                var right = rightSpan[x];
                outputSpan[x] = new Rgba32(left + right - left * right);
            }
        }
    }

    private sealed class BlendOverlayImpl(CombinedTexture owner, bool hardLight, bool resultOver) : CombineOpImpl(owner)
    {
        protected override void CombinePixels(Span<Rgba32> outputSpan, ReadOnlySpan<Vector4> leftSpan, ReadOnlySpan<Vector4> rightSpan)
        {
            var primarySpan   = hardLight ? rightSpan : leftSpan;
            var secondarySpan = hardLight ? leftSpan : rightSpan;
            for (var x = 0; x < outputSpan.Length; ++x)
            {
                var left      = leftSpan[x];
                var right     = rightSpan[x];
                var primary   = primarySpan[x];
                var secondary = secondarySpan[x];
                var blend = new Vector3(
                    BlendOverlay(primary.X, secondary.X),
                    BlendOverlay(primary.Y, secondary.Y),
                    BlendOverlay(primary.Z, secondary.Z));
                var result = new Vector4(Vector3.Lerp(right.AsVector3(), blend, left.W), right.W);
                outputSpan[x] = new Rgba32(resultOver ? Over(result, left) : Over(left, result));
            }
        }
    }

    private sealed class BlendOverlayRgbaImpl(CombinedTexture owner, bool hardLight) : CombineOpImpl(owner)
    {
        protected override void CombinePixels(Span<Rgba32> outputSpan, ReadOnlySpan<Vector4> leftSpan, ReadOnlySpan<Vector4> rightSpan)
        {
            var primarySpan   = hardLight ? rightSpan : leftSpan;
            var secondarySpan = hardLight ? leftSpan : rightSpan;
            for (var x = 0; x < outputSpan.Length; ++x)
            {
                var primary   = primarySpan[x];
                var secondary = secondarySpan[x];
                outputSpan[x] = new Rgba32(
                    BlendOverlay(primary.X, secondary.X),
                    BlendOverlay(primary.Y, secondary.Y),
                    BlendOverlay(primary.Z, secondary.Z),
                    BlendOverlay(primary.W, secondary.W));
            }
        }
    }
}
