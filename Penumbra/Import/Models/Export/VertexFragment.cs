using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Memory;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models.Export;

public interface IVertexTexture
{
    public static readonly IReadOnlyList<string> TexCoords = ["TEXCOORD_0", "TEXCOORD_1", "TEXCOORD_2", "TEXCOORD_3"];

    public static KeyValuePair<string, AttributeFormat> CreateCoord(int index)
        => KeyValuePair.Create(TexCoords[index], new AttributeFormat(DimensionType.VEC2, EncodingType.FLOAT, false));

    public abstract static int MaxTextCoords { get; }
}

public interface IVertexColor
{
    /// <remarks> We only realistically require UNSIGNED_BYTE for this, however Blender 3.6 errors on that (fixed in 4.0). </remarks>
    public static KeyValuePair<string, AttributeFormat> CreateColor(string name)
        => KeyValuePair.Create(name, new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));

    public abstract static IEnumerable<string> CustomAttributes { get; }

    public abstract static int MaxXivColors { get; }
}

public interface IVertexFlow
{
    public static KeyValuePair<string, AttributeFormat> CreateFlow(string name)
        => KeyValuePair.Create(name, new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));

    public abstract static IEnumerable<string> CustomAttributes { get; }

    public abstract static bool HasFlow { get; }
}

internal struct VertexFlow0 : IVertexFlow
{
    public static IEnumerable<string> CustomAttributes
        => [];

    public static bool HasFlow
        => false;
}

internal struct VertexFlow1 : IVertexFlow
{
    public static IEnumerable<string> CustomAttributes
        => ["_FFXIV_TANGENT_2"];

    public static bool HasFlow
        => true;
}

internal struct VertexColor0 : IVertexColor
{
    public static IEnumerable<string> CustomAttributes
        => [];

    public static int MaxXivColors
        => 0;
}

internal record struct VertexColor1(Vector4 Color) : IVertexColor
{
    public static IEnumerable<string> CustomAttributes
        => ["_FFXIV_COLOR"];

    public static int MaxXivColors
        => 1;

