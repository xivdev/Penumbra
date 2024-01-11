using Lumina.Data.Parsing;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models.Import;

public class MeshImporter(IEnumerable<Node> nodes)
{
    public struct Mesh
    {
        public MdlStructs.MeshStruct          MeshStruct;
        public List<MdlStructs.SubmeshStruct> SubMeshStructs;

        public string? Material;

        public MdlStructs.VertexDeclarationStruct VertexDeclaration;
        public IEnumerable<byte>                  VertexBuffer;

        public List<ushort> Indices;

        public List<string>? Bones;

        public List<string> MetaAttributes;

        public List<MeshShapeKey> ShapeKeys;
    }

    public struct MeshShapeKey
    {
        public string                            Name;
        public MdlStructs.ShapeMeshStruct        ShapeMesh;
        public List<MdlStructs.ShapeValueStruct> ShapeValues;
    }

    public static Mesh Import(IEnumerable<Node> nodes)
    {
        var importer = new MeshImporter(nodes);
        return importer.Create();
    }

    private readonly List<MdlStructs.SubmeshStruct> _subMeshes = [];

    private string? _material;

    private          MdlStructs.VertexDeclarationStruct? _vertexDeclaration;
    private          byte[]?                             _strides;
    private          ushort                              _vertexCount;
    private readonly List<byte>[]                        _streams = [[], [], []];

    private readonly List<ushort> _indices = [];

    private List<string>? _bones;

    private readonly List<string> _metaAttributes = [];

    private readonly Dictionary<string, List<MdlStructs.ShapeValueStruct>> _shapeValues = [];

    private Mesh Create()
    {
        foreach (var node in nodes)
            BuildSubMeshForNode(node);

        ArgumentNullException.ThrowIfNull(_strides);
        ArgumentNullException.ThrowIfNull(_vertexDeclaration);

        return new Mesh
        {
            MeshStruct = new MdlStructs.MeshStruct
            {
                VertexBufferOffset = [0, (uint)_streams[0].Count, (uint)(_streams[0].Count + _streams[1].Count)],
                VertexBufferStride = _strides,
                VertexCount        = _vertexCount,
                VertexStreamCount = (byte)_vertexDeclaration.Value.VertexElements
                    .Select(element => element.Stream + 1)
                    .Max(),
                StartIndex = 0,
                IndexCount = (uint)_indices.Count,

                // TODO: import material names
                MaterialIndex  = 0,
                SubMeshIndex   = 0,
                SubMeshCount   = (ushort)_subMeshes.Count,
                BoneTableIndex = 0,
            },
            SubMeshStructs    = _subMeshes,
            Material          = _material,
            VertexDeclaration = _vertexDeclaration.Value,
            VertexBuffer      = _streams[0].Concat(_streams[1]).Concat(_streams[2]),
            Indices           = _indices,
            Bones             = _bones,
            MetaAttributes    = _metaAttributes,
            ShapeKeys = _shapeValues
                .Select(pair => new MeshShapeKey()
                {
                    Name = pair.Key,
                    ShapeMesh = new MdlStructs.ShapeMeshStruct()
                    {
                        MeshIndexOffset  = 0,
                        ShapeValueOffset = 0,
                        ShapeValueCount  = (uint)pair.Value.Count,
                    },
                    ShapeValues = pair.Value,
                })
                .ToList(),
        };
    }

