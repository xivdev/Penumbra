namespace Penumbra.Import.Models.Export;

public struct VertexColorXiv(Vector4 xivColor) : IXivVertex<VertexColor1, VertexTexture0Xiv, VertexFlow0>
{
    internal VertexColor1 Colors = new(xivColor);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);
}

public struct VertexColor2Xiv(Vector4 xivColor0, Vector4 xivColor1) : IXivVertex<VertexColor2, VertexTexture0Xiv, VertexFlow0>
{
    internal VertexColor2 Colors = new(xivColor0, xivColor1);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);
}

public struct VertexTexture1ColorXiv(Vector2 texCoord0, Vector4 xivColor) : IXivVertex<VertexColor1, VertexTexture1Xiv, VertexFlow0>
{
    internal VertexColor1 Colors = new(xivColor);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    internal VertexTexture1Xiv Coords = new(texCoord0);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);
}

public struct VertexTexture1Color2Xiv(Vector2 texCoord0, Vector4 xivColor0, Vector4 xivColor1)
    : IXivVertex<VertexColor2, VertexTexture1Xiv, VertexFlow0>
{
    internal VertexColor2 Colors = new(xivColor0, xivColor1);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    internal VertexTexture1Xiv Coords = new(texCoord0);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);
}

public struct VertexTexture2ColorXiv(Vector2 texCoord0, Vector2 texCoord1, Vector4 xivColor)
    : IXivVertex<VertexColor1, VertexTexture2Xiv, VertexFlow0>
{
    internal VertexColor1 Colors = new(xivColor);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    internal VertexTexture2Xiv Coords = new(texCoord0, texCoord1);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);
}

public struct VertexTexture2Color2Xiv(Vector2 texCoord0, Vector2 texCoord1, Vector4 xivColor0, Vector4 xivColor1)
    : IXivVertex<VertexColor2, VertexTexture2Xiv, VertexFlow0>
{
    internal VertexColor2 Colors = new(xivColor0, xivColor1);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    internal VertexTexture2Xiv Coords = new(texCoord0, texCoord1);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);
}

public struct VertexTexture3ColorXiv(Vector2 texCoord0, Vector2 texCoord1, Vector2 texCoord2, Vector4 xivColor)
    : IXivVertex<VertexColor1, VertexTexture3Xiv, VertexFlow0>
{
    internal VertexColor1 Colors = new(xivColor);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    internal VertexTexture3Xiv Coords = new(texCoord0, texCoord1, texCoord2);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);
}

public struct VertexTexture3Color2Xiv(Vector2 texCoord0, Vector2 texCoord1, Vector2 texCoord2, Vector4 xivColor0, Vector4 xivColor1)
    : IXivVertex<VertexColor2, VertexTexture3Xiv, VertexFlow0>
{
    internal VertexColor2 Colors = new(xivColor0, xivColor1);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    internal VertexTexture3Xiv Coords = new(texCoord0, texCoord1, texCoord2);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);
}

public struct VertexColorFlowXiv(Vector4 xivColor, Vector4 flow) : IXivVertex<VertexColor1, VertexTexture0Xiv, VertexFlow1>
{
    internal VertexColor1 Colors = new(xivColor);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    public Vector4 Flow { get; set; } = flow;
}

public struct VertexColor2FlowXiv(Vector4 xivColor0, Vector4 xivColor1, Vector4 flow) : IXivVertex<VertexColor2, VertexTexture0Xiv, VertexFlow1>
{
    internal VertexColor2 Colors = new(xivColor0, xivColor1);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    public Vector4 Flow { get; set; } = flow;
}

public struct VertexTexture1ColorFlowXiv(Vector2 texCoord0, Vector4 xivColor, Vector4 flow)
    : IXivVertex<VertexColor1, VertexTexture1Xiv, VertexFlow1>
{
    internal VertexColor1 Colors = new(xivColor);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    internal VertexTexture1Xiv Coords = new(texCoord0);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);

    public Vector4 Flow { get; set; } = flow;
}

