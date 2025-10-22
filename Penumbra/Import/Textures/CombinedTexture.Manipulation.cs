using ImSharp;
using OtterGui.Text;
using Rgba32 = SixLabors.ImageSharp.PixelFormats.Rgba32;

namespace Penumbra.Import.Textures;

public partial class CombinedTexture
{
    private Matrix4x4 _multiplierLeft  = Matrix4x4.Identity;
    private Vector4   _constantLeft    = Vector4.Zero;
    private Matrix4x4 _multiplierRight = Matrix4x4.Identity;
    private Vector4   _constantRight   = Vector4.Zero;
    private int       _offsetX;
    private int       _offsetY;
    private CombineOp _combineOp    = CombineOp.Over;
    private ResizeOp  _resizeOp     = ResizeOp.None;
    private Channels  _copyChannels = Channels.Red | Channels.Green | Channels.Blue | Channels.Alpha;

    private RgbaPixelData _leftPixels  = RgbaPixelData.Empty;
    private RgbaPixelData _rightPixels = RgbaPixelData.Empty;

    private const float OneThird = 1.0f / 3.0f;
    private const float RWeight  = 0.2126f;
    private const float GWeight  = 0.7152f;
    private const float BWeight  = 0.0722f;

    // @formatter:off
    private static readonly IReadOnlyList<(StringU8 Label, Matrix4x4 Multiplier, Vector4 Constant)> PredefinedColorTransforms =
        [
            (new StringU8("No Transform (Identity)"u8),       Matrix4x4.Identity,                                                                                                                                            Vector4.Zero  ),
            (new StringU8("Grayscale (Average)"u8),           new Matrix4x4(OneThird, OneThird, OneThird, 0.0f,     OneThird, OneThird, OneThird, 0.0f,     OneThird, OneThird, OneThird, 0.0f,     0.0f, 0.0f, 0.0f, 1.0f), Vector4.Zero  ),
            (new StringU8("Grayscale (Weighted)"u8),          new Matrix4x4(RWeight,  RWeight,  RWeight,  0.0f,     GWeight,  GWeight,  GWeight,  0.0f,     BWeight,  BWeight,  BWeight,  0.0f,     0.0f, 0.0f, 0.0f, 1.0f), Vector4.Zero  ),
            (new StringU8("Grayscale (Average) to Alpha"u8),  new Matrix4x4(OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, 0.0f, 0.0f, 0.0f, 0.0f), Vector4.Zero  ),
            (new StringU8("Grayscale (Weighted) to Alpha"u8), new Matrix4x4(RWeight,  RWeight,  RWeight,  RWeight,  GWeight,  GWeight,  GWeight,  GWeight,  BWeight,  BWeight,  BWeight,  BWeight,  0.0f, 0.0f, 0.0f, 0.0f), Vector4.Zero  ),
            (new StringU8("Make Opaque (Drop Alpha)"u8),      new Matrix4x4(1.0f,     0.0f,     0.0f,     0.0f,     0.0f,     1.0f,     0.0f,     0.0f,     0.0f,     0.0f,     1.0f,     0.0f,     0.0f, 0.0f, 0.0f, 0.0f), Vector4.UnitW ),
            (new StringU8("Extract Red"u8),                   new Matrix4x4(1.0f,     1.0f,     1.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f, 0.0f, 0.0f, 0.0f), Vector4.UnitW ),
            (new StringU8("Extract Green"u8),                 new Matrix4x4(0.0f,     0.0f,     0.0f,     0.0f,     1.0f,     1.0f,     1.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f, 0.0f, 0.0f, 0.0f), Vector4.UnitW ),
            (new StringU8("Extract Blue"u8),                  new Matrix4x4(0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     1.0f,     1.0f,     1.0f,     0.0f,     0.0f, 0.0f, 0.0f, 0.0f), Vector4.UnitW ),
            (new StringU8("Extract Alpha"u8),                 new Matrix4x4(0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     1.0f, 1.0f, 1.0f, 0.0f), Vector4.UnitW ),
        ];
    // @formatter:on

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

    private void AddPixelsMultiplied(int y, ParallelLoopState _)
    {
        for (var x = 0; x < _leftPixels.Width; ++x)
        {
            var offset = (_leftPixels.Width * y + x) * 4;
            var left   = DataLeft(offset);
            var right  = DataRight(x, y);
            var alpha  = right.W + left.W * (1 - right.W);
            var rgba = alpha == 0
                ? new Rgba32()
                : new Rgba32(((right * right.W + left * left.W * (1 - right.W)) / alpha) with { W = alpha });
            _centerStorage.RgbaPixels[offset]     = rgba.R;
            _centerStorage.RgbaPixels[offset + 1] = rgba.G;
            _centerStorage.RgbaPixels[offset + 2] = rgba.B;
            _centerStorage.RgbaPixels[offset + 3] = rgba.A;
        }
    }

