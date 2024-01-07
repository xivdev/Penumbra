using Lumina.Data.Parsing;
using OtterGui;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models.Import;

public class SubMeshImporter
{
    public struct SubMesh
    {
        public MdlStructs.SubmeshStruct SubMeshStruct;

        public MdlStructs.VertexDeclarationStruct VertexDeclaration;

        public ushort       VertexCount;
        public byte[]       Strides;
        public List<byte>[] Streams;

        public ushort[] Indices;

        public Dictionary<string, List<MdlStructs.ShapeValueStruct>> ShapeValues;
    }

    public static SubMesh Import(Node node, IDictionary<ushort, ushort>? nodeBoneMap)
    {
        var importer = new SubMeshImporter(node, nodeBoneMap);
        return importer.Create();
    }

    private readonly MeshPrimitive                _primitive;
    private readonly IDictionary<ushort, ushort>? _nodeBoneMap;

    private List<VertexAttribute>? _attributes;

    private          ushort       _vertexCount;
    private          byte[]       _strides = [0, 0, 0];
    private readonly List<byte>[] _streams;

    private ushort[]? _indices;

    private readonly List<string>?                                          _morphNames;
    private          Dictionary<string, List<MdlStructs.ShapeValueStruct>>? _shapeValues;

    private SubMeshImporter(Node node, IDictionary<ushort, ushort>? nodeBoneMap)
    {
        var mesh = node.Mesh;

        var primitiveCount = mesh.Primitives.Count;
        if (primitiveCount != 1)
        {
            var name = node.Name ?? mesh.Name ?? "(no name)";
            throw new Exception($"Mesh \"{name}\" has {primitiveCount} primitives, expected 1.");
        }

        _primitive   = mesh.Primitives[0];
        _nodeBoneMap = nodeBoneMap;

        try
        {
            _morphNames = mesh.Extras.GetNode("targetNames").Deserialize<List<string>>();
        }
        catch
        {
            _morphNames = null;
        }

        // All meshes may use up to 3 byte streams.
        _streams = new List<byte>[3];
        for (var streamIndex = 0; streamIndex < 3; streamIndex++)
            _streams[streamIndex] = [];
    }

    private SubMesh Create()
    {
        // Build all the data we'll need.
        BuildIndices();
        BuildAttributes();
        BuildVertices();

        ArgumentNullException.ThrowIfNull(_indices);
        ArgumentNullException.ThrowIfNull(_attributes);
        ArgumentNullException.ThrowIfNull(_shapeValues);

        return new SubMesh()
        {
            SubMeshStruct = new MdlStructs.SubmeshStruct()
            {
                IndexOffset        = 0,
                IndexCount         = (uint)_indices.Length,
                AttributeIndexMask = 0,

                // TODO: Flesh these out. Game doesn't seem to rely on them existing, though.
                BoneStartIndex = 0,
                BoneCount      = 0,
            },
            VertexDeclaration = new MdlStructs.VertexDeclarationStruct()
            {
                VertexElements = _attributes.Select(attribute => attribute.Element).ToArray(),
            },
            VertexCount = _vertexCount,
            Strides     = _strides,
            Streams     = _streams,
            Indices     = _indices,
            ShapeValues = _shapeValues,
        };
    }

    private void BuildIndices()
    {
        _indices = _primitive.GetIndices().Select(idx => (ushort)idx).ToArray();
    }

    private void BuildAttributes()
    {
        var accessors = _primitive.VertexAccessors;

        var morphAccessors = Enumerable.Range(0, _primitive.MorphTargetsCount)
            .Select(index => _primitive.GetMorphTargetAccessors(index)).ToList();

        // Try to build all the attributes the mesh might use.
        // The order here is chosen to match a typical model's element order.
        var rawAttributes = new[]
        {
            VertexAttribute.Position(accessors, morphAccessors),
            VertexAttribute.BlendWeight(accessors),
            VertexAttribute.BlendIndex(accessors, _nodeBoneMap),
            VertexAttribute.Normal(accessors, morphAccessors),
            VertexAttribute.Tangent1(accessors, morphAccessors),
            VertexAttribute.Color(accessors),
            VertexAttribute.Uv(accessors),
        };

        var attributes = new List<VertexAttribute>();
        var offsets = new byte[]
        {
            0,
            0,
            0,
        };
        foreach (var attribute in rawAttributes)
        {
            if (attribute == null)
                continue;

            attributes.Add(attribute.WithOffset(offsets[attribute.Stream]));
            offsets[attribute.Stream] += attribute.Size;
        }

        _attributes = attributes;
        // After building the attributes, the resulting next offsets are our stream strides.
        _strides = offsets;
    }

    private void BuildVertices()
    {
        ArgumentNullException.ThrowIfNull(_attributes);

        // Lists of vertex indices that are effected by each morph target for this primitive.
        var morphModifiedVertices = Enumerable.Range(0, _primitive.MorphTargetsCount)
            .Select(_ => new List<int>())
            .ToArray();

        // We can safely assume that POSITION exists by this point - and if, by some bizarre chance, it doesn't, failing out is sane.
        _vertexCount = (ushort)_primitive.VertexAccessors["POSITION"].Count;

        for (var vertexIndex = 0; vertexIndex < _vertexCount; vertexIndex++)
        {
            // Write out vertex data to streams for each attribute.
            foreach (var attribute in _attributes)
                _streams[attribute.Stream].AddRange(attribute.Build(vertexIndex));

            // Record which morph targets have values for this vertex, if any.
            var changedMorphs = morphModifiedVertices
                .WithIndex()
                .Where(pair => _attributes.Any(attribute => attribute.HasMorph(pair.Index, vertexIndex)))
                .Select(pair => pair.Value);
            foreach (var modifiedVertices in changedMorphs)
                modifiedVertices.Add(vertexIndex);
        }

        BuildShapeValues(morphModifiedVertices);
    }

    private void BuildShapeValues(IEnumerable<List<int>> morphModifiedVertices)
    {
        ArgumentNullException.ThrowIfNull(_indices);
        ArgumentNullException.ThrowIfNull(_attributes);

        var morphShapeValues = new Dictionary<string, List<MdlStructs.ShapeValueStruct>>();

        foreach (var (modifiedVertices, morphIndex) in morphModifiedVertices.WithIndex())
        {
            // Each for a given mesh, each shape key contains a list of shape value mappings.
            var shapeValues = new List<MdlStructs.ShapeValueStruct>();

            foreach (var vertexIndex in modifiedVertices)
            {
                // Write out the morphed vertex to the vertex streams.
                foreach (var attribute in _attributes)
                    _streams[attribute.Stream].AddRange(attribute.BuildMorph(morphIndex, vertexIndex));

                // Find any indices that target this vertex index and create a mapping.
                var targetingIndices = _indices.WithIndex()
                    .SelectWhere(pair => (pair.Value == vertexIndex, pair.Index));
                shapeValues.AddRange(targetingIndices.Select(targetingIndex => new MdlStructs.ShapeValueStruct
                {
                    BaseIndicesIndex     = (ushort)targetingIndex,
                    ReplacingVertexIndex = _vertexCount,
                }));

                _vertexCount++;
            }

            var name = _morphNames != null ? _morphNames[morphIndex] : $"unnamed_shape_{morphIndex}";
            morphShapeValues.Add(name, shapeValues);
        }

        _shapeValues = morphShapeValues;
    }
}