public struct VertexTexture1Color2FlowXiv(Vector2 texCoord0, Vector4 xivColor0, Vector4 xivColor1, Vector4 flow)
    : IXivVertex<VertexColor2, VertexTexture1Xiv, VertexFlow1>
{
    internal VertexColor2 Colors = new(xivColor0, xivColor1);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    internal VertexTexture1Xiv Coords = new(texCoord0);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);

    public Vector4 Flow { get; set; } = flow;
}

public struct VertexTexture2ColorFlowXiv(Vector2 texCoord0, Vector2 texCoord1, Vector4 xivColor, Vector4 flow)
    : IXivVertex<VertexColor1, VertexTexture2Xiv, VertexFlow1>
{
    internal VertexColor1 Colors = new(xivColor);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    internal VertexTexture2Xiv Coords = new(texCoord0, texCoord1);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);

    public Vector4 Flow { get; set; } = flow;
}

public struct VertexTexture2Color2FlowXiv(Vector2 texCoord0, Vector2 texCoord1, Vector4 xivColor0, Vector4 xivColor1, Vector4 flow)
    : IXivVertex<VertexColor2, VertexTexture2Xiv, VertexFlow1>
{
    internal VertexColor2 Colors = new(xivColor0, xivColor1);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    internal VertexTexture2Xiv Coords = new(texCoord0, texCoord1);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);

    public Vector4 Flow { get; set; } = flow;
}

public struct VertexTexture3ColorFlowXiv(Vector2 texCoord0, Vector2 texCoord1, Vector2 texCoord2, Vector4 xivColor, Vector4 flow)
    : IXivVertex<VertexColor1, VertexTexture3Xiv, VertexFlow1>
{
    internal VertexColor1 Colors = new(xivColor);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    internal VertexTexture3Xiv Coords = new(texCoord0, texCoord1, texCoord2);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);

    public Vector4 Flow { get; set; } = flow;
}

public struct VertexTexture3Color2FlowXiv(
    Vector2 texCoord0,
    Vector2 texCoord1,
    Vector2 texCoord2,
    Vector4 xivColor0,
    Vector4 xivColor1,
    Vector4 flow)
    : IXivVertex<VertexColor2, VertexTexture3Xiv, VertexFlow1>
{
    internal VertexColor2 Colors = new(xivColor0, xivColor1);

    public readonly Vector4 GetXivColor(int index)
        => Colors.Get(index);

    public void SetXivColor(int index, Vector4 value)
        => Colors.Set(index, value);

    internal VertexTexture3Xiv Coords = new(texCoord0, texCoord1, texCoord2);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);

    public Vector4 Flow { get; set; } = flow;
}

public struct VertexFlowXiv(Vector4 flow) : IXivVertex<VertexColor0, VertexTexture0Xiv, VertexFlow1>
{
    public Vector4 Flow { get; set; } = flow;
}

public struct VertexTexture1FlowXiv(Vector2 texCoord0, Vector4 flow) : IXivVertex<VertexColor0, VertexTexture1Xiv, VertexFlow1>
{
    internal VertexTexture1Xiv Coords = new(texCoord0);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);

    public Vector4 Flow { get; set; } = flow;
}

public struct VertexTexture2FlowXiv(Vector2 texCoord0, Vector2 texCoord1, Vector4 flow)
    : IXivVertex<VertexColor0, VertexTexture2Xiv, VertexFlow1>
{
    internal VertexTexture2Xiv Coords = new(texCoord0, texCoord1);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);

    public Vector4 Flow { get; set; } = flow;
}

public struct VertexTexture3FlowXiv(Vector2 texCoord0, Vector2 texCoord1, Vector2 texCoord2, Vector4 flow)
    : IXivVertex<VertexColor0, VertexTexture3Xiv, VertexFlow1>
{
    internal VertexTexture3Xiv Coords = new(texCoord0, texCoord1, texCoord2);

    public readonly Vector2 GetTexCoord(int index)
        => Coords.Get(index);

    public void SetTexCoord(int index, Vector2 value)
        => Coords.Set(index, value);

    public Vector4 Flow { get; set; } = flow;
}