    private void ReverseAddPixelsMultiplied(int y, ParallelLoopState _)
    {
        for (var x = 0; x < _leftPixels.Width; ++x)
        {
            var offset = (_leftPixels.Width * y + x) * 4;
            var left   = DataLeft(offset);
            var right  = DataRight(x, y);
            var alpha  = left.W + right.W * (1 - left.W);
            var rgba = alpha == 0
                ? new Rgba32()
                : new Rgba32(((left * left.W + right * right.W * (1 - left.W)) / alpha) with { W = alpha });
            _centerStorage.RgbaPixels[offset]     = rgba.R;
            _centerStorage.RgbaPixels[offset + 1] = rgba.G;
            _centerStorage.RgbaPixels[offset + 2] = rgba.B;
            _centerStorage.RgbaPixels[offset + 3] = rgba.A;
        }
    }

    private void ChannelMergePixelsMultiplied(int y, ParallelLoopState _)
    {
        var channels = _copyChannels;
        for (var x = 0; x < _leftPixels.Width; ++x)
        {
            var offset = (_leftPixels.Width * y + x) * 4;
            var left   = DataLeft(offset);
            var right  = DataRight(x, y);
            var rgba = new Rgba32((channels & Channels.Red) != 0 ? right.X : left.X,
                (channels & Channels.Green) != 0 ? right.Y : left.Y,
                (channels & Channels.Blue) != 0 ? right.Z : left.Z,
                (channels & Channels.Alpha) != 0 ? right.W : left.W);
            _centerStorage.RgbaPixels[offset]     = rgba.R;
            _centerStorage.RgbaPixels[offset + 1] = rgba.G;
            _centerStorage.RgbaPixels[offset + 2] = rgba.B;
            _centerStorage.RgbaPixels[offset + 3] = rgba.A;
        }
    }

    private void MultiplyPixelsLeft(int y, ParallelLoopState _)
    {
        for (var x = 0; x < _leftPixels.Width; ++x)
        {
            var offset = (_leftPixels.Width * y + x) * 4;
            var left   = DataLeft(offset);
            var rgba   = new Rgba32(left);
            _centerStorage.RgbaPixels[offset]     = rgba.R;
            _centerStorage.RgbaPixels[offset + 1] = rgba.G;
            _centerStorage.RgbaPixels[offset + 2] = rgba.B;
            _centerStorage.RgbaPixels[offset + 3] = rgba.A;
        }
    }

    private void MultiplyPixelsRight(int y, ParallelLoopState _)
    {
        for (var x = 0; x < _rightPixels.Width; ++x)
        {
            var offset = (_rightPixels.Width * y + x) * 4;
            var right  = DataRight(offset);
            var rgba   = new Rgba32(right);
            _centerStorage.RgbaPixels[offset]     = rgba.R;
            _centerStorage.RgbaPixels[offset + 1] = rgba.G;
            _centerStorage.RgbaPixels[offset + 2] = rgba.B;
            _centerStorage.RgbaPixels[offset + 3] = rgba.A;
        }
    }

    private (int Width, int Height) CombineImage()
    {
        var combineOp = GetActualCombineOp();
        var resizeOp  = GetActualResizeOp(_resizeOp, combineOp);

        var left  = resizeOp != ResizeOp.RightOnly ? RgbaPixelData.FromTexture(_left) : RgbaPixelData.Empty;
        var right = resizeOp != ResizeOp.LeftOnly ? RgbaPixelData.FromTexture(_right) : RgbaPixelData.Empty;

        var targetSize = resizeOp switch
        {
            ResizeOp.RightOnly => right.Size,
            ResizeOp.ToRight   => right.Size,
            _                  => left.Size,
        };

        try
        {
            _centerStorage.RgbaPixels = RgbaPixelData.NewPixelData(targetSize);
            _centerStorage.Type       = TextureType.Bitmap;

            _leftPixels = resizeOp switch
            {
                ResizeOp.RightOnly => RgbaPixelData.Empty,
                _                  => left.Resize(targetSize),
            };
            _rightPixels = resizeOp switch
            {
                ResizeOp.LeftOnly => RgbaPixelData.Empty,
                ResizeOp.None     => right,
                _                 => right.Resize(targetSize),
            };

            Parallel.For(0, targetSize.Height, combineOp switch
            {
                CombineOp.Over          => AddPixelsMultiplied,
                CombineOp.Under         => ReverseAddPixelsMultiplied,
                CombineOp.LeftMultiply  => MultiplyPixelsLeft,
                CombineOp.RightMultiply => MultiplyPixelsRight,
                CombineOp.CopyChannels  => ChannelMergePixelsMultiplied,
                _                       => throw new InvalidOperationException($"Cannot combine images with operation {combineOp}"),
            });
        }
        finally
        {
            _leftPixels  = RgbaPixelData.Empty;
            _rightPixels = RgbaPixelData.Empty;
        }

        return targetSize;
    }

