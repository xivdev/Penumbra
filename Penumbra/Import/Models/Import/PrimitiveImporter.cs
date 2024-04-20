using Lumina.Data.Parsing;
using OtterGui;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models.Import;

public class PrimitiveImporter
{
    public struct Primitive
    {
        public string? Material;

        public MdlStructs.VertexDeclarationStruct VertexDeclaration;

        public ushort       VertexCount;
        public byte[]       Strides;
        public List<byte>[] Streams;

        public ushort[] Indices;

        public BoundingBox BoundingBox;

        public List<List<MdlStructs.ShapeValueStruct>> ShapeValues;
    }

    public static Primitive Import(MeshPrimitive primitive, IDictionary<ushort, ushort>? nodeBoneMap, IoNotifier notifier)
    {
        var importer = new PrimitiveImporter(primitive, nodeBoneMap, notifier);
        return importer.Create();
    }

    private readonly IoNotifier _notifier;

    private readonly MeshPrimitive                _primitive;
    private readonly IDictionary<ushort, ushort>? _nodeBoneMap;

    private ushort[]? _indices;

    private List<VertexAttribute>? _vertexAttributes;

    private          ushort       _vertexCount;
    private          byte[]       _strides = [0, 0, 0];
    private readonly List<byte>[] _streams = [[], [], []];

    private readonly BoundingBox _boundingBox = new();

    private List<List<MdlStructs.ShapeValueStruct>>? _shapeValues;

    private PrimitiveImporter(MeshPrimitive primitive, IDictionary<ushort, ushort>? nodeBoneMap, IoNotifier notifier)
    {
        _notifier    = notifier;
        _primitive   = primitive;
        _nodeBoneMap = nodeBoneMap;
    }

    private Primitive Create()
    {
        // TODO: This structure is verging on a little silly. Reconsider.
        BuildIndices();
        BuildVertexAttributes();
        BuildVertices();
        BuildBoundingBox();

        ArgumentNullException.ThrowIfNull(_vertexAttributes);
        ArgumentNullException.ThrowIfNull(_indices);
        ArgumentNullException.ThrowIfNull(_shapeValues);

        var material = _primitive.Material?.Name;
        if (material == "")
            material = null;

        return new Primitive
        {
            Material = material,
            VertexDeclaration = new MdlStructs.VertexDeclarationStruct
            {
                VertexElements = _vertexAttributes.Select(attribute => attribute.Element).ToArray(),
            },
            VertexCount = _vertexCount,
            Strides     = _strides,
            Streams     = _streams,
            Indices     = _indices,
            BoundingBox = _boundingBox,
            ShapeValues = _shapeValues,
        };
    }

    private void BuildIndices()
    {
        // TODO: glTF supports a bunch of primitive types, ref. Schema2.PrimitiveType. All this code is currently assuming that it's using plain triangles (4). It should probably be generalised to other formats - I _suspect_ we should be able to get away with evaluating the indices to triangles with GetTriangleIndices, but will need investigation.
        _indices = _primitive.GetIndices().Select(idx => (ushort)idx).ToArray();
    }

    private void BuildVertexAttributes()
    {
        // Tangent calculation requires indices if missing.
        ArgumentNullException.ThrowIfNull(_indices);

        var accessors = _primitive.VertexAccessors;

        var morphAccessors = Enumerable.Range(0, _primitive.MorphTargetsCount)
            .Select(index => _primitive.GetMorphTargetAccessors(index)).ToList();

        // Try to build all the attributes the mesh might use.
        // The order here is chosen to match a typical model's element order.
        var rawAttributes = new[]
        {
            VertexAttribute.Position(accessors, morphAccessors, _notifier),
            VertexAttribute.BlendWeight(accessors, _notifier),
            VertexAttribute.BlendIndex(accessors, _nodeBoneMap, _notifier),
            VertexAttribute.Normal(accessors, morphAccessors),
            VertexAttribute.Tangent1(accessors, morphAccessors, _indices, _notifier),
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

        _vertexAttributes = attributes;
        // After building the attributes, the resulting next offsets are our stream strides.
        _strides = offsets;
    }

    private void BuildVertices()
    {
        ArgumentNullException.ThrowIfNull(_vertexAttributes);

        // Lists of vertex indices that are effected by each morph target for this primitive.
        var morphModifiedVertices = Enumerable.Range(0, _primitive.MorphTargetsCount)
            .Select(_ => new List<int>())
            .ToArray();

        // We can safely assume that POSITION exists by this point - and if, by some bizarre chance, it doesn't, failing out is sane.
        _vertexCount = (ushort)_primitive.VertexAccessors["POSITION"].Count;

        for (var vertexIndex = 0; vertexIndex < _vertexCount; vertexIndex++)
        {
            // Write out vertex data to streams for each attribute.
            foreach (var attribute in _vertexAttributes)
                _streams[attribute.Stream].AddRange(attribute.Build(vertexIndex));

            // Record which morph targets have values for this vertex, if any.
            var index = vertexIndex;
            var changedMorphs = morphModifiedVertices
                .WithIndex()
                .Where(pair => _vertexAttributes.Any(attribute => attribute.HasMorph(pair.Index, index)))
                .Select(pair => pair.Value);
            foreach (var modifiedVertices in changedMorphs)
                modifiedVertices.Add(vertexIndex);
        }

        BuildShapeValues(morphModifiedVertices);
    }

    private void BuildShapeValues(IEnumerable<List<int>> morphModifiedVertices)
    {
        ArgumentNullException.ThrowIfNull(_indices);
        ArgumentNullException.ThrowIfNull(_vertexAttributes);

        var morphShapeValues = new List<List<MdlStructs.ShapeValueStruct>>();

        foreach (var (modifiedVertices, morphIndex) in morphModifiedVertices.WithIndex())
        {
            // For a given mesh, each shape key contains a list of shape value mappings.
            var shapeValues = new List<MdlStructs.ShapeValueStruct>();

            foreach (var vertexIndex in modifiedVertices)
            {
                // Write out the morphed vertex to the vertex streams.
                foreach (var attribute in _vertexAttributes)
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

            morphShapeValues.Add(shapeValues);
        }

        _shapeValues = morphShapeValues;
    }

    private void BuildBoundingBox()
    {
        var positions = _primitive.VertexAccessors["POSITION"].AsVector3Array();
        foreach (var position in positions)
            _boundingBox.Merge(position);
    }
}
