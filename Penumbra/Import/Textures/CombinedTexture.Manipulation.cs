using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using SixLabors.ImageSharp.PixelFormats;
using Dalamud.Interface;
using Penumbra.UI;

namespace Penumbra.Import.Textures;

public partial class CombinedTexture
{
    private Matrix4x4 _multiplierLeft  = Matrix4x4.Identity;
    private Vector4   _constantLeft    = Vector4.Zero;
    private Matrix4x4 _multiplierRight = Matrix4x4.Identity;
    private Vector4   _constantRight   = Vector4.Zero;
    private int       _offsetX         = 0;
    private int       _offsetY         = 0;
    private CombineOp _combineOp       = CombineOp.Over;
    private ResizeOp  _resizeOp        = ResizeOp.None;
    private Channels  _copyChannels    = Channels.Red | Channels.Green | Channels.Blue | Channels.Alpha;

    private RgbaPixelData _leftPixels  = RgbaPixelData.Empty;
    private RgbaPixelData _rightPixels = RgbaPixelData.Empty;

    private const float OneThird = 1.0f / 3.0f;
    private const float RWeight  = 0.2126f;
    private const float GWeight  = 0.7152f;
    private const float BWeight  = 0.0722f;

    // @formatter:off
    private static readonly IReadOnlyList<(string Label, Matrix4x4 Multiplier, Vector4 Constant)> PredefinedColorTransforms =
        new[]
        {
            ("No Transform (Identity)",       Matrix4x4.Identity,                                                                                                                                            Vector4.Zero  ),
            ("Grayscale (Average)",           new Matrix4x4(OneThird, OneThird, OneThird, 0.0f,     OneThird, OneThird, OneThird, 0.0f,     OneThird, OneThird, OneThird, 0.0f,     0.0f, 0.0f, 0.0f, 1.0f), Vector4.Zero  ),
            ("Grayscale (Weighted)",          new Matrix4x4(RWeight,  RWeight,  RWeight,  0.0f,     GWeight,  GWeight,  GWeight,  0.0f,     BWeight,  BWeight,  BWeight,  0.0f,     0.0f, 0.0f, 0.0f, 1.0f), Vector4.Zero  ),
            ("Grayscale (Average) to Alpha",  new Matrix4x4(OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, OneThird, 0.0f, 0.0f, 0.0f, 0.0f), Vector4.Zero  ),
            ("Grayscale (Weighted) to Alpha", new Matrix4x4(RWeight,  RWeight,  RWeight,  RWeight,  GWeight,  GWeight,  GWeight,  GWeight,  BWeight,  BWeight,  BWeight,  BWeight,  0.0f, 0.0f, 0.0f, 0.0f), Vector4.Zero  ),
            ("Make Opaque (Drop Alpha)",      new Matrix4x4(1.0f,     0.0f,     0.0f,     0.0f,     0.0f,     1.0f,     0.0f,     0.0f,     0.0f,     0.0f,     1.0f,     0.0f,     0.0f, 0.0f, 0.0f, 0.0f), Vector4.UnitW ),
            ("Extract Red",                   new Matrix4x4(1.0f,     1.0f,     1.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f, 0.0f, 0.0f, 0.0f), Vector4.UnitW ),
            ("Extract Green",                 new Matrix4x4(0.0f,     0.0f,     0.0f,     0.0f,     1.0f,     1.0f,     1.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f, 0.0f, 0.0f, 0.0f), Vector4.UnitW ),
            ("Extract Blue",                  new Matrix4x4(0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     1.0f,     1.0f,     1.0f,     0.0f,     0.0f, 0.0f, 0.0f, 0.0f), Vector4.UnitW ),
            ("Extract Alpha",                 new Matrix4x4(0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     1.0f, 1.0f, 1.0f, 0.0f), Vector4.UnitW ),
        };
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

    private static bool DragFloat(string label, float width, ref float value)
    {
        var tmp = value;
        ImGui.TableNextColumn();
        ImGui.SetNextItemWidth(width);
        if (ImGui.DragFloat(label, ref tmp, 0.001f, -1f, 1f))
            value = tmp;

        return ImGui.IsItemDeactivatedAfterEdit();
    }

    public void DrawMatrixInputLeft(float width)
    {
        var ret = DrawMatrixInput(ref _multiplierLeft, ref _constantLeft, width);
        ret |= DrawMatrixTools(ref _multiplierLeft, ref _constantLeft);
        if (ret)
            Update();
    }

