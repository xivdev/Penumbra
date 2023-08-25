using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using SixLabors.ImageSharp.PixelFormats;
using Dalamud.Interface;
using Penumbra.UI;

namespace Penumbra.Import.Textures;

public partial class CombinedTexture
{
    private enum CombineOp
    {
        LeftCopy      = -3,
        RightCopy     = -2,
        Invalid       = -1,
        Over          = 0,
        Under         = 1,
        LeftMultiply  = 2,
        RightMultiply = 3,
        CopyChannels  = 4,
    }

    [Flags]
    private enum Channels
    {
        Red   = 1,
        Green = 2,
        Blue  = 4,
        Alpha = 8,
    }

    private Matrix4x4 _multiplierLeft  = Matrix4x4.Identity;
    private Vector4   _constantLeft    = Vector4.Zero;
    private Matrix4x4 _multiplierRight = Matrix4x4.Identity;
    private Vector4   _constantRight   = Vector4.Zero;
    private int       _offsetX         = 0;
    private int       _offsetY         = 0;
    private CombineOp _combineOp       = CombineOp.Over;
    private Channels  _copyChannels    = Channels.Red | Channels.Green | Channels.Blue | Channels.Alpha;

    private static readonly IReadOnlyList<string> CombineOpLabels = new string[]
    {
        "Overlay over Input",
        "Input over Overlay",
        "Ignore Overlay",
        "Replace Input",
        "Copy Channels",
    };