    private void BuildSubMeshForNode(Node node)
    {
        // Record some offsets we'll be using later, before they get mutated with sub-mesh values.
        var vertexOffset = _vertexCount;
        var indexOffset  = _indices.Count;

        var nodeBoneMap = CreateNodeBoneMap(node);
        var subMesh     = SubMeshImporter.Import(node, nodeBoneMap);

        var subMeshName = node.Name ?? node.Mesh.Name;

        // TODO: Record a warning if there's a mismatch between current and incoming, as we can't support multiple materials per mesh.
        _material ??= subMesh.Material;

        // Check that vertex declarations match - we need to combine the buffers, so a mismatch would take a whole load of resolution.
        if (_vertexDeclaration == null)
            _vertexDeclaration = subMesh.VertexDeclaration;
        else if (VertexDeclarationMismatch(subMesh.VertexDeclaration, _vertexDeclaration.Value))
            throw new Exception(
                $"Sub-mesh \"{subMeshName}\" vertex declaration mismatch. All sub-meshes of a mesh must have equivalent vertex declarations.");

        // Given that strides are derived from declarations, a lack of mismatch in declarations means the strides are fine.
        // TODO: I mean, given that strides are derivable, might be worth dropping strides from the sub mesh return structure and computing when needed.
        _strides ??= subMesh.Strides;

        // Merge the sub-mesh streams into the main mesh stream bodies.
        _vertexCount += subMesh.VertexCount;

        foreach (var (stream, subStream) in _streams.Zip(subMesh.Streams))
            stream.AddRange(subStream);

        // As we're appending vertex data to the buffers, we need to update indices to point into that later block.
        _indices.AddRange(subMesh.Indices.Select(index => (ushort)(index + vertexOffset)));

        // Merge the sub-mesh's shape values into the mesh's.
        foreach (var (name, subMeshShapeValues) in subMesh.ShapeValues)
        {
            if (!_shapeValues.TryGetValue(name, out var meshShapeValues))
            {
                meshShapeValues = [];
                _shapeValues.Add(name, meshShapeValues);
            }

            meshShapeValues.AddRange(subMeshShapeValues.Select(value => value with
            {
                BaseIndicesIndex = (ushort)(value.BaseIndicesIndex + indexOffset),
                ReplacingVertexIndex = (ushort)(value.ReplacingVertexIndex + vertexOffset),
            }));
        }

        // And finally, merge in the sub-mesh struct itself.
        _subMeshes.Add(subMesh.SubMeshStruct with
        {
            IndexOffset = (ushort)(subMesh.SubMeshStruct.IndexOffset + indexOffset),
            AttributeIndexMask = Utility.GetMergedAttributeMask(
                subMesh.SubMeshStruct.AttributeIndexMask, subMesh.MetaAttributes, _metaAttributes),
        });
    }

    private static bool VertexDeclarationMismatch(MdlStructs.VertexDeclarationStruct a, MdlStructs.VertexDeclarationStruct b)
    {
        var elA = a.VertexElements;
        var elB = b.VertexElements;

        if (elA.Length != elB.Length)
            return true;

        // NOTE: This assumes that elements will always be in the same order. Under the current implementation, that's guaranteed.
        return elA.Zip(elB).Any(pair =>
            pair.First.Usage != pair.Second.Usage
         || pair.First.Type != pair.Second.Type
         || pair.First.Offset != pair.Second.Offset
         || pair.First.Stream != pair.Second.Stream
        );
    }

    private Dictionary<ushort, ushort>? CreateNodeBoneMap(Node node)
    {
        // Unskinned assets can skip this all of this.
        if (node.Skin == null)
            return null;

        // Build an array of joint names, preserving the joint index from the skin.
        // Any unnamed joints we'll be coalescing on a fallback bone name - though this is realistically unlikely to occur.
        var jointNames = Enumerable.Range(0, node.Skin.JointsCount)
            .Select(index => node.Skin.GetJoint(index).Joint.Name ?? "unnamed_joint")
            .ToArray();

        // TODO: This is duplicated with the sub mesh importer - would be good to avoid (not that it's a huge issue).
        var mesh           = node.Mesh;
        var meshName       = node.Name ?? mesh.Name ?? "(no name)";
        var primitiveCount = mesh.Primitives.Count;
        if (primitiveCount != 1)
            throw new Exception($"Mesh \"{meshName}\" has {primitiveCount} primitives, expected 1.");

        var primitive = mesh.Primitives[0];

        // Per glTF specification, an asset with a skin MUST contain skinning attributes on its mesh.
        var jointsAccessor = primitive.GetVertexAccessor("JOINTS_0")
         ?? throw new Exception($"Skinned mesh \"{meshName}\" is skinned but does not contain skinning vertex attributes.");

        // Build a set of joints that are referenced by this mesh.
        // TODO: Would be neat to omit 0-weighted joints here, but doing so will require some further work on bone mapping behavior to ensure the unweighted joints can still be resolved to valid bone indices during vertex data construction.
        var usedJoints = new HashSet<ushort>();
        foreach (var joints in jointsAccessor.AsVector4Array())
        {
            for (var index = 0; index < 4; index++)
                usedJoints.Add((ushort)joints[index]);
        }

        // Only initialise the bones list if we're actually going to put something in it.
        _bones ??= [];

        // Build a dictionary of node-specific joint indices mesh-wide bone indices.
        var nodeBoneMap = new Dictionary<ushort, ushort>();
        foreach (var usedJoint in usedJoints)
        {
            var jointName = jointNames[usedJoint];
            var boneIndex = _bones.IndexOf(jointName);
            if (boneIndex == -1)
            {
                boneIndex = _bones.Count;
                _bones.Add(jointName);
            }

            nodeBoneMap.Add(usedJoint, (ushort)boneIndex);
        }

        return nodeBoneMap;
    }
}
