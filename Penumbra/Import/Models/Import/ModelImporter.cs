using Lumina.Data.Parsing;
using Penumbra.GameData.Files;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models.Import;

public partial class ModelImporter
{
    public static MdlFile Import(ModelRoot model)
    {
        var importer = new ModelImporter(model);
        return importer.Create();
    }

    // NOTE: This is intended to match TexTool's grouping regex, ".*[_ ^]([0-9]+)[\\.\\-]?([0-9]+)?$"
    [GeneratedRegex(@"[_ ^](?'Mesh'[0-9]+)[.-]?(?'SubMesh'[0-9]+)?$", RegexOptions.Compiled)]
    private static partial Regex MeshNameGroupingRegex();

    private readonly ModelRoot _model;

    private List<MdlStructs.MeshStruct> _meshes = new();
    private List<MdlStructs.SubmeshStruct> _subMeshes = new();

    private List<MdlStructs.VertexDeclarationStruct> _vertexDeclarations = new();
    private List<byte> _vertexBuffer = new();

    private List<ushort> _indices = new();

    private List<string> _bones = new();
    private List<MdlStructs.BoneTableStruct> _boneTables = new();

    private Dictionary<string, List<MdlStructs.ShapeMeshStruct>> _shapeMeshes = new();
    private List<MdlStructs.ShapeValueStruct> _shapeValues = new();

    private ModelImporter(ModelRoot model)
    {
        _model = model;
    }

    private MdlFile Create()
    {
        // Group and build out meshes in this model.
        foreach (var subMeshNodes in GroupedMeshNodes())
            BuildMeshForGroup(subMeshNodes);

        // Now that all of the meshes have been built, we can build some of the model-wide metadata.
        var shapes = new List<MdlFile.Shape>();
        var shapeMeshes = new List<MdlStructs.ShapeMeshStruct>();
        foreach (var (keyName, keyMeshes) in _shapeMeshes)
        {
            shapes.Add(new MdlFile.Shape()
            {
                ShapeName = keyName,
                // NOTE: these values are per-LoD.
                ShapeMeshStartIndex = [(ushort)shapeMeshes.Count, 0, 0],
                ShapeMeshCount = [(ushort)keyMeshes.Count, 0, 0],
            });
            shapeMeshes.AddRange(keyMeshes);
        }

        var indexBuffer = _indices.SelectMany(BitConverter.GetBytes).ToArray();

        var emptyBoundingBox = new MdlStructs.BoundingBoxStruct()
        {
            Min = [0, 0, 0, 0],
            Max = [0, 0, 0, 0],
        };

        // And finally, the MdlFile itself.
        return new MdlFile()
        {
            VertexOffset = [0, 0, 0],
            VertexBufferSize = [(uint)_vertexBuffer.Count, 0, 0],
            IndexOffset = [(uint)_vertexBuffer.Count, 0, 0],
            IndexBufferSize = [(uint)indexBuffer.Length, 0, 0],

            VertexDeclarations = _vertexDeclarations.ToArray(),
            Meshes = _meshes.ToArray(),
            SubMeshes = _subMeshes.ToArray(),

            BoneTables = _boneTables.ToArray(),
            Bones = _bones.ToArray(),
            // TODO: Game doesn't seem to rely on this, but would be good to populate.
            SubMeshBoneMap = [],

            Shapes = shapes.ToArray(),
            ShapeMeshes = shapeMeshes.ToArray(),
            ShapeValues = _shapeValues.ToArray(),

            LodCount = 1,

            Lods = [new MdlStructs.LodStruct()
            {
                MeshIndex = 0,
                MeshCount = (ushort)_meshes.Count,

                ModelLodRange = 0,
                TextureLodRange = 0,
                
                VertexDataOffset = 0,
                VertexBufferSize = (uint)_vertexBuffer.Count,
                IndexDataOffset = (uint)_vertexBuffer.Count,
                IndexBufferSize = (uint)indexBuffer.Length,
            }],

            // TODO: Would be good to populate from gltf material names.
            Materials = ["/NO_MATERIAL"],

            // TODO: Would be good to calculate all of this up the tree.
            Radius = 1,
            BoundingBoxes = emptyBoundingBox,
            BoneBoundingBoxes = Enumerable.Repeat(emptyBoundingBox, _bones.Count).ToArray(),

            RemainingData = [.._vertexBuffer, ..indexBuffer],
        };
    }

