using System.Text.Json;
using Lumina.Data.Parsing;
using OtterGui;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models.Import;

public class SubMeshImporter
{
    public struct SubMesh
    {
        public MdlStructs.SubmeshStruct SubMeshStruct;

        public string? Material;

        public MdlStructs.VertexDeclarationStruct VertexDeclaration;

        public ushort       VertexCount;
        public byte[]       Strides;
        public List<byte>[] Streams;

        public List<ushort> Indices;

        public BoundingBox BoundingBox;

        public string[] MetaAttributes;

        public Dictionary<string, List<MdlStructs.ShapeValueStruct>> ShapeValues;
    }

    public static SubMesh Import(Node node, IDictionary<ushort, ushort>? nodeBoneMap, IoNotifier notifier)
    {
        var importer = new SubMeshImporter(node, nodeBoneMap, notifier);
        return importer.Create();
    }

    private readonly IoNotifier _notifier;

    private readonly Node                         _node;
    private readonly IDictionary<ushort, ushort>? _nodeBoneMap;

    private string? _material;

    private          MdlStructs.VertexDeclarationStruct? _vertexDeclaration;
    private          ushort                              _vertexCount;
    private          byte[]?                             _strides;
    private readonly List<byte>[]                        _streams = [[], [], []];

    private readonly List<ushort> _indices = [];

    private readonly BoundingBox _boundingBox = new();

    private readonly List<string>?                                         _morphNames;
    private readonly Dictionary<string, List<MdlStructs.ShapeValueStruct>> _shapeValues = [];

    private SubMeshImporter(Node node, IDictionary<ushort, ushort>? nodeBoneMap, IoNotifier notifier)
    {
        _notifier    = notifier;
        _node        = node;
        _nodeBoneMap = nodeBoneMap;

        try
        {
            _morphNames = node.Mesh.Extras["targetNames"].Deserialize<List<string>>();
        }
        catch
        {
            _morphNames = null;
        }
    }

    private SubMesh Create()
    {
        // Build all the data we'll need.
        foreach (var (primitive, index) in _node.Mesh.Primitives.WithIndex())
            BuildPrimitive(primitive, index);

        ArgumentNullException.ThrowIfNull(_indices);
        ArgumentNullException.ThrowIfNull(_vertexDeclaration);
        ArgumentNullException.ThrowIfNull(_strides);
        ArgumentNullException.ThrowIfNull(_shapeValues);

        var metaAttributes = BuildMetaAttributes();

        // At this level, we assume that attributes are wholly controlled by this sub-mesh.
        var attributeMask = metaAttributes.Length switch
        {
            < 32 => (1u << metaAttributes.Length) - 1,
            32   => uint.MaxValue,
            > 32 => throw _notifier.Exception("Models may utilise a maximum of 32 attributes."),
        };

        return new SubMesh()
        {
            SubMeshStruct = new MdlStructs.SubmeshStruct()
            {
                IndexOffset        = 0,
                IndexCount         = (uint)_indices.Count,
                AttributeIndexMask = attributeMask,

                // TODO: Flesh these out. Game doesn't seem to rely on them existing, though.
                BoneStartIndex = 0,
                BoneCount      = 0,
            },
            Material          = _material,
            VertexDeclaration = _vertexDeclaration.Value,
            VertexCount       = _vertexCount,
            Strides           = _strides,
            Streams           = _streams,
            Indices           = _indices,
            BoundingBox       = _boundingBox,
            MetaAttributes    = metaAttributes,
            ShapeValues       = _shapeValues,
        };
    }

    private void BuildPrimitive(MeshPrimitive meshPrimitive, int index)
    {
        var vertexOffset = _vertexCount;
        var indexOffset  = _indices.Count;

        var primitive = PrimitiveImporter.Import(meshPrimitive, _nodeBoneMap, _notifier.WithContext($"Primitive {index}"));

        // Material
        _material ??= primitive.Material;
        if (primitive.Material != null && _material != primitive.Material)
            _notifier.Warning($"Meshes may only reference one material. Primitive {index} material \"{primitive.Material}\" has been ignored.");

        // Vertex metadata
        if (_vertexDeclaration == null)
            _vertexDeclaration = primitive.VertexDeclaration;
        else
            Utility.EnsureVertexDeclarationMatch(_vertexDeclaration.Value, primitive.VertexDeclaration, _notifier);

        _strides ??= primitive.Strides;

        // Vertices
        _vertexCount += primitive.VertexCount;

        foreach (var (stream, primitiveStream) in _streams.Zip(primitive.Streams))
            stream.AddRange(primitiveStream);

        // Indices
        _indices.AddRange(primitive.Indices.Select(i => (ushort)(i + vertexOffset)));

        // Shape values
        foreach (var (primitiveShapeValues, morphIndex) in primitive.ShapeValues.WithIndex())
        {
            // Per glTF spec, all primitives MUST have the same number of morph targets in the same order.
            // As such, this lookup should be safe - a failure here is a broken glTF file.
            var name = _morphNames != null ? _morphNames[morphIndex] : $"unnamed_shape_{morphIndex}";

            if (!_shapeValues.TryGetValue(name, out var subMeshShapeValues))
            {
                subMeshShapeValues = [];
                _shapeValues.Add(name, subMeshShapeValues);
            }

            subMeshShapeValues.AddRange(primitiveShapeValues.Select(value => value with
            {
                BaseIndicesIndex = (ushort)(value.BaseIndicesIndex + indexOffset),
                ReplacingVertexIndex = (ushort)(value.ReplacingVertexIndex + vertexOffset),
            }));
        }

        // Bounds
        _boundingBox.Merge(primitive.BoundingBox);
    }

    private string[] BuildMetaAttributes()
    {
        Dictionary<string, JsonElement>? nodeExtras;
        try
        {
            nodeExtras = _node.Extras.Deserialize<Dictionary<string, JsonElement>>();
        }
        catch
        {
            nodeExtras = null;
        }

        // We consider any "extras" key with a boolean value set to `true` to be an attribute.
        return nodeExtras?
                .Where(pair => pair.Value.ValueKind == JsonValueKind.True)
                .Select(pair => pair.Key)
                .ToArray()
         ?? [];
    }
}