    private static readonly IReadOnlyList<string> CombineOpTooltips = new string[]
    {
        "Standard composition.\nApply the overlay over the input.",
        "Standard composition, reversed.\nApply the input over the overlay.",
        "Use only the input, and ignore the overlay.",
        "Completely replace the input with the overlay.",
        "Replace some input channels with those from the overlay.\nUseful for Multi maps.",
    };

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
            ("Extract Red",                   new Matrix4x4(1.0f,     1.0f,     1.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f, 0.0f, 0.0f, 0.0f), Vector4.UnitW ),
            ("Extract Green",                 new Matrix4x4(0.0f,     0.0f,     0.0f,     0.0f,     1.0f,     1.0f,     1.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f, 0.0f, 0.0f, 0.0f), Vector4.UnitW ),
            ("Extract Blue",                  new Matrix4x4(0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     1.0f,     1.0f,     1.0f,     0.0f,     0.0f, 0.0f, 0.0f, 0.0f), Vector4.UnitW ),
            ("Extract Alpha",                 new Matrix4x4(0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     0.0f,     1.0f, 1.0f, 1.0f, 0.0f), Vector4.UnitW ),
        };
    // @formatter:on

    private CombineOp GetActualCombineOp()
    {
        var combineOp = (_left.IsLoaded, _right.IsLoaded) switch
        {
            (true, true)   => _combineOp,
            (true, false)  => CombineOp.LeftMultiply,
            (false, true)  => CombineOp.RightMultiply,
            (false, false) => CombineOp.Invalid,
        };

        if (combineOp == CombineOp.CopyChannels)
        {
            if (_copyChannels == 0)
                combineOp = CombineOp.LeftMultiply;
            else if (_copyChannels == (Channels.Red | Channels.Green | Channels.Blue | Channels.Alpha))
                combineOp = CombineOp.RightMultiply;
        }

        return combineOp switch
        {
            CombineOp.LeftMultiply when _multiplierLeft.IsIdentity && _constantLeft == Vector4.Zero    => CombineOp.LeftCopy,
            CombineOp.RightMultiply when _multiplierRight.IsIdentity && _constantRight == Vector4.Zero => CombineOp.RightCopy,
            _                                                                                          => combineOp,
        };
    }

    private Vector4 DataLeft(int offset)
        => CappedVector(_left.RgbaPixels, offset, _multiplierLeft, _constantLeft);

    private Vector4 DataRight(int offset)
        => CappedVector(_right.RgbaPixels, offset, _multiplierRight, _constantRight);

    private Vector4 DataRight(int x, int y)
    {
        x += _offsetX;
        y += _offsetY;
        if (x < 0 || x >= _right.TextureWrap!.Width || y < 0 || y >= _right.TextureWrap!.Height)
            return Vector4.Zero;

        var offset = (y * _right.TextureWrap!.Width + x) * 4;
        return CappedVector(_right.RgbaPixels, offset, _multiplierRight, _constantRight);
    }

    private void AddPixelsMultiplied(int y, ParallelLoopState _)
    {
        for (var x = 0; x < _left.TextureWrap!.Width; ++x)
        {
            var offset = (_left.TextureWrap!.Width * y + x) * 4;
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
        for (var x = 0; x < _left.TextureWrap!.Width; ++x)
        {
            var offset = (_left.TextureWrap!.Width * y + x) * 4;
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
        for (var x = 0; x < _left.TextureWrap!.Width; ++x)
        {
            var offset = (_left.TextureWrap!.Width * y + x) * 4;
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
        for (var x = 0; x < _left.TextureWrap!.Width; ++x)
        {
            var offset = (_left.TextureWrap!.Width * y + x) * 4;
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
        for (var x = 0; x < _right.TextureWrap!.Width; ++x)
        {
            var offset = (_right.TextureWrap!.Width * y + x) * 4;
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
        var (width, height) = combineOp is not CombineOp.Invalid or CombineOp.RightCopy or CombineOp.RightMultiply
            ? (_left.TextureWrap!.Width, _left.TextureWrap!.Height)
            : (_right.TextureWrap!.Width, _right.TextureWrap!.Height);
        _centerStorage.RgbaPixels = new byte[width * height * 4];
        _centerStorage.Type       = TextureType.Bitmap;
        Parallel.For(0, height, combineOp switch
        {
            CombineOp.Over          => AddPixelsMultiplied,
            CombineOp.Under         => ReverseAddPixelsMultiplied,
            CombineOp.LeftMultiply  => MultiplyPixelsLeft,
            CombineOp.RightMultiply => MultiplyPixelsRight,
            CombineOp.CopyChannels  => ChannelMergePixelsMultiplied,
            _                       => throw new InvalidOperationException($"Cannot combine images with operation {combineOp}"),
        });

        return (width, height);
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
        var changes = false;

        using (var combo = ImRaii.Combo("Presets", string.Empty, ImGuiComboFlags.NoPreview))
        {
            if (combo)
                foreach (var (label, preMultiplier, preConstant) in PredefinedColorTransforms)
                {
                    if (ImGui.Selectable(label, multiplier == preMultiplier && constant == preConstant))
                    {
                        multiplier = preMultiplier;
                        constant   = preConstant;
                        changes    = true;
                    }
                }
        }

        ImGui.SameLine();
        ImGui.Dummy(ImGuiHelpers.ScaledVector2(20, 0));
        ImGui.SameLine();
        ImGui.TextUnformatted("Invert");
        ImGui.SameLine();
        if (ImGui.Button("Colors"))
        {
            InvertRed(ref multiplier, ref constant);
            InvertGreen(ref multiplier, ref constant);
            InvertBlue(ref multiplier, ref constant);
            changes = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("R"))
        {
            InvertRed(ref multiplier, ref constant);
            changes = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("G"))
        {
            InvertGreen(ref multiplier, ref constant);
            changes = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("B"))
        {
            InvertBlue(ref multiplier, ref constant);
            changes = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("A"))
        {
            InvertAlpha(ref multiplier, ref constant);
            changes = true;
        }

        return changes;
    }

    private static void InvertRed(ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        multiplier.M11 = -multiplier.M11;
        multiplier.M21 = -multiplier.M21;
        multiplier.M31 = -multiplier.M31;
        multiplier.M41 = -multiplier.M41;
        constant.X     = 1.0f - constant.X;
    }

    private static void InvertGreen(ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        multiplier.M12 = -multiplier.M12;
        multiplier.M22 = -multiplier.M22;
        multiplier.M32 = -multiplier.M32;
        multiplier.M42 = -multiplier.M42;
        constant.Y     = 1.0f - constant.Y;
    }

    private static void InvertBlue(ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        multiplier.M13 = -multiplier.M13;
        multiplier.M23 = -multiplier.M23;
        multiplier.M33 = -multiplier.M33;
        multiplier.M43 = -multiplier.M43;
        constant.Z     = 1.0f - constant.Z;
    }

    private static void InvertAlpha(ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        multiplier.M14 = -multiplier.M14;
        multiplier.M24 = -multiplier.M24;
        multiplier.M34 = -multiplier.M34;
        multiplier.M44 = -multiplier.M44;
        constant.W     = 1.0f - constant.W;
    }
}