    public readonly Vector4 Get(int index)
        => index switch
        {
            0 => Color,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    public void Set(int index, Vector4 value)
    {
        switch (index)
        {
            case 0:  Color = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}

internal record struct VertexColor2(Vector4 Color0, Vector4 Color1) : IVertexColor
{
    public static IEnumerable<string> CustomAttributes
        => ["_FFXIV_COLOR_0", "_FFXIV_COLOR_1"];

    public static int MaxXivColors
        => 2;

    public readonly Vector4 Get(int index)
        => index switch
        {
            0 => Color0,
            1 => Color1,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    public void Set(int index, Vector4 value)
    {
        switch (index)
        {
            case 0:  Color0 = value; break;
            case 1:  Color1 = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}

internal struct VertexTexture0Xiv : IVertexTexture
{
    public static int MaxTextCoords
        => 0;
}

internal record struct VertexTexture1Xiv(Vector2 TexCoord0) : IVertexTexture
{
    public static int MaxTextCoords
        => 1;

    public readonly Vector2 Get(int index)
        => index switch
        {
            0 => TexCoord0,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    public void Set(int index, Vector2 value)
    {
        switch (index)
        {
            case 0:  TexCoord0 = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}

internal record struct VertexTexture2Xiv(Vector2 TexCoord0, Vector2 TexCoord1) : IVertexTexture
{
    public static int MaxTextCoords
        => 2;

    public readonly Vector2 Get(int index)
        => index switch
        {
            0 => TexCoord0,
            1 => TexCoord1,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    public void Set(int index, Vector2 value)
    {
        switch (index)
        {
            case 0:  TexCoord0 = value; break;
            case 1:  TexCoord1 = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}

internal record struct VertexTexture3Xiv(Vector2 TexCoord0, Vector2 TexCoord1, Vector2 TexCoord2) : IVertexTexture
{
    public static int MaxTextCoords
        => 3;

    public readonly Vector2 Get(int index)
        => index switch
        {
            0 => TexCoord0,
            1 => TexCoord1,
            2 => TexCoord2,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    public void Set(int index, Vector2 value)
    {
        switch (index)
        {
            case 0:  TexCoord0 = value; break;
            case 1:  TexCoord1 = value; break;
            case 2:  TexCoord2 = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}

public interface IXivVertex<TVertexColor, TVertexTexture, TVertexFlow> : IVertexCustom
    where TVertexColor : IVertexColor
    where TVertexTexture : IVertexTexture
    where TVertexFlow : IVertexFlow
{
    IEnumerable<KeyValuePair<string, AttributeFormat>> IVertexReflection.GetEncodingAttributes()
        =>
        [
            .. Enumerable.Range(0, TVertexTexture.MaxTextCoords).Select(IVertexTexture.CreateCoord),
            .. TVertexColor.CustomAttributes.Select(IVertexColor.CreateColor),
            .. TVertexFlow.CustomAttributes.Select(IVertexFlow.CreateFlow),
        ];

    int IVertexMaterial.MaxColors
        => 0;

    int IVertexMaterial.MaxTextCoords
        => TVertexTexture.MaxTextCoords;

    public bool HasFlow
        => TVertexFlow.HasFlow;

    public int MaxXivColors
        => TVertexColor.MaxXivColors;

    IEnumerable<string> IVertexCustom.CustomAttributes
        => [.. TVertexColor.CustomAttributes, .. TVertexFlow.CustomAttributes];

    void IVertexMaterial.Add(in VertexMaterialDelta delta)
    {
        for (var i = 0; i < TVertexTexture.MaxTextCoords; ++i)
            SetTexCoord(i, delta.GetTexCoord(i));
    }

    VertexMaterialDelta IVertexMaterial.Subtract(IVertexMaterial baseValue)
        => TVertexTexture.MaxTextCoords switch
        {
            0 => VertexMaterialDelta.Zero,
            1 => VertexMaterialDelta.Zero with { TexCoord0Delta = GetTexCoord(0) - baseValue.GetTexCoord(0) },
            2 => new VertexMaterialDelta(Vector4.Zero, Vector4.Zero, GetTexCoord(0) - baseValue.GetTexCoord(0),
                GetTexCoord(1) - baseValue.GetTexCoord(1)),
            3 => new VertexMaterialDelta(Vector4.Zero, Vector4.Zero, GetTexCoord(0) - baseValue.GetTexCoord(0),
                GetTexCoord(1) - baseValue.GetTexCoord(1),
                GetTexCoord(2) - baseValue.GetTexCoord(2), Vector2.Zero),
            4 => new VertexMaterialDelta(Vector4.Zero, Vector4.Zero, GetTexCoord(0) - baseValue.GetTexCoord(0),
                GetTexCoord(1) - baseValue.GetTexCoord(1),
                GetTexCoord(2) - baseValue.GetTexCoord(2), GetTexCoord(3) - baseValue.GetTexCoord(3)),
            _ => throw new NotSupportedException(),
        };

    bool IVertexCustom.TryGetCustomAttribute(string attributeName, out object? value)
    {
        foreach (var (index, attribute) in TVertexColor.CustomAttributes.Index())
        {
            if (attribute != attributeName)
                continue;

            value = GetXivColor(index);
            return true;
        }

        foreach (var attribute in TVertexFlow.CustomAttributes)
        {
            if (attribute != attributeName)
                continue;

            value = Flow;
            return true;
        }

        value = null;
        return false;
    }

    void IVertexCustom.SetCustomAttribute(string attributeName, object value)
    {
        if (value is not Vector4 valueVector4)
            return;

        foreach (var (index, attribute) in TVertexColor.CustomAttributes.Index())
        {
            if (attribute != attributeName)
                continue;

            SetXivColor(index, valueVector4);
            return;
        }

        foreach (var attribute in TVertexFlow.CustomAttributes)
        {
            if (attribute != attributeName)
                continue;

            Flow = valueVector4;
            return;
        }
    }

    public Vector4 GetXivColor(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public void SetXivColor(int index, Vector4 color)
        => throw new ArgumentOutOfRangeException(nameof(index));

    Vector2 IVertexMaterial.GetTexCoord(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    void IVertexMaterial.SetTexCoord(int index, Vector2 value)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public Vector4 Flow
    {
        get => throw new ArgumentOutOfRangeException(nameof(Flow));
        set => throw new ArgumentOutOfRangeException(nameof(Flow));
    }

    Vector4 IVertexMaterial.GetColor(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    void IVertexMaterial.SetColor(int index, Vector4 color)
    { }

    void IVertexCustom.Validate()
    {
        for (var i = 0; i < TVertexColor.MaxXivColors; ++i)
        {
            var color = GetXivColor(i);
            if (Vector4.GreaterThanAny(color, Vector4.One) || Vector4.LessThanAny(color, Vector4.Zero))
                throw new ArgumentOutOfRangeException($"XivColor{i}");
        }

        if (!HasFlow)
            return;

        var flow = Flow;
        if (Vector4.GreaterThanAny(flow, Vector4.One) || Vector4.LessThanAny(flow, Vector4.Zero))
            throw new ArgumentOutOfRangeException(nameof(Flow));
    }
}
