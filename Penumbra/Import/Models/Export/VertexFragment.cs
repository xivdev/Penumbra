using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models.Export;

public struct VertexTexture2ColorFfxiv : IVertexCustom
{
    public const string FFXIV_COLOR = "_FFXIV_COLOR";

    [VertexAttribute("TEXCOORD_0")]
    public Vector2 TexCoord0;

    [VertexAttribute("TEXCOORD_1")]
    public Vector2 TexCoord1;

    [VertexAttribute(FFXIV_COLOR, EncodingType.UNSIGNED_BYTE, false)]
    public Vector4 FfxivColor;

    public int MaxColors => 0;

    public int MaxTextCoords => 2;

    private static readonly string[] CustomNames = [FFXIV_COLOR];
    public IEnumerable<string> CustomAttributes => CustomNames;

    public VertexTexture2ColorFfxiv(Vector2 texCoord0, Vector2 texCoord1, Vector4 ffxivColor)
    {
        TexCoord0 = texCoord0;
        TexCoord1 = texCoord1;
        FfxivColor = ffxivColor;
    }

    public void Add(in VertexMaterialDelta delta)
    {
        TexCoord0 += delta.TexCoord0Delta;
        TexCoord1 += delta.TexCoord1Delta;
    }

    public VertexMaterialDelta Subtract(IVertexMaterial baseValue)
    {
        return new VertexMaterialDelta(Vector4.Zero, Vector4.Zero, TexCoord0 - baseValue.GetTexCoord(0), TexCoord1 - baseValue.GetTexCoord(1));
    }

    public Vector2 GetTexCoord(int index)
        => index switch
        {
            0 => TexCoord0,
            1 => TexCoord1,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    public void SetTexCoord(int setIndex, Vector2 coord)
    {
        if (setIndex == 0) TexCoord0 = coord;
        if (setIndex == 1) TexCoord1 = coord;
        if (setIndex >= 2) throw new ArgumentOutOfRangeException(nameof(setIndex));
    }

    public bool TryGetCustomAttribute(string attributeName, out object? value)
    {
        switch (attributeName)
        {
            case FFXIV_COLOR:
                value = FfxivColor;
                return true;

            default:
                value = null;
                return false;
        }
    }

    public void SetCustomAttribute(string attributeName, object value)
    {
        if (attributeName == FFXIV_COLOR && value is Vector4 valueVector4)
            FfxivColor = valueVector4;
    }

    public Vector4 GetColor(int index)
        => throw new ArgumentOutOfRangeException(nameof(index));

    public void SetColor(int setIndex, Vector4 color)
    {
    }

    public void Validate()
    {
        var components = new[] { FfxivColor.X, FfxivColor.Y, FfxivColor.Z, FfxivColor.W };
        if (components.Any(component => component < 0 || component > 1))
            throw new ArgumentOutOfRangeException(nameof(FfxivColor));
    }
}