    public void DrawMatrixInputRight(float width)
    {
        var ret = DrawMatrixInput(ref _multiplierRight, ref _constantRight, width);
        ret |= DrawMatrixTools(ref _multiplierRight, ref _constantRight);

        ImGui.SetNextItemWidth(75.0f * UiHelpers.Scale);
        ImGui.DragInt("##XOffset", ref _offsetX, 0.5f);
        ret |= ImGui.IsItemDeactivatedAfterEdit();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(75.0f * UiHelpers.Scale);
        ImGui.DragInt("Offsets##YOffset", ref _offsetY, 0.5f);
        ret |= ImGui.IsItemDeactivatedAfterEdit();

        ImGui.SetNextItemWidth(200.0f * UiHelpers.Scale);
        using (var c = ImRaii.Combo("Combine Operation", CombineOpLabels[(int)_combineOp]))
        {
            if (c)
                foreach (var op in Enum.GetValues<CombineOp>())
                {
                    if ((int)op < 0) // Negative codes are for internal use only.
                        continue;

                    if (ImGui.Selectable(CombineOpLabels[(int)op], op == _combineOp))
                    {
                        _combineOp = op;
                        ret        = true;
                    }

                    ImGuiUtil.SelectableHelpMarker(CombineOpTooltips[(int)op]);
                }
        }

        var resizeOp = GetActualResizeOp(_resizeOp, _combineOp);
        using (var dis = ImRaii.Disabled((int)resizeOp < 0))
        {
            ret |= ImGuiUtil.GenericEnumCombo("Resizing Mode", 200.0f * UiHelpers.Scale, _resizeOp, out _resizeOp,
                Enum.GetValues<ResizeOp>().Where(op => (int)op >= 0), op => ResizeOpLabels[(int)op]);
        }

        using (var dis = ImRaii.Disabled(_combineOp != CombineOp.CopyChannels))
        {
            ImGui.TextUnformatted("Copy");
            foreach (var channel in Enum.GetValues<Channels>())
            {
                ImGui.SameLine();
                var copy = (_copyChannels & channel) != 0;
                if (ImGui.Checkbox(channel.ToString(), ref copy))
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
        using var table = ImRaii.Table(string.Empty, 5, ImGuiTableFlags.BordersInner | ImGuiTableFlags.SizingFixedFit);
        if (!table)
            return false;

        var changes = false;

        ImGui.TableNextColumn();
        ImGui.TableNextColumn();
        ImGuiUtil.Center("R");
        ImGui.TableNextColumn();
        ImGuiUtil.Center("G");
        ImGui.TableNextColumn();
        ImGuiUtil.Center("B");
        ImGui.TableNextColumn();
        ImGuiUtil.Center("A");

        var inputWidth = width / 6;
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text("R    ");
        changes |= DragFloat("##RR", inputWidth, ref multiplier.M11);
        changes |= DragFloat("##RG", inputWidth, ref multiplier.M12);
        changes |= DragFloat("##RB", inputWidth, ref multiplier.M13);
        changes |= DragFloat("##RA", inputWidth, ref multiplier.M14);

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text("G    ");
        changes |= DragFloat("##GR", inputWidth, ref multiplier.M21);
        changes |= DragFloat("##GG", inputWidth, ref multiplier.M22);
        changes |= DragFloat("##GB", inputWidth, ref multiplier.M23);
        changes |= DragFloat("##GA", inputWidth, ref multiplier.M24);

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text("B    ");
        changes |= DragFloat("##BR", inputWidth, ref multiplier.M31);
        changes |= DragFloat("##BG", inputWidth, ref multiplier.M32);
        changes |= DragFloat("##BB", inputWidth, ref multiplier.M33);
        changes |= DragFloat("##BA", inputWidth, ref multiplier.M34);

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text("A    ");
        changes |= DragFloat("##AR", inputWidth, ref multiplier.M41);
        changes |= DragFloat("##AG", inputWidth, ref multiplier.M42);
        changes |= DragFloat("##AB", inputWidth, ref multiplier.M43);
        changes |= DragFloat("##AA", inputWidth, ref multiplier.M44);

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.Text("1    ");
        changes |= DragFloat("##1R", inputWidth, ref constant.X);
        changes |= DragFloat("##1G", inputWidth, ref constant.Y);
        changes |= DragFloat("##1B", inputWidth, ref constant.Z);
        changes |= DragFloat("##1A", inputWidth, ref constant.W);

        return changes;
    }

    private static bool DrawMatrixTools(ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        var changes = PresetCombo(ref multiplier, ref constant);
        ImGui.SameLine();
        ImGui.Dummy(ImGuiHelpers.ScaledVector2(20, 0));
        ImGui.SameLine();
        ImGui.TextUnformatted("Invert");
        ImGui.SameLine();

        Channels channels = 0;
        if (ImGui.Button("Colors"))
            channels |= Channels.Red | Channels.Green | Channels.Blue;
        ImGui.SameLine();
        if (ImGui.Button("R"))
            channels |= Channels.Red;

        ImGui.SameLine();
        if (ImGui.Button("G"))
            channels |= Channels.Green;

        ImGui.SameLine();
        if (ImGui.Button("B"))
            channels |= Channels.Blue;

        ImGui.SameLine();
        if (ImGui.Button("A"))
            channels |= Channels.Alpha;

        changes |= InvertChannels(channels, ref multiplier, ref constant);
        return changes;
    }

    private static bool PresetCombo(ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        using var combo = ImRaii.Combo("Presets", string.Empty, ImGuiComboFlags.NoPreview);
        if (!combo)
            return false;

        var ret = false;
        foreach (var (label, preMultiplier, preConstant) in PredefinedColorTransforms)
        {
            if (!ImGui.Selectable(label, multiplier == preMultiplier && constant == preConstant))
                continue;

            multiplier = preMultiplier;
            constant   = preConstant;
            ret        = true;
        }

        return ret;
    }
}