    private static Vector4 CappedVector(IReadOnlyList<byte> bytes, int offset, Matrix4x4 transform, Vector4 constant)
    {
        if (bytes.Count == 0)
            return Vector4.Zero;

        var rgba        = new Rgba32(bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]);
        var transformed = Vector4.Transform(rgba.ToVector4(), transform) + constant;

        transformed.X = Math.Clamp(transformed.X, 0, 1);
        transformed.Y = Math.Clamp(transformed.Y, 0, 1);
        transformed.Z = Math.Clamp(transformed.Z, 0, 1);
        transformed.W = Math.Clamp(transformed.W, 0, 1);
        return transformed;
    }

    private static bool DragFloat(Utf8StringHandler<LabelStringHandlerBuffer> label, float width, ref float value)
    {
        var tmp = value;
        Im.Table.NextColumn();
        Im.Item.SetNextWidth(width);
        if (Im.Drag(label, ref tmp, speed: 0.001f, min: -1f, max: 1f))
            value = tmp;

        return Im.Item.DeactivatedAfterEdit;
    }

    public void DrawMatrixInputLeft(float width)
    {
        var ret = DrawMatrixInput(ref _multiplierLeft, ref _constantLeft, width);
        ret |= DrawMatrixTools(ref _multiplierLeft, ref _constantLeft);
        if (ret)
            Update();
    }

    private sealed class CombineOperationCombo() : SimpleFilterCombo<CombineOp>(SimpleFilterType.None)
    {
        private static readonly CombineOp[] UserValues = Enum.GetValues<CombineOp>().Where(c => (int)c >= 0).ToArray();

        public override StringU8 DisplayString(in CombineOp value)
            => new(value.ToLabelU8());

        public override string FilterString(in CombineOp value)
            => value.ToLabel();

        public override IEnumerable<CombineOp> GetBaseItems()
            => UserValues;

        public override StringU8 Tooltip(in CombineOp value)
            => new(value.Tooltip());
    }

    private sealed class ResizeOperationCombo() : SimpleFilterCombo<ResizeOp>(SimpleFilterType.None)
    {
        private static readonly ResizeOp[] UserValues = Enum.GetValues<ResizeOp>().Where(c => (int)c >= 0).ToArray();

        public override StringU8 DisplayString(in ResizeOp value)
            => new(value.ToLabelU8());

        public override string FilterString(in ResizeOp value)
            => value.ToLabel();

        public override IEnumerable<ResizeOp> GetBaseItems()
            => UserValues;
    }

    private readonly CombineOperationCombo _combineCombo = new();
    private readonly ResizeOperationCombo  _resizeCombo  = new();

    public void DrawMatrixInputRight(float width)
    {
        var ret = DrawMatrixInput(ref _multiplierRight, ref _constantRight, width);
        ret |= DrawMatrixTools(ref _multiplierRight, ref _constantRight);

        Im.Item.SetNextWidthScaled(75);
        Im.Drag("##XOffset"u8, ref _offsetX, speed: 0.5f);
        ret |= Im.Item.DeactivatedAfterEdit;
        Im.Line.Same();
        Im.Item.SetNextWidthScaled(75);
        Im.Drag("Offsets##YOffset"u8, ref _offsetY, speed: 0.5f);
        ret |= Im.Item.DeactivatedAfterEdit;

        Im.Item.SetNextWidthScaled(200);
        ret |= _combineCombo.Draw("Combine Operation"u8, ref _combineOp, StringU8.Empty, 200 * Im.Style.GlobalScale);
        var resizeOp = GetActualResizeOp(_resizeOp, _combineOp);
        using (Im.Disabled((int)resizeOp < 0))
        {
            ret |= _resizeCombo.Draw("Resizing Mode"u8, ref _resizeOp, StringU8.Empty, 200 * Im.Style.GlobalScale);
        }

        using (Im.Disabled(_combineOp != CombineOp.CopyChannels))
        {
            Im.Text("Copy"u8);
            foreach (var channel in Enum.GetValues<Channels>())
            {
                Im.Line.Same();
                var copy = (_copyChannels & channel) != 0;
                if (Im.Checkbox(channel.ToString(), ref copy))
                {
                    _copyChannels = copy ? _copyChannels | channel : _copyChannels & ~channel;
                    ret           = true;
                }
            }
        }

        if (ret)
            Update();
    }

    private static bool DrawMatrixInput(ref Matrix4x4 multiplier, ref Vector4 constant, float width)
    {
        using var table = Im.Table.Begin(StringU8.Empty, 5, TableFlags.BordersInner | TableFlags.SizingFixedFit);
        if (!table)
            return false;

        var changes = false;

        table.NextColumn();
        table.NextColumn();
        ImEx.TextCentered("R"u8);
        table.NextColumn();
        ImEx.TextCentered("G"u8);
        table.NextColumn();
        ImEx.TextCentered("B"u8);
        table.NextColumn();
        ImEx.TextCentered("A"u8);

        var inputWidth = width / 6;
        table.DrawFrameColumn("R    "u8);
        changes |= DragFloat("##RR"u8, inputWidth, ref multiplier.M11);
        changes |= DragFloat("##RG"u8, inputWidth, ref multiplier.M12);
        changes |= DragFloat("##RB"u8, inputWidth, ref multiplier.M13);
        changes |= DragFloat("##RA"u8, inputWidth, ref multiplier.M14);

        table.DrawFrameColumn("G    "u8);
        changes |= DragFloat("##GR"u8, inputWidth, ref multiplier.M21);
        changes |= DragFloat("##GG"u8, inputWidth, ref multiplier.M22);
        changes |= DragFloat("##GB"u8, inputWidth, ref multiplier.M23);
        changes |= DragFloat("##GA"u8, inputWidth, ref multiplier.M24);

        table.DrawFrameColumn("B    "u8);
        changes |= DragFloat("##BR"u8, inputWidth, ref multiplier.M31);
        changes |= DragFloat("##BG"u8, inputWidth, ref multiplier.M32);
        changes |= DragFloat("##BB"u8, inputWidth, ref multiplier.M33);
        changes |= DragFloat("##BA"u8, inputWidth, ref multiplier.M34);

        table.DrawFrameColumn("A    "u8);
        changes |= DragFloat("##AR"u8, inputWidth, ref multiplier.M41);
        changes |= DragFloat("##AG"u8, inputWidth, ref multiplier.M42);
        changes |= DragFloat("##AB"u8, inputWidth, ref multiplier.M43);
        changes |= DragFloat("##AA"u8, inputWidth, ref multiplier.M44);

        table.DrawFrameColumn("1    "u8);
        changes |= DragFloat("##1R"u8, inputWidth, ref constant.X);
        changes |= DragFloat("##1G"u8, inputWidth, ref constant.Y);
        changes |= DragFloat("##1B"u8, inputWidth, ref constant.Z);
        changes |= DragFloat("##1A"u8, inputWidth, ref constant.W);

        return changes;
    }

    private static bool DrawMatrixTools(ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        var changes = PresetCombo(ref multiplier, ref constant);
        Im.Line.Same();
        Im.ScaledDummy(20);
        Im.Line.Same();
        Im.Text("Invert"u8);
        Im.Line.Same();

        Channels channels = 0;
        if (Im.Button("Colors"u8))
            channels |= Channels.Red | Channels.Green | Channels.Blue;
        Im.Line.Same();
        if (Im.Button("R"u8))
            channels |= Channels.Red;

        Im.Line.Same();
        if (Im.Button("G"u8))
            channels |= Channels.Green;

        Im.Line.Same();
        if (Im.Button("B"u8))
            channels |= Channels.Blue;

        Im.Line.Same();
        if (Im.Button("A"u8))
            channels |= Channels.Alpha;

        changes |= InvertChannels(channels, ref multiplier, ref constant);
        return changes;
    }

    private static bool PresetCombo(ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        using var combo = Im.Combo.Begin("Presets"u8, StringU8.Empty, ComboFlags.NoPreview);
        if (!combo)
            return false;

        var ret = false;
        foreach (var (label, preMultiplier, preConstant) in PredefinedColorTransforms)
        {
            if (!Im.Selectable(label, multiplier == preMultiplier && constant == preConstant))
                continue;

            multiplier = preMultiplier;
            constant   = preConstant;
            ret        = true;
        }

        return ret;
    }
}
