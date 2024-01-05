using Lumina.Data.Parsing;
using Penumbra.GameData.Files;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models.Import;

using BuildFn = Func<int, byte[]>;
using HasMorphFn = Func<int, int, bool>;
using BuildMorphFn = Func<int, int, byte[]>;
using Accessors = IReadOnlyDictionary<string, Accessor>;

public class VertexAttribute
{
    /// <summary> XIV vertex element metadata structure. </summary>
    public readonly MdlStructs.VertexElement Element;
    /// <summary> Write this vertex attribute's value at the specified index to the provided byte array. </summary>
    public readonly BuildFn Build;

    public readonly HasMorphFn HasMorph;

    public readonly BuildMorphFn BuildMorph;

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

    public VertexAttribute(
        MdlStructs.VertexElement element,
        BuildFn write,
        HasMorphFn? hasMorph = null,
        BuildMorphFn? buildMorph = null
    )
    {
        Element = element;
        Build = write;
        HasMorph = hasMorph ?? DefaultHasMorph;
        BuildMorph = buildMorph ?? DefaultBuildMorph;
    }

    // todo: this is per-shape at the moment - consider if it should do them all at once (i mean we always want to check all of them, it's mostly a semantics question on who owns the loop)
    private static bool DefaultHasMorph(int morphIndex, int vertexIndex)
    {
        return false;
    }

    // xiv stores shapes as full vertex replacements, so the default value for a morph attribute is simply it's built state (rather than a delta or w/e)
    private byte[] DefaultBuildMorph(int morphIndex, int vertexIndex)
    {
        return Build(vertexIndex);
    }

    public static VertexAttribute Position(Accessors accessors, IEnumerable<Accessors> morphAccessors)
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

        var foo = morphAccessors
            .Select(ma => ma.GetValueOrDefault("POSITION")?.AsVector3Array())
            .ToArray();

        return new VertexAttribute(
            element,
            index => BuildSingle3(values[index]),
            // TODO: at the moment this is only defined for position - is it worth setting one up for normal, too?
            (morphIndex, vertexIndex) =>
            {
                var deltas = foo[morphIndex];
                if (deltas == null) return false;
                var delta = deltas[vertexIndex];
                return delta != Vector3.Zero;
            },
            // TODO: this will _need_ to be defined for any values that appear in morphs, i.e. geom and maybe mats
            (morphIndex, vertexIndex) =>
            {
                var value = values[vertexIndex];

                var delta = foo[morphIndex]?[vertexIndex];
                if (delta != null)
                    value += delta.Value;

                return BuildSingle3(value);
            }
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
            index => BuildByteFloat4(values[index])
        );
    }

    // TODO: this will need to take in a skeleton mapping of some kind so i can persist the bones used and wire up the joints correctly. hopefully by the "write vertex buffer" stage of building, we already know something about the skeleton.
    public static VertexAttribute? BlendIndex(Accessors accessors, IDictionary<ushort, ushort>? boneMap)
    {
        if (!accessors.TryGetValue("JOINTS_0", out var accessor))
            return null;

        if (!accessors.ContainsKey("WEIGHTS_0"))
            throw new Exception("Mesh contained JOINTS_0 attribute but no corresponding WEIGHTS_0 attribute.");

        if (boneMap == null)
            throw new Exception("Mesh contained JOINTS_0 attribute but no bone mapping was created.");

        var element = new MdlStructs.VertexElement()
        {
            Stream = 0,
            Type = (byte)MdlFile.VertexType.UInt,
            Usage = (byte)MdlFile.VertexUsage.BlendIndices,
        };

        var values = accessor.AsVector4Array();

        return new VertexAttribute(
            element,
            index => {
                var foo = values[index];
                return BuildUInt(new Vector4(
                    boneMap[(ushort)foo.X],
                    boneMap[(ushort)foo.Y],
                    boneMap[(ushort)foo.Z],
                    boneMap[(ushort)foo.W]
                ));
            }
        );
    }

    public static VertexAttribute? Normal(Accessors accessors, IEnumerable<Accessors> morphAccessors)
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

        var foo = morphAccessors
            .Select(ma => ma.GetValueOrDefault("NORMAL")?.AsVector3Array())
            .ToArray();

        return new VertexAttribute(
            element,
            index => BuildHalf4(new Vector4(values[index], 0)),
            null,
            (morphIndex, vertexIndex) =>
            {
                var value = values[vertexIndex];

                var delta = foo[morphIndex]?[vertexIndex];
                if (delta != null)
                    value += delta.Value;

                return BuildHalf4(new Vector4(value, 0));
            }
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
                index => BuildHalf2(values1[index])
            );

        var values2 = accessor2.AsVector2Array();

        return new VertexAttribute(
            element with { Type = (byte)MdlFile.VertexType.Half4 },
            index =>
            {
                var value1 = values1[index];
                var value2 = values2[index];
                return BuildHalf4(new Vector4(value1.X, value1.Y, value2.X, value2.Y));
            }
        );
    }

    public static VertexAttribute? Tangent1(Accessors accessors, IEnumerable<Accessors> morphAccessors)
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

        var foo = morphAccessors
            .Select(ma => ma.GetValueOrDefault("TANGENT")?.AsVector3Array())
            .ToArray();

        return new VertexAttribute(
            element,
            index => BuildByteFloat4(values[index]),
            null,
            (morphIndex, vertexIndex) =>
            {
                var value = values[vertexIndex];

                var delta = foo[morphIndex]?[vertexIndex];
                if (delta != null)
                    value += new Vector4(delta.Value, 0);

                return BuildByteFloat4(value);
            }
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
            index => BuildByteFloat4(values[index])
        );
    }

    private static byte[] BuildSingle3(Vector3 input)
    {
        return [
            ..BitConverter.GetBytes(input.X),
            ..BitConverter.GetBytes(input.Y),
            ..BitConverter.GetBytes(input.Z),
        ];
    }

    private static byte[] BuildUInt(Vector4 input)
    {
        return [
            (byte)input.X,
            (byte)input.Y,
            (byte)input.Z,
            (byte)input.W,
        ];
    }

    private static byte[] BuildByteFloat4(Vector4 input)
    {
        return [
            (byte)Math.Round(input.X * 255f),
            (byte)Math.Round(input.Y * 255f),
            (byte)Math.Round(input.Z * 255f),
            (byte)Math.Round(input.W * 255f),
        ];
    }

    private static byte[] BuildHalf2(Vector2 input)
    {
        return [
            ..BitConverter.GetBytes((Half)input.X),
            ..BitConverter.GetBytes((Half)input.Y),
        ];
    }

    private static byte[] BuildHalf4(Vector4 input)
    {
        return [
            ..BitConverter.GetBytes((Half)input.X),
            ..BitConverter.GetBytes((Half)input.Y),
            ..BitConverter.GetBytes((Half)input.Z),
            ..BitConverter.GetBytes((Half)input.W),
        ];
    }
}
