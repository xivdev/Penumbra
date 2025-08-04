using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Memory;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models.Export;

/*
Yeah, look, I tried to make this file less garbage. It's a little difficult.
Realistically, it will need to stick around until transforms/mutations are built
and there's reason to overhaul the export pipeline.
*/

public struct VertexColorFfxiv(Vector4 ffxivColor) : IVertexCustom
{
    public IEnumerable<KeyValuePair<string, AttributeFormat>> GetEncodingAttributes()
    {
        // NOTE: We only realistically require UNSIGNED_BYTE for this, however Blender 3.6 errors on that (fixed in 4.0).
        yield return new KeyValuePair<string, AttributeFormat>("_FFXIV_COLOR",
            new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));
    }

    public Vector4 FfxivColor = ffxivColor;

    public int MaxColors
        => 0;

    public int MaxTextCoords
        => 0;

    private static readonly string[] CustomNames = ["_FFXIV_COLOR"];

    public IEnumerable<string> CustomAttributes
        => CustomNames;

    public void Add(in VertexMaterialDelta delta)
    { }

    public VertexMaterialDelta Subtract(IVertexMaterial baseValue)
        => new(Vector4.Zero, Vector4.Zero, Vector2.Zero, Vector2.Zero);

    public Vector2 GetTexCoord(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public void SetTexCoord(int setIndex, Vector2 coord)
    { }

    public bool TryGetCustomAttribute(string attributeName, out object? value)
    {
        switch (attributeName)
        {
            case "_FFXIV_COLOR":
                value = FfxivColor;
                return true;

            default:
                value = null;
                return false;
        }
    }

    public void SetCustomAttribute(string attributeName, object value)
    {
        if (attributeName == "_FFXIV_COLOR" && value is Vector4 valueVector4)
            FfxivColor = valueVector4;
    }

    public Vector4 GetColor(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public void SetColor(int setIndex, Vector4 color)
    { }

    public void Validate()
    {
        var components = new[]
        {
            FfxivColor.X,
            FfxivColor.Y,
            FfxivColor.Z,
            FfxivColor.W,
        };
        if (components.Any(component => component < 0 || component > 1))
            throw new ArgumentOutOfRangeException(nameof(FfxivColor));
    }
}

public struct VertexColor2Ffxiv(Vector4 ffxivColor0, Vector4 ffxivColor1) : IVertexCustom
{
    public IEnumerable<KeyValuePair<string, AttributeFormat>> GetEncodingAttributes()
    {
        yield return new KeyValuePair<string, AttributeFormat>("_FFXIV_COLOR_0",
            new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));
        yield return new KeyValuePair<string, AttributeFormat>("_FFXIV_COLOR_1",
            new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));
    }

    public Vector4 FfxivColor0 = ffxivColor0;
    public Vector4 FfxivColor1 = ffxivColor1;

    public int MaxColors
        => 0;

    public int MaxTextCoords
        => 0;

    private static readonly string[] CustomNames = ["_FFXIV_COLOR_0", "_FFXIV_COLOR_1"];

    public IEnumerable<string> CustomAttributes
        => CustomNames;

    public void Add(in VertexMaterialDelta delta)
    { }

    public VertexMaterialDelta Subtract(IVertexMaterial baseValue)
        => new(Vector4.Zero, Vector4.Zero, Vector2.Zero, Vector2.Zero);

    public Vector2 GetTexCoord(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public void SetTexCoord(int setIndex, Vector2 coord)
    { }

    public bool TryGetCustomAttribute(string attributeName, out object? value)
    {
        switch (attributeName)
        {
            case "_FFXIV_COLOR_0":
                value = FfxivColor0;
                return true;
            
            case "_FFXIV_COLOR_1":
                value = FfxivColor1;
                return true;

            default:
                value = null;
                return false;
        }
    }

    public void SetCustomAttribute(string attributeName, object value)
    {
        switch (attributeName)
        {
            case "_FFXIV_COLOR_0" when value is Vector4 valueVector4:
                FfxivColor0 = valueVector4;
                break;
            case "_FFXIV_COLOR_1" when value is Vector4 valueVector4:
                FfxivColor1 = valueVector4;
                break;
        }
    }

    public Vector4 GetColor(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public void SetColor(int setIndex, Vector4 color)
    { }

    public void Validate()
    {
        var components = new[]
        {
            FfxivColor0.X,
            FfxivColor0.Y,
            FfxivColor0.Z,
            FfxivColor0.W,
        };
        if (components.Any(component => component is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(FfxivColor0));
        components =
        [
            FfxivColor1.X,
            FfxivColor1.Y,
            FfxivColor1.Z,
            FfxivColor1.W,
        ];
        if (components.Any(component => component is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(FfxivColor1));
    }
}


public struct VertexTexture1ColorFfxiv(Vector2 texCoord0, Vector4 ffxivColor) : IVertexCustom
{
    public IEnumerable<KeyValuePair<string, AttributeFormat>> GetEncodingAttributes()
    {
        yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_0",
            new AttributeFormat(DimensionType.VEC2, EncodingType.FLOAT, false));
        yield return new KeyValuePair<string, AttributeFormat>("_FFXIV_COLOR",
            new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));
    }

    public Vector2 TexCoord0 = texCoord0;

    public Vector4 FfxivColor = ffxivColor;

    public int MaxColors
        => 0;

    public int MaxTextCoords
        => 1;

    private static readonly string[] CustomNames = ["_FFXIV_COLOR"];

    public IEnumerable<string> CustomAttributes
        => CustomNames;

    public void Add(in VertexMaterialDelta delta)
    {
        TexCoord0 += delta.TexCoord0Delta;
    }

    public VertexMaterialDelta Subtract(IVertexMaterial baseValue)
        => new(Vector4.Zero, Vector4.Zero, TexCoord0 - baseValue.GetTexCoord(0), Vector2.Zero);

    public Vector2 GetTexCoord(int index)
        => index switch
        {
            0 => TexCoord0,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    public void SetTexCoord(int setIndex, Vector2 coord)
    {
        if (setIndex == 0)
            TexCoord0 = coord;
        if (setIndex >= 1)
            throw new ArgumentOutOfRangeException(nameof(setIndex));
    }

    public bool TryGetCustomAttribute(string attributeName, out object? value)
    {
        switch (attributeName)
        {
            case "_FFXIV_COLOR":
                value = FfxivColor;
                return true;

            default:
                value = null;
                return false;
        }
    }

    public void SetCustomAttribute(string attributeName, object value)
    {
        if (attributeName == "_FFXIV_COLOR" && value is Vector4 valueVector4)
            FfxivColor = valueVector4;
    }

    public Vector4 GetColor(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public void SetColor(int setIndex, Vector4 color)
    { }

    public void Validate()
    {
        var components = new[]
        {
            FfxivColor.X,
            FfxivColor.Y,
            FfxivColor.Z,
            FfxivColor.W,
        };
        if (components.Any(component => component < 0 || component > 1))
            throw new ArgumentOutOfRangeException(nameof(FfxivColor));
    }
}

public struct VertexTexture1Color2Ffxiv(Vector2 texCoord0, Vector4 ffxivColor0, Vector4 ffxivColor1) : IVertexCustom
{
    public IEnumerable<KeyValuePair<string, AttributeFormat>> GetEncodingAttributes()
    {
        yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_0",
            new AttributeFormat(DimensionType.VEC2, EncodingType.FLOAT, false));
        yield return new KeyValuePair<string, AttributeFormat>("_FFXIV_COLOR_0",
            new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));
        yield return new KeyValuePair<string, AttributeFormat>("_FFXIV_COLOR_1",
            new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));
    }

    public Vector2 TexCoord0 = texCoord0;

    public Vector4 FfxivColor0 = ffxivColor0;
    public Vector4 FfxivColor1 = ffxivColor1;

    public int MaxColors
        => 0;

    public int MaxTextCoords
        => 1;

    private static readonly string[] CustomNames = ["_FFXIV_COLOR_0", "_FFXIV_COLOR_1"];

    public IEnumerable<string> CustomAttributes
        => CustomNames;

    public void Add(in VertexMaterialDelta delta)
    {
        TexCoord0 += delta.TexCoord0Delta;
    }

    public VertexMaterialDelta Subtract(IVertexMaterial baseValue)
        => new(Vector4.Zero, Vector4.Zero, TexCoord0 - baseValue.GetTexCoord(0), Vector2.Zero);

    public Vector2 GetTexCoord(int index)
        => index switch
        {
            0 => TexCoord0,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    public void SetTexCoord(int setIndex, Vector2 coord)
    {
        if (setIndex == 0)
            TexCoord0 = coord;
        if (setIndex >= 1)
            throw new ArgumentOutOfRangeException(nameof(setIndex));
    }

    public bool TryGetCustomAttribute(string attributeName, out object? value)
    {
        switch (attributeName)
        {
            case "_FFXIV_COLOR_0":
                value = FfxivColor0;
                return true;
            
            case "_FFXIV_COLOR_1":
                value = FfxivColor1;
                return true;

            default:
                value = null;
                return false;
        }
    }

    public void SetCustomAttribute(string attributeName, object value)
    {
        switch (attributeName)
        {
            case "_FFXIV_COLOR_0" when value is Vector4 valueVector4:
                FfxivColor0 = valueVector4;
                break;
            case "_FFXIV_COLOR_1" when value is Vector4 valueVector4:
                FfxivColor1 = valueVector4;
                break;
        }
    }

    public Vector4 GetColor(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public void SetColor(int setIndex, Vector4 color)
    { }

    public void Validate()
    {
        var components = new[]
        {
            FfxivColor0.X,
            FfxivColor0.Y,
            FfxivColor0.Z,
            FfxivColor0.W,
        };
        if (components.Any(component => component is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(FfxivColor0));
        components =
        [
            FfxivColor1.X,
            FfxivColor1.Y,
            FfxivColor1.Z,
            FfxivColor1.W,
        ];
        if (components.Any(component => component is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(FfxivColor1));
    }
}


public struct VertexTexture2ColorFfxiv(Vector2 texCoord0, Vector2 texCoord1, Vector4 ffxivColor) : IVertexCustom
{
    public IEnumerable<KeyValuePair<string, AttributeFormat>> GetEncodingAttributes()
    {
        yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_0",
            new AttributeFormat(DimensionType.VEC2, EncodingType.FLOAT, false));
        yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_1",
            new AttributeFormat(DimensionType.VEC2, EncodingType.FLOAT, false));
        yield return new KeyValuePair<string, AttributeFormat>("_FFXIV_COLOR",
            new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));
    }

    public Vector2 TexCoord0  = texCoord0;
    public Vector2 TexCoord1  = texCoord1;
    public Vector4 FfxivColor = ffxivColor;

    public int MaxColors
        => 0;

    public int MaxTextCoords
        => 2;

    private static readonly string[] CustomNames = ["_FFXIV_COLOR"];

    public IEnumerable<string> CustomAttributes
        => CustomNames;

    public void Add(in VertexMaterialDelta delta)
    {
        TexCoord0 += delta.TexCoord0Delta;
        TexCoord1 += delta.TexCoord1Delta;
    }

    public VertexMaterialDelta Subtract(IVertexMaterial baseValue)
        => new(Vector4.Zero, Vector4.Zero, TexCoord0 - baseValue.GetTexCoord(0), TexCoord1 - baseValue.GetTexCoord(1));

    public Vector2 GetTexCoord(int index)
        => index switch
        {
            0 => TexCoord0,
            1 => TexCoord1,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    public void SetTexCoord(int setIndex, Vector2 coord)
    {
        if (setIndex == 0)
            TexCoord0 = coord;
        if (setIndex == 1)
            TexCoord1 = coord;
        if (setIndex >= 2)
            throw new ArgumentOutOfRangeException(nameof(setIndex));
    }

    public bool TryGetCustomAttribute(string attributeName, out object? value)
    {
        switch (attributeName)
        {
            case "_FFXIV_COLOR":
                value = FfxivColor;
                return true;

            default:
                value = null;
                return false;
        }
    }

    public void SetCustomAttribute(string attributeName, object value)
    {
        if (attributeName == "_FFXIV_COLOR" && value is Vector4 valueVector4)
            FfxivColor = valueVector4;
    }

    public Vector4 GetColor(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public void SetColor(int setIndex, Vector4 color)
    { }

    public void Validate()
    {
        var components = new[]
        {
            FfxivColor.X,
            FfxivColor.Y,
            FfxivColor.Z,
            FfxivColor.W,
        };
        if (components.Any(component => component < 0 || component > 1))
            throw new ArgumentOutOfRangeException(nameof(FfxivColor));
    }
}

public struct VertexTexture2Color2Ffxiv(Vector2 texCoord0, Vector2 texCoord1, Vector4 ffxivColor0, Vector4 ffxivColor1) : IVertexCustom
{
    public IEnumerable<KeyValuePair<string, AttributeFormat>> GetEncodingAttributes()
    {
        yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_0",
            new AttributeFormat(DimensionType.VEC2, EncodingType.FLOAT, false));
        yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_1",
            new AttributeFormat(DimensionType.VEC2, EncodingType.FLOAT, false));
        yield return new KeyValuePair<string, AttributeFormat>("_FFXIV_COLOR_0",
            new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));
        yield return new KeyValuePair<string, AttributeFormat>("_FFXIV_COLOR_1",
            new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));
    }

    public Vector2 TexCoord0   = texCoord0;
    public Vector2 TexCoord1   = texCoord1;
    public Vector4 FfxivColor0 = ffxivColor0;
    public Vector4 FfxivColor1 = ffxivColor1;

    public int MaxColors
        => 0;

    public int MaxTextCoords
        => 2;

    private static readonly string[] CustomNames = ["_FFXIV_COLOR_0", "_FFXIV_COLOR_1"];

    public IEnumerable<string> CustomAttributes
        => CustomNames;

    public void Add(in VertexMaterialDelta delta)
    {
        TexCoord0 += delta.TexCoord0Delta;
        TexCoord1 += delta.TexCoord1Delta;
    }

    public VertexMaterialDelta Subtract(IVertexMaterial baseValue)
        => new(Vector4.Zero, Vector4.Zero, TexCoord0 - baseValue.GetTexCoord(0), TexCoord1 - baseValue.GetTexCoord(1));

    public Vector2 GetTexCoord(int index)
        => index switch
        {
            0 => TexCoord0,
            1 => TexCoord1,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    public void SetTexCoord(int setIndex, Vector2 coord)
    {
        if (setIndex == 0)
            TexCoord0 = coord;
        if (setIndex == 1)
            TexCoord1 = coord;
        if (setIndex >= 2)
            throw new ArgumentOutOfRangeException(nameof(setIndex));
    }

    public bool TryGetCustomAttribute(string attributeName, out object? value)
    {
        switch (attributeName)
        {
            case "_FFXIV_COLOR_0":
                value = FfxivColor0;
                return true;
            
            case "_FFXIV_COLOR_1":
                value = FfxivColor1;
                return true;

            default:
                value = null;
                return false;
        }
    }

    public void SetCustomAttribute(string attributeName, object value)
    {
        switch (attributeName)
        {
            case "_FFXIV_COLOR_0" when value is Vector4 valueVector4:
                FfxivColor0 = valueVector4;
                break;
            case "_FFXIV_COLOR_1" when value is Vector4 valueVector4:
                FfxivColor1 = valueVector4;
                break;
        }
    }

    public Vector4 GetColor(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public void SetColor(int setIndex, Vector4 color)
    { }

    public void Validate()
    {
        var components = new[]
        {
            FfxivColor0.X,
            FfxivColor0.Y,
            FfxivColor0.Z,
            FfxivColor0.W,
        };
        if (components.Any(component => component is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(FfxivColor0));
        components =
        [
            FfxivColor1.X,
            FfxivColor1.Y,
            FfxivColor1.Z,
            FfxivColor1.W,
        ];
        if (components.Any(component => component is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(FfxivColor1));
    }

}

public struct VertexTexture3ColorFfxiv(Vector2 texCoord0, Vector2 texCoord1, Vector2 texCoord2, Vector4 ffxivColor)
    : IVertexCustom
{
    public IEnumerable<KeyValuePair<string, AttributeFormat>> GetEncodingAttributes()
    {
        yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_0",
            new AttributeFormat(DimensionType.VEC2, EncodingType.FLOAT, false));
        yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_1",
            new AttributeFormat(DimensionType.VEC2, EncodingType.FLOAT, false));
        yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_2",
            new AttributeFormat(DimensionType.VEC2, EncodingType.FLOAT, false));
        yield return new KeyValuePair<string, AttributeFormat>("_FFXIV_COLOR",
            new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));
    }

    public Vector2 TexCoord0  = texCoord0;
    public Vector2 TexCoord1  = texCoord1;
    public Vector2 TexCoord2  = texCoord2;
    public Vector4 FfxivColor = ffxivColor;

    public int MaxColors
        => 0;

    public int MaxTextCoords
        => 3;

    private static readonly string[] CustomNames = ["_FFXIV_COLOR"];

    public IEnumerable<string> CustomAttributes
        => CustomNames;

    public void Add(in VertexMaterialDelta delta)
    {
        TexCoord0 += delta.TexCoord0Delta;
        TexCoord1 += delta.TexCoord1Delta;
        TexCoord2 += delta.TexCoord2Delta;
    }

    public VertexMaterialDelta Subtract(IVertexMaterial baseValue)
        => new(Vector4.Zero, Vector4.Zero, TexCoord0 - baseValue.GetTexCoord(0), TexCoord1 - baseValue.GetTexCoord(1));

    public Vector2 GetTexCoord(int index)
        => index switch
        {
            0 => TexCoord0,
            1 => TexCoord1,
            2 => TexCoord2,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    public void SetTexCoord(int setIndex, Vector2 coord)
    {
        if (setIndex == 0)
            TexCoord0 = coord;
        if (setIndex == 1)
            TexCoord1 = coord;
        if (setIndex == 2)
            TexCoord2 = coord;
        if (setIndex >= 3)
            throw new ArgumentOutOfRangeException(nameof(setIndex));
    }

    public bool TryGetCustomAttribute(string attributeName, out object? value)
    {
        switch (attributeName)
        {
            case "_FFXIV_COLOR":
                value = FfxivColor;
                return true;

            default:
                value = null;
                return false;
        }
    }

    public void SetCustomAttribute(string attributeName, object value)
    {
        if (attributeName == "_FFXIV_COLOR" && value is Vector4 valueVector4)
            FfxivColor = valueVector4;
    }

    public Vector4 GetColor(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public void SetColor(int setIndex, Vector4 color)
    { }

    public void Validate()
    {
        var components = new[]
        {
            FfxivColor.X,
            FfxivColor.Y,
            FfxivColor.Z,
            FfxivColor.W,
        };
        if (components.Any(component => component is < 0f or > 1f))
            throw new ArgumentOutOfRangeException(nameof(FfxivColor));
    }
}

public struct VertexTexture3Color2Ffxiv(Vector2 texCoord0, Vector2 texCoord1, Vector2 texCoord2, Vector4 ffxivColor0, Vector4 ffxivColor1)
    : IVertexCustom
{
    public IEnumerable<KeyValuePair<string, AttributeFormat>> GetEncodingAttributes()
    {
        yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_0",
            new AttributeFormat(DimensionType.VEC2, EncodingType.FLOAT, false));
        yield return new KeyValuePair<string, AttributeFormat>("TEXCOORD_1",
            new AttributeFormat(DimensionType.VEC2, EncodingType.FLOAT, false));
        yield return new KeyValuePair<string, AttributeFormat>("_FFXIV_COLOR_0",
            new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));
        yield return new KeyValuePair<string, AttributeFormat>("_FFXIV_COLOR_1",
            new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT, true));
    }

    public Vector2 TexCoord0   = texCoord0;
    public Vector2 TexCoord1   = texCoord1;
    public Vector2 TexCoord2   = texCoord2;
    public Vector4 FfxivColor0 = ffxivColor0;
    public Vector4 FfxivColor1 = ffxivColor1;

    public int MaxColors
        => 0;

    public int MaxTextCoords
        => 3;

    private static readonly string[] CustomNames = ["_FFXIV_COLOR_0", "_FFXIV_COLOR_1"];

    public IEnumerable<string> CustomAttributes
        => CustomNames;

    public void Add(in VertexMaterialDelta delta)
    {
        TexCoord0 += delta.TexCoord0Delta;
        TexCoord1 += delta.TexCoord1Delta;
        TexCoord2 += delta.TexCoord2Delta;
    }

    public VertexMaterialDelta Subtract(IVertexMaterial baseValue)
        => new(Vector4.Zero, Vector4.Zero, TexCoord0 - baseValue.GetTexCoord(0), TexCoord1 - baseValue.GetTexCoord(1));

    public Vector2 GetTexCoord(int index)
        => index switch
        {
            0 => TexCoord0,
            1 => TexCoord1,
            2 => TexCoord2,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    public void SetTexCoord(int setIndex, Vector2 coord)
    {
        if (setIndex == 0)
            TexCoord0 = coord;
        if (setIndex == 1)
            TexCoord1 = coord;
        if (setIndex == 2)
            TexCoord2 = coord;
        if (setIndex >= 3)
            throw new ArgumentOutOfRangeException(nameof(setIndex));
    }

    public bool TryGetCustomAttribute(string attributeName, out object? value)
    {
        switch (attributeName)
        {
            case "_FFXIV_COLOR_0":
                value = FfxivColor0;
                return true;
            
            case "_FFXIV_COLOR_1":
                value = FfxivColor1;
                return true;

            default:
                value = null;
                return false;
        }
    }

    public void SetCustomAttribute(string attributeName, object value)
    {
        switch (attributeName)
        {
            case "_FFXIV_COLOR_0" when value is Vector4 valueVector4:
                FfxivColor0 = valueVector4;
                break;
            case "_FFXIV_COLOR_1" when value is Vector4 valueVector4:
                FfxivColor1 = valueVector4;
                break;
        }
    }

    public Vector4 GetColor(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public void SetColor(int setIndex, Vector4 color)
    { }

    public void Validate()
    {
        var components = new[]
        {
            FfxivColor0.X,
            FfxivColor0.Y,
            FfxivColor0.Z,
            FfxivColor0.W,
        };
        if (components.Any(component => component is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(FfxivColor0));
        components =
        [
            FfxivColor1.X,
            FfxivColor1.Y,
            FfxivColor1.Z,
            FfxivColor1.W,
        ];
        if (components.Any(component => component is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(FfxivColor1));
    }
}
