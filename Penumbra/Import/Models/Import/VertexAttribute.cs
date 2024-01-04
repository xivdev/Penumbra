using Lumina.Data.Parsing;
using Penumbra.GameData.Files;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models.Import;

using Writer = Action<int, List<byte>>;
using Accessors = IReadOnlyDictionary<string, Accessor>;

public class VertexAttribute
{
    /// <summary> XIV vertex element metadata structure. </summary>
    public readonly MdlStructs.VertexElement Element;
    /// <summary> Write this vertex attribute's value at the specified index to the provided byte array. </summary>
    public readonly Writer Write;

    /// <summary> Size in bytes of a single vertex's attribute value. </summary>
    public byte Size => (MdlFile.VertexType)Element.Type switch
    {
        MdlFile.VertexType.Single3 => 12,
        MdlFile.VertexType.Single4 => 16,
        MdlFile.VertexType.UInt => 4,
        MdlFile.VertexType.ByteFloat4 => 4,
        MdlFile.VertexType.Half2 => 4,
        MdlFile.VertexType.Half4 => 8,

        _ => throw new Exception($"Unhandled vertex type {(MdlFile.VertexType)Element.Type}"),
    };

    public VertexAttribute(MdlStructs.VertexElement element, Writer write)
    {
        Element = element;
        Write = write;
    }

    public static VertexAttribute Position(Accessors accessors)
    {
        if (!accessors.TryGetValue("POSITION", out var accessor))
            throw new Exception("Meshes must contain a POSITION attribute.");

        var element = new MdlStructs.VertexElement()
        {
            Stream = 0,
            Type = (byte)MdlFile.VertexType.Single3,
            Usage = (byte)MdlFile.VertexUsage.Position,
        };

        var values = accessor.AsVector3Array();

        return new VertexAttribute(
            element,
            (index, bytes) => WriteSingle3(values[index], bytes)
        );
    }

    public static VertexAttribute? BlendWeight(Accessors accessors)
    {
        if (!accessors.TryGetValue("WEIGHTS_0", out var accessor))
            return null;

        if (!accessors.ContainsKey("JOINTS_0"))
            throw new Exception("Mesh contained WEIGHTS_0 attribute but no corresponding JOINTS_0 attribute.");

        var element = new MdlStructs.VertexElement()
        {
            Stream = 0,
            Type = (byte)MdlFile.VertexType.ByteFloat4,
            Usage = (byte)MdlFile.VertexUsage.BlendWeights,
        };

        var values = accessor.AsVector4Array();

        return new VertexAttribute(
            element,
            // TODO: TEMP TESTING PINNED TO BONE 0 UNTIL I SET UP BONE MAPPINGS
            // (index, bytes) => WriteByteFloat4(values[index], bytes)
            (index, bytes) => WriteByteFloat4(Vector4.UnitX, bytes)
        );
    }

    // TODO: this will need to take in a skeleton mapping of some kind so i can persist the bones used and wire up the joints correctly. hopefully by the "write vertex buffer" stage of building, we already know something about the skeleton.
    public static VertexAttribute? BlendIndex(Accessors accessors)
    {
        if (!accessors.TryGetValue("JOINTS_0", out var accessor))
            return null;

        if (!accessors.ContainsKey("WEIGHTS_0"))
            throw new Exception("Mesh contained JOINTS_0 attribute but no corresponding WEIGHTS_0 attribute.");

        var element = new MdlStructs.VertexElement()
        {
            Stream = 0,
            Type = (byte)MdlFile.VertexType.UInt,
            Usage = (byte)MdlFile.VertexUsage.BlendIndices,
        };

        var values = accessor.AsVector4Array();

        return new VertexAttribute(
            element,
            // TODO: TEMP TESTING PINNED TO BONE 0 UNTIL I SET UP BONE MAPPINGS
            // (index, bytes) => WriteUInt(values[index], bytes)
            (index, bytes) => WriteUInt(Vector4.Zero, bytes)
        );
    }

