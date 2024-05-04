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

    /// <summary> Build a byte array containing this vertex attribute's data for the specified vertex index. </summary>
    public readonly BuildFn Build;

    /// <summary> Check if the specified morph target index contains a morph for the specified vertex index. </summary>
    public readonly HasMorphFn HasMorph;

    /// <summary> Build a byte array containing this vertex attribute's data, as modified by the specified morph target, for the specified vertex index. </summary>
    public readonly BuildMorphFn BuildMorph;

    public byte Stream
        => Element.Stream;

    /// <summary> Size in bytes of a single vertex's attribute value. </summary>
    public byte Size
        => (MdlFile.VertexType)Element.Type switch
        {
            MdlFile.VertexType.Single1 => 4,
            MdlFile.VertexType.Single2 => 8,
            MdlFile.VertexType.Single3 => 12,
            MdlFile.VertexType.Single4 => 16,
            MdlFile.VertexType.UByte4  => 4,
            MdlFile.VertexType.Short2  => 4,
            MdlFile.VertexType.Short4  => 8,
            MdlFile.VertexType.NByte4  => 4,
            MdlFile.VertexType.Half2   => 4,
            MdlFile.VertexType.Half4   => 8,

            _ => throw new Exception($"Unhandled vertex type {(MdlFile.VertexType)Element.Type}"),
        };

    private VertexAttribute(
        MdlStructs.VertexElement element,
        BuildFn write,
        HasMorphFn? hasMorph = null,
        BuildMorphFn? buildMorph = null
    )
    {
        Element    = element;
        Build      = write;
        HasMorph   = hasMorph ?? DefaultHasMorph;
        BuildMorph = buildMorph ?? DefaultBuildMorph;
    }

    public VertexAttribute WithOffset(byte offset)
        => new(
            Element with { Offset = offset },
            Build,
            HasMorph,
            BuildMorph
        );

    /// <remarks> We assume that attributes don't have morph data unless explicitly configured. </remarks>
    private static bool DefaultHasMorph(int morphIndex, int vertexIndex)
        => false;

    /// <remarks>
    /// XIV stores shapes as full vertex replacements, so all attributes need to output something for a morph.
    /// As a fallback, we're just building the normal vertex data for the index.
    /// </remarks>>
    private byte[] DefaultBuildMorph(int morphIndex, int vertexIndex)
        => Build(vertexIndex);

    public static VertexAttribute Position(Accessors accessors, IEnumerable<Accessors> morphAccessors, IoNotifier notifier)
    {
        if (!accessors.TryGetValue("POSITION", out var accessor))
            throw notifier.Exception("Meshes must contain a POSITION attribute.");

        var element = new MdlStructs.VertexElement()
        {
            Stream = 0,
            Type   = (byte)MdlFile.VertexType.Single3,
            Usage  = (byte)MdlFile.VertexUsage.Position,
        };

        var values = accessor.AsVector3Array();

        var morphValues = morphAccessors
            .Select(a => a.GetValueOrDefault("POSITION")?.AsVector3Array())
            .ToArray();

        return new VertexAttribute(
            element,
            index => BuildSingle3(values[index]),
            (morphIndex, vertexIndex) =>
            {
                var deltas = morphValues[morphIndex];
                if (deltas == null)
                    return false;

                var delta = deltas[vertexIndex];
                return delta != Vector3.Zero;
            },
            (morphIndex, vertexIndex) =>
            {
                var value = values[vertexIndex];

                var delta = morphValues[morphIndex]?[vertexIndex];
                if (delta != null)
                    value += delta.Value;

                return BuildSingle3(value);
            }
        );
    }

    public static VertexAttribute? BlendWeight(Accessors accessors, IoNotifier notifier)
    {
        if (!accessors.TryGetValue("WEIGHTS_0", out var accessor))
            return null;

        if (!accessors.ContainsKey("JOINTS_0"))
            throw notifier.Exception("Mesh contained WEIGHTS_0 attribute but no corresponding JOINTS_0 attribute.");

        var element = new MdlStructs.VertexElement()
        {
            Stream = 0,
            Type   = (byte)MdlFile.VertexType.NByte4,
            Usage  = (byte)MdlFile.VertexUsage.BlendWeights,
        };

        var values = accessor.AsVector4Array();

        return new VertexAttribute(
            element,
            index => {
                // Blend weights are _very_ sensitive to float imprecision - a vertex sum being off
                // by one, such as 256, is enough to cause a visible defect. To avoid this, we tweak
                // the converted values to have the expected sum, preferencing values with minimal differences.
                var originalValues = values[index];
                var byteValues = BuildNByte4(originalValues);

                var adjustment = 255 - byteValues.Select(value => (int)value).Sum();
                while (adjustment != 0)
                {
                    var convertedValues = byteValues.Select(value => value * (1f / 255f)).ToArray();
                    var closestIndex = Enumerable.Range(0, 4)
                        .Select(index => (index, delta: Math.Abs(originalValues[index] - convertedValues[index])))
                        .MinBy(x => x.delta)
                        .index;
                    byteValues[closestIndex] = (byte)(byteValues[closestIndex] + Math.CopySign(1, adjustment));
                    adjustment = 255 - byteValues.Select(value => (int)value).Sum();
                }
                
                return byteValues;
            }
        );
    }

    public static VertexAttribute? BlendIndex(Accessors accessors, IDictionary<ushort, ushort>? boneMap, IoNotifier notifier)
    {
        if (!accessors.TryGetValue("JOINTS_0", out var jointsAccessor))
            return null;

        if (!accessors.TryGetValue("WEIGHTS_0", out var weightsAccessor))
            throw notifier.Exception("Mesh contained JOINTS_0 attribute but no corresponding WEIGHTS_0 attribute.");

        if (boneMap == null)
            throw notifier.Exception("Mesh contained JOINTS_0 attribute but no bone mapping was created.");

        var element = new MdlStructs.VertexElement()
        {
            Stream = 0,
            Type   = (byte)MdlFile.VertexType.UByte4,
            Usage  = (byte)MdlFile.VertexUsage.BlendIndices,
        };

        var joints = jointsAccessor.AsVector4Array();
        var weights = weightsAccessor.AsVector4Array();

        return new VertexAttribute(
            element,
            index =>
            {
                var gltfIndices = joints[index];
                var gltfWeights = weights[index]; 

                return BuildUByte4(new Vector4(
                    gltfWeights.X == 0 ? 0 : boneMap[(ushort)gltfIndices.X],
                    gltfWeights.Y == 0 ? 0 : boneMap[(ushort)gltfIndices.Y],
                    gltfWeights.Z == 0 ? 0 : boneMap[(ushort)gltfIndices.Z],
                    gltfWeights.W == 0 ? 0 : boneMap[(ushort)gltfIndices.W]
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
            Type   = (byte)MdlFile.VertexType.Single3,
            Usage  = (byte)MdlFile.VertexUsage.Normal,
        };

        var values = accessor.AsVector3Array();

        var morphValues = morphAccessors
            .Select(a => a.GetValueOrDefault("NORMAL")?.AsVector3Array())
            .ToArray();

        return new VertexAttribute(
            element,
            index => BuildSingle3(values[index]),
            buildMorph: (morphIndex, vertexIndex) =>
            {
                var value = values[vertexIndex];

                var delta = morphValues[morphIndex]?[vertexIndex];
                if (delta != null)
                    value += delta.Value;

                return BuildSingle3(value);
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
            Usage  = (byte)MdlFile.VertexUsage.UV,
        };

        var values1 = accessor1.AsVector2Array();

        // There's only one TEXCOORD, output UV coordinates as vec2s.
        if (!accessors.TryGetValue("TEXCOORD_1", out var accessor2))
            return new VertexAttribute(
                element with { Type = (byte)MdlFile.VertexType.Single2 },
                index => BuildSingle2(values1[index])
            );

        var values2 = accessor2.AsVector2Array();

        // Two TEXCOORDs are available, repack them into xiv's vec4 [0X, 0Y, 1X, 1Y] format.
        return new VertexAttribute(
            element with { Type = (byte)MdlFile.VertexType.Single4 },
            index =>
            {
                var value1 = values1[index];
                var value2 = values2[index];
                return BuildSingle4(new Vector4(value1.X, value1.Y, value2.X, value2.Y));
            }
        );
    }

    public static VertexAttribute? Tangent1(Accessors accessors, IEnumerable<Accessors> morphAccessors, ushort[] indices, IoNotifier notifier)
    {
        if (!accessors.TryGetValue("NORMAL", out var normalAccessor))
        {
            notifier.Warning("Normals are required to facilitate import or calculation of tangents.");
            return null;
        }

        var normals = normalAccessor.AsVector3Array();
        var tangents = accessors.TryGetValue("TANGENT", out var accessor)
            ? accessor.AsVector4Array()
            : CalculateTangents(accessors, indices, normals, notifier);

        if (tangents == null)
        {
            notifier.Warning("No tangents available for sub-mesh. This could lead to incorrect lighting, or mismatched vertex attributes.");
            return null;
        }

        var element = new MdlStructs.VertexElement
        {
            Stream = 1,
            Type   = (byte)MdlFile.VertexType.NByte4,
            Usage  = (byte)MdlFile.VertexUsage.Tangent1,
        };

        // Per glTF specification, TANGENT morph values are stored as vec3, with the W component always considered to be 0.
        var morphValues = morphAccessors
            .Select(a => (Tangent: a.GetValueOrDefault("TANGENT")?.AsVector3Array(),
                Normal: a.GetValueOrDefault("NORMAL")?.AsVector3Array()))
            .ToList();

        return new VertexAttribute(
            element,
            index => BuildBitangent(tangents[index], normals[index]),
            buildMorph: (morphIndex, vertexIndex) =>
            {
                var tangent      = tangents[vertexIndex];
                var tangentDelta = morphValues[morphIndex].Tangent?[vertexIndex];
                if (tangentDelta != null)
                    tangent += new Vector4(tangentDelta.Value, 0);

                var normal      = normals[vertexIndex];
                var normalDelta = morphValues[morphIndex].Normal?[vertexIndex];
                if (normalDelta != null)
                    normal += normalDelta.Value;

                return BuildBitangent(tangent, normal);
            }
        );
    }

    /// <summary> Build a byte array representing bitangent data computed from the provided tangent and normal. </summary>
    /// <remarks> XIV primarily stores bitangents, rather than tangents as with most other software, so we calculate on import. </remarks>
    private static byte[] BuildBitangent(Vector4 tangent, Vector3 normal)
    {
        var handedness = tangent.W;
        var tangent3   = new Vector3(tangent.X, tangent.Y, tangent.Z);
        var bitangent  = Vector3.Normalize(Vector3.Cross(normal, tangent3));
        bitangent *= handedness;

        // Byte floats encode 0..1, and bitangents are stored as -1..1. Convert.
        bitangent = (bitangent + Vector3.One) / 2;
        return BuildNByte4(new Vector4(bitangent, handedness));
    }

    /// <summary> Attempt to calculate tangent values based on other pre-existing data. </summary>
    private static Vector4[]? CalculateTangents(Accessors accessors, ushort[] indices, IList<Vector3> normals, IoNotifier notifier)
    {
        // To calculate tangents, we will also need access to uv data.
        if (!accessors.TryGetValue("TEXCOORD_0", out var uvAccessor))
            return null;

        var positions = accessors["POSITION"].AsVector3Array();
        var uvs       = uvAccessor.AsVector2Array();

        notifier.Warning(
            "Calculating tangents, this may result in degraded light interaction. For best results, ensure tangents are caculated or retained during export from 3D modelling tools.");

        var vertexCount = positions.Count;

        // https://github.com/TexTools/xivModdingFramework/blob/master/xivModdingFramework/Models/Helpers/ModelModifiers.cs#L1569
        // https://gamedev.stackexchange.com/a/68617
        // https://marti.works/posts/post-calculating-tangents-for-your-mesh/post/
        var tangents   = new Vector3[vertexCount];
        var bitangents = new Vector3[vertexCount];

        // Iterate over triangles, calculating tangents relative to the UVs.
        for (var index = 0; index < indices.Length; index += 3)
        {
            // Collect information for this triangle.
            var vertexIndex1 = indices[index];
            var vertexIndex2 = indices[index + 1];
            var vertexIndex3 = indices[index + 2];

            var position1 = positions[vertexIndex1];
            var position2 = positions[vertexIndex2];
            var position3 = positions[vertexIndex3];

            var texCoord1 = uvs[vertexIndex1];
            var texCoord2 = uvs[vertexIndex2];
            var texCoord3 = uvs[vertexIndex3];

            // Calculate deltas for the position XYZ, and texcoord UV.
            var edge1 = position2 - position1;
            var edge2 = position3 - position1;

            var uv1 = texCoord2 - texCoord1;
            var uv2 = texCoord3 - texCoord1;

            // Solve.
            var r = 1.0f / (uv1.X * uv2.Y - uv1.Y * uv2.X);
            var tangent = new Vector3(
                (edge1.X * uv2.Y - edge2.X * uv1.Y) * r,
                (edge1.Y * uv2.Y - edge2.Y * uv1.Y) * r,
                (edge1.Z * uv2.Y - edge2.Z * uv1.Y) * r
            );
            var bitangent = new Vector3(
                (edge1.X * uv2.X - edge2.X * uv1.X) * r,
                (edge1.Y * uv2.X - edge2.Y * uv1.X) * r,
                (edge1.Z * uv2.X - edge2.Z * uv1.X) * r
            );

            // Update vertex values.
            tangents[vertexIndex1] += tangent;
            tangents[vertexIndex2] += tangent;
            tangents[vertexIndex3] += tangent;

            bitangents[vertexIndex1] += bitangent;
            bitangents[vertexIndex2] += bitangent;
            bitangents[vertexIndex3] += bitangent;
        }

        // All the triangles have been calculated, normalise the results for each vertex.
        var result = new Vector4[vertexCount];
        for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            var n = normals[vertexIndex];
            var t = tangents[vertexIndex];
            var b = bitangents[vertexIndex];

            // Gram-Schmidt orthogonalize and calculate handedness.
            var tangent    = Vector3.Normalize(t - n * Vector3.Dot(n, t));
            var handedness = Vector3.Dot(Vector3.Cross(t, b), n) > 0 ? 1 : -1;

            result[vertexIndex] = new Vector4(tangent, handedness);
        }

        return result;
    }

    public static VertexAttribute Color(Accessors accessors)
    {
        // Try to retrieve the custom color attribute we use for export, falling back to the glTF standard name.
        if (!accessors.TryGetValue("_FFXIV_COLOR", out var accessor))
            accessors.TryGetValue("COLOR_0", out accessor);

        var element = new MdlStructs.VertexElement()
        {
            Stream = 1,
            Type   = (byte)MdlFile.VertexType.NByte4,
            Usage  = (byte)MdlFile.VertexUsage.Color,
        };

        // Some shaders rely on the presence of vertex colors to render - fall back to a pure white value if it's missing.
        var values = accessor?.AsVector4Array();

        return new VertexAttribute(
            element,
            index => BuildNByte4(values?[index] ?? Vector4.One)
        );
    }

    private static byte[] BuildSingle2(Vector2 input)
        =>
        [
            ..BitConverter.GetBytes(input.X),
            ..BitConverter.GetBytes(input.Y),
        ];

    private static byte[] BuildSingle3(Vector3 input)
        =>
        [
            ..BitConverter.GetBytes(input.X),
            ..BitConverter.GetBytes(input.Y),
            ..BitConverter.GetBytes(input.Z),
        ];

    private static byte[] BuildSingle4(Vector4 input)
        =>
        [
            ..BitConverter.GetBytes(input.X),
            ..BitConverter.GetBytes(input.Y),
            ..BitConverter.GetBytes(input.Z),
            ..BitConverter.GetBytes(input.W),
        ];

    private static byte[] BuildUByte4(Vector4 input)
        =>
        [
            (byte)input.X,
            (byte)input.Y,
            (byte)input.Z,
            (byte)input.W,
        ];

    private static byte[] BuildNByte4(Vector4 input)
        =>
        [
            (byte)Math.Round(input.X * 255f),
            (byte)Math.Round(input.Y * 255f),
            (byte)Math.Round(input.Z * 255f),
            (byte)Math.Round(input.W * 255f),
        ];
}
