namespace Penumbra.Import.Textures;

public partial class CombinedTexture
{
    private enum CombineOp
    {
        LeftMultiply  = -4,
        LeftCopy      = -3,
        RightCopy     = -2,
        Invalid       = -1,
        Over          = 0,
        Under         = 1,
        RightMultiply = 2,
        CopyChannels  = 3,
    }

    private enum ResizeOp
    {
        LeftOnly  = -2,
        RightOnly = -1,
        None      = 0,
        ToLeft    = 1,
        ToRight   = 2,
    }

    [Flags]
    private enum Channels : byte
    {
        Red   = 1,
        Green = 2,
        Blue  = 4,
        Alpha = 8,
    }

    private static readonly IReadOnlyList<string> CombineOpLabels = new[]
    {
        "Overlay over Input",
        "Input over Overlay",
        "Replace Input",
        "Copy Channels",
    };

    private static readonly IReadOnlyList<string> CombineOpTooltips = new[]
    {
        "Standard composition.\nApply the overlay over the input.",
        "Standard composition, reversed.\nApply the input over the overlay ; can be used to fix some wrong imports.",
        "Completely replace the input with the overlay.\nCan be used to select the destination file as input and the source file as overlay.",
        "Replace some input channels with those from the overlay.\nUseful for Multi maps.",
    };

    private static readonly IReadOnlyList<string> ResizeOpLabels = new string[]
    {
        "No Resizing",
        "Adjust Overlay to Input",
        "Adjust Input to Overlay",
    };

    private static ResizeOp GetActualResizeOp(ResizeOp resizeOp, CombineOp combineOp)
        => combineOp switch
        {
            CombineOp.LeftCopy      => ResizeOp.LeftOnly,
            CombineOp.LeftMultiply  => ResizeOp.LeftOnly,
            CombineOp.RightCopy     => ResizeOp.RightOnly,
            CombineOp.RightMultiply => ResizeOp.RightOnly,
            CombineOp.Over          => resizeOp,
            CombineOp.Under         => resizeOp,
            CombineOp.CopyChannels  => resizeOp,
            _                       => throw new ArgumentException($"Invalid combine operation {combineOp}"),
        };

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


    private static bool InvertChannels(Channels channels, ref Matrix4x4 multiplier, ref Vector4 constant)
    {
        if (channels.HasFlag(Channels.Red))
            InvertRed(ref multiplier, ref constant);
        if (channels.HasFlag(Channels.Green))
            InvertGreen(ref multiplier, ref constant);
        if (channels.HasFlag(Channels.Blue))
            InvertBlue(ref multiplier, ref constant);
        if (channels.HasFlag(Channels.Alpha))
            InvertAlpha(ref multiplier, ref constant);
        return channels != 0;
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