    /// <summary> Returns an iterator over sorted, grouped mesh nodes. </summary>
    private IEnumerable<IEnumerable<Node>> GroupedMeshNodes() =>
        _model.LogicalNodes
            .Where(node => node.Mesh != null)
            .Select(node => 
            {
                var name = node.Name ?? node.Mesh.Name ?? "NOMATCH";
                var match = MeshNameGroupingRegex().Match(name);
                return (node, match);
            })
            .Where(pair => pair.match.Success)
            .OrderBy(pair =>
            {
                var subMeshGroup = pair.match.Groups["SubMesh"];
                return subMeshGroup.Success ? int.Parse(subMeshGroup.Value) : 0;
            })
            .GroupBy(
                pair => int.Parse(pair.match.Groups["Mesh"].Value),
                pair => pair.node
            )
            .OrderBy(group => group.Key);

    private void BuildMeshForGroup(IEnumerable<Node> subMeshNodes)
    {
        // Record some offsets we'll be using later, before they get mutated with mesh values.
        var subMeshOffset = _subMeshes.Count;
        var vertexOffset = _vertexBuffer.Count;
        var indexOffset = _indices.Count;
        var shapeValueOffset = _shapeValues.Count;

        var mesh = MeshImporter.Import(subMeshNodes);
        var meshStartIndex = (uint)(mesh.MeshStruct.StartIndex + indexOffset);

        // If no bone table is used for a mesh, the index is set to 255.
        var boneTableIndex = 255;
        if (mesh.Bones != null)
            boneTableIndex = BuildBoneTable(mesh.Bones);

        _meshes.Add(mesh.MeshStruct with
        {
            SubMeshIndex = (ushort)(mesh.MeshStruct.SubMeshIndex + subMeshOffset),
            BoneTableIndex = (ushort)boneTableIndex,
            StartIndex = meshStartIndex,
            VertexBufferOffset = mesh.MeshStruct.VertexBufferOffset
                .Select(offset => (uint)(offset + vertexOffset))
                .ToArray(),
        });

        foreach (var subMesh in mesh.SubMeshStructs)
            _subMeshes.Add(subMesh with
            {
                IndexOffset = (uint)(subMesh.IndexOffset + indexOffset),
            });

        _vertexDeclarations.Add(mesh.VertexDeclaration);
        _vertexBuffer.AddRange(mesh.VertexBuffer);

        _indices.AddRange(mesh.Indicies);

        foreach (var meshShapeKey in mesh.ShapeKeys)
        {
            if (!_shapeMeshes.TryGetValue(meshShapeKey.Name, out var shapeMeshes))
            {
                shapeMeshes = new();
                _shapeMeshes.Add(meshShapeKey.Name, shapeMeshes);
            }

            shapeMeshes.Add(meshShapeKey.ShapeMesh with
            {
                MeshIndexOffset = meshStartIndex,
                ShapeValueOffset = (uint)shapeValueOffset,
            });

            _shapeValues.AddRange(meshShapeKey.ShapeValues);
        }
    }

    private ushort BuildBoneTable(List<string> boneNames)
    {
        var boneIndices = new List<ushort>();
        foreach (var boneName in boneNames)
        {
            var boneIndex = _bones.IndexOf(boneName);
            if (boneIndex == -1)
            {
                boneIndex = _bones.Count;
                _bones.Add(boneName);
            }
            boneIndices.Add((ushort)boneIndex);
        }

        if (boneIndices.Count > 64)
            throw new Exception("XIV does not support meshes weighted to more than 64 bones.");

        var boneIndicesArray = new ushort[64];
        Array.Copy(boneIndices.ToArray(), boneIndicesArray, boneIndices.Count);

        var boneTableIndex = _boneTables.Count;
        _boneTables.Add(new MdlStructs.BoneTableStruct()
        {
            BoneIndex = boneIndicesArray,
            BoneCount = (byte)boneIndices.Count,
        });

        return (ushort)boneTableIndex;
    }
}