    public static VertexAttribute? Normal(Accessors accessors)
    {
        if (!accessors.TryGetValue("NORMAL", out var accessor))
            return null;

        var element = new MdlStructs.VertexElement()
        {
            Stream = 1,
            Type = (byte)MdlFile.VertexType.Half4,
            Usage = (byte)MdlFile.VertexUsage.Normal,
        };

        var values = accessor.AsVector3Array();

        return new VertexAttribute(
            element,
            (index, bytes) => WriteHalf4(new Vector4(values[index], 0), bytes)
        );
    }

    public static VertexAttribute? Uv(Accessors accessors)
    {
        if (!accessors.TryGetValue("TEXCOORD_0", out var accessor1))
            return null;

        // We're omitting type here, and filling it in on return, as there's two different types we might use.
        var element = new MdlStructs.VertexElement()
        {
            Stream = 1,
            Usage = (byte)MdlFile.VertexUsage.UV,
        };

        var values1 = accessor1.AsVector2Array();

        if (!accessors.TryGetValue("TEXCOORD_1", out var accessor2))
            return new VertexAttribute(
                element with { Type = (byte)MdlFile.VertexType.Half2 },
                (index, bytes) => WriteHalf2(values1[index], bytes)
            );

        var values2 = accessor2.AsVector2Array();

        return new VertexAttribute(
            element with { Type = (byte)MdlFile.VertexType.Half4 },
            (index, bytes) =>
            {
                var value1 = values1[index];
                var value2 = values2[index];
                WriteHalf4(new Vector4(value1.X, value1.Y, value2.X, value2.Y), bytes);
            }
        );
    }

    public static VertexAttribute? Tangent1(Accessors accessors)
    {
        if (!accessors.TryGetValue("TANGENT", out var accessor))
            return null;

        var element = new MdlStructs.VertexElement()
        {
            Stream = 1,
            Type = (byte)MdlFile.VertexType.ByteFloat4,
            Usage = (byte)MdlFile.VertexUsage.Tangent1,
        };

        var values = accessor.AsVector4Array();

        return new VertexAttribute(
            element,
            (index, bytes) => WriteByteFloat4(values[index], bytes)
        );
    }

    public static VertexAttribute? Color(Accessors accessors)
    {
        if (!accessors.TryGetValue("COLOR_0", out var accessor))
            return null;

        var element = new MdlStructs.VertexElement()
        {
            Stream = 1,
            Type = (byte)MdlFile.VertexType.ByteFloat4,
            Usage = (byte)MdlFile.VertexUsage.Color,
        };

        var values = accessor.AsVector4Array();

        return new VertexAttribute(
            element,
            (index, bytes) => WriteByteFloat4(values[index], bytes)
        );
    }

    private static void WriteSingle3(Vector3 input, List<byte> bytes)
    {
        bytes.AddRange(BitConverter.GetBytes(input.X));
        bytes.AddRange(BitConverter.GetBytes(input.Y));
        bytes.AddRange(BitConverter.GetBytes(input.Z));
    }

    private static void WriteUInt(Vector4 input, List<byte> bytes)
    {
        bytes.Add((byte)input.X);
        bytes.Add((byte)input.Y);
        bytes.Add((byte)input.Z);
        bytes.Add((byte)input.W);
    }

    private static void WriteByteFloat4(Vector4 input, List<byte> bytes)
    {
        bytes.Add((byte)Math.Round(input.X * 255f));
        bytes.Add((byte)Math.Round(input.Y * 255f));
        bytes.Add((byte)Math.Round(input.Z * 255f));
        bytes.Add((byte)Math.Round(input.W * 255f));
    }

    private static void WriteHalf2(Vector2 input, List<byte> bytes)
    {
        bytes.AddRange(BitConverter.GetBytes((Half)input.X));
        bytes.AddRange(BitConverter.GetBytes((Half)input.Y));
    }

    private static void WriteHalf4(Vector4 input, List<byte> bytes)
    {
        bytes.AddRange(BitConverter.GetBytes((Half)input.X));
        bytes.AddRange(BitConverter.GetBytes((Half)input.Y));
        bytes.AddRange(BitConverter.GetBytes((Half)input.Z));
        bytes.AddRange(BitConverter.GetBytes((Half)input.W));
    }
}
