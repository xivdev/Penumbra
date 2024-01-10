using Lumina.Data.Parsing;
using Penumbra.GameData.Files;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models.Import;

public partial class ModelImporter(ModelRoot _model)
{
    public static MdlFile Import(ModelRoot model)
    {
        var importer = new ModelImporter(model);
        return importer.Create();
    }

    // NOTE: This is intended to match TexTool's grouping regex, ".*[_ ^]([0-9]+)[\\.\\-]?([0-9]+)?$"
    [GeneratedRegex(@"[_ ^](?'Mesh'[0-9]+)[.-]?(?'SubMesh'[0-9]+)?$", RegexOptions.Compiled | RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture)]
    private static partial Regex MeshNameGroupingRegex();

    private readonly List<MdlStructs.MeshStruct>    _meshes    = [];
    private readonly List<MdlStructs.SubmeshStruct> _subMeshes = [];

    private readonly List<string> _materials = [];

    private readonly List<MdlStructs.VertexDeclarationStruct> _vertexDeclarations = [];
    private readonly List<byte>                               _vertexBuffer       = [];

    private readonly List<ushort> _indices = [];

    private readonly List<string>                     _bones      = [];
    private readonly List<MdlStructs.BoneTableStruct> _boneTables = [];

    private readonly Dictionary<string, List<MdlStructs.ShapeMeshStruct>> _shapeMeshes = [];
    private readonly List<MdlStructs.ShapeValueStruct>                    _shapeValues = [];

    private MdlFile Create()
    {
        // Group and build out meshes in this model.
        foreach (var subMeshNodes in GroupedMeshNodes())
            BuildMeshForGroup(subMeshNodes);

        // Now that all the meshes have been built, we can build some of the model-wide metadata.
        var materials = _materials.Count > 0 ? _materials : ["/NO_MATERIAL"];

        var shapes      = new List<MdlFile.Shape>();
        var shapeMeshes = new List<MdlStructs.ShapeMeshStruct>();
        foreach (var (keyName, keyMeshes) in _shapeMeshes)
        {
            shapes.Add(new MdlFile.Shape()
            {
                ShapeName = keyName,
                // NOTE: these values are per-LoD.
                ShapeMeshStartIndex = [(ushort)shapeMeshes.Count, 0, 0],
                ShapeMeshCount      = [(ushort)keyMeshes.Count, 0, 0],
            });
            shapeMeshes.AddRange(keyMeshes);
        }

        var indexBuffer = _indices.SelectMany(BitConverter.GetBytes).ToArray();

        // And finally, the MdlFile itself.
        return new MdlFile
        {
            VertexOffset       = [0, 0, 0],
            VertexBufferSize   = [(uint)_vertexBuffer.Count, 0, 0],
            IndexOffset        = [(uint)_vertexBuffer.Count, 0, 0],
            IndexBufferSize    = [(uint)indexBuffer.Length, 0, 0],
            VertexDeclarations = [.. _vertexDeclarations],
            Meshes             = [.. _meshes],
            SubMeshes          = [.. _subMeshes],
            BoneTables         = [.. _boneTables],
            Bones              = [.. _bones],
            // TODO: Game doesn't seem to rely on this, but would be good to populate.
            SubMeshBoneMap = [],
            Shapes         = [.. shapes],
            ShapeMeshes    = [.. shapeMeshes],
            ShapeValues    = [.. _shapeValues],
            LodCount       = 1,
            Lods =
            [
                new MdlStructs.LodStruct
                {
                    MeshIndex        = 0,
                    MeshCount        = (ushort)_meshes.Count,
                    ModelLodRange    = 0,
                    TextureLodRange  = 0,
                    VertexDataOffset = 0,
                    VertexBufferSize = (uint)_vertexBuffer.Count,
                    IndexDataOffset  = (uint)_vertexBuffer.Count,
                    IndexBufferSize  = (uint)indexBuffer.Length,
                },
            ],

            Materials = [.. materials],

            // TODO: Would be good to calculate all of this up the tree.
            Radius            = 1,
            BoundingBoxes     = MdlFile.EmptyBoundingBox,
            BoneBoundingBoxes = Enumerable.Repeat(MdlFile.EmptyBoundingBox, _bones.Count).ToArray(),
            RemainingData     = [.._vertexBuffer, ..indexBuffer],
        };
    }

    /// <summary> Returns an iterator over sorted, grouped mesh nodes. </summary>
    private IEnumerable<IEnumerable<Node>> GroupedMeshNodes()
        => _model.LogicalNodes
            .Where(node => node.Mesh != null)
            .Select(node =>
            {
                var name  = node.Name ?? node.Mesh.Name ?? "NOMATCH";
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
        var subMeshOffset    = _subMeshes.Count;
        var vertexOffset     = _vertexBuffer.Count;
        var indexOffset      = _indices.Count;
        var shapeValueOffset = _shapeValues.Count;

        var mesh           = MeshImporter.Import(subMeshNodes);
        var meshStartIndex = (uint)(mesh.MeshStruct.StartIndex + indexOffset);

        ushort materialIndex = 0;
        if (mesh.Material != null)
            materialIndex = GetMaterialIndex(mesh.Material);

        // If no bone table is used for a mesh, the index is set to 255.
        ushort boneTableIndex = 255;
        if (mesh.Bones != null)
            boneTableIndex = BuildBoneTable(mesh.Bones);

        _meshes.Add(mesh.MeshStruct with
        {
            MaterialIndex = materialIndex,
            SubMeshIndex = (ushort)(mesh.MeshStruct.SubMeshIndex + subMeshOffset),
            BoneTableIndex = boneTableIndex,
            StartIndex = meshStartIndex,
            VertexBufferOffset = mesh.MeshStruct.VertexBufferOffset
                .Select(offset => (uint)(offset + vertexOffset))
                .ToArray(),
        });

        _subMeshes.AddRange(mesh.SubMeshStructs.Select(m => m with
        {
            IndexOffset = (uint)(m.IndexOffset + indexOffset),
        }));

        _vertexDeclarations.Add(mesh.VertexDeclaration);
        _vertexBuffer.AddRange(mesh.VertexBuffer);

        _indices.AddRange(mesh.Indices);

        foreach (var meshShapeKey in mesh.ShapeKeys)
        {
            if (!_shapeMeshes.TryGetValue(meshShapeKey.Name, out var shapeMeshes))
            {
                shapeMeshes = [];
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

    private ushort GetMaterialIndex(string materialName)
    {
        // If we already have this material, grab the current one
        var index = _materials.IndexOf(materialName);
        if (index >= 0)
            return (ushort)index;

        // If there's already 4 materials, we can't add any more.
        // TODO: permit, with a warning to reduce, and validation in MdlTab.
        var count = _materials.Count;
        if (count >= 4)
            return 0;

        _materials.Add(materialName);
        return (ushort)count;
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
