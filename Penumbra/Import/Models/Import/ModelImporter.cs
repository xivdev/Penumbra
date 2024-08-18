using Lumina.Data.Parsing;
using OtterGui;
using Penumbra.GameData.Files;
using Penumbra.GameData.Files.ModelStructs;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models.Import;

public partial class ModelImporter(ModelRoot model, IoNotifier notifier)
{
    public static MdlFile Import(ModelRoot model, IoNotifier notifier)
    {
        var importer = new ModelImporter(model, notifier);
        return importer.Create();
    }

    // NOTE: This is intended to match TexTool's grouping regex, ".*[_ ^]([0-9]+)[\\.\\-]?([0-9]+)?$"
    [GeneratedRegex(@"[_ ^](?'Mesh'[0-9]+)[.-]?(?'SubMesh'[0-9]+)?$",
        RegexOptions.Compiled | RegexOptions.NonBacktracking | RegexOptions.ExplicitCapture)]
    private static partial Regex MeshNameGroupingRegex();

    private readonly List<MeshStruct>               _meshes    = [];
    private readonly List<MdlStructs.SubmeshStruct> _subMeshes = [];

    private readonly List<string> _materials = [];

    private readonly List<MdlStructs.VertexDeclarationStruct> _vertexDeclarations = [];
    private readonly List<byte>                               _vertexBuffer       = [];

    private readonly List<ushort> _indices = [];

    private readonly List<string>          _bones      = [];
    private readonly List<BoneTableStruct> _boneTables = [];

    private readonly BoundingBox _boundingBox = new();

    private readonly List<string> _metaAttributes = [];

    private readonly Dictionary<string, List<MdlStructs.ShapeMeshStruct>> _shapeMeshes = [];
    private readonly List<MdlStructs.ShapeValueStruct>                    _shapeValues = [];

    private MdlFile Create()
    {
        // Group and build out meshes in this model.
        foreach (var (subMeshNodes, index) in GroupedMeshNodes().WithIndex())
            BuildMeshForGroup(subMeshNodes, index);

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
            Attributes     = [.. _metaAttributes],
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
            Materials     = [.. materials],
            BoundingBoxes = _boundingBox.ToStruct(),

            // TODO: Would be good to calculate all of this up the tree.
            Radius            = 1,
            BoneBoundingBoxes = Enumerable.Repeat(MdlFile.EmptyBoundingBox, _bones.Count).ToArray(),
            RemainingData     = [.._vertexBuffer, ..indexBuffer],
            Valid             = true,
        };
    }

    /// <summary> Returns an iterator over sorted, grouped mesh nodes. </summary>
    private IEnumerable<IEnumerable<Node>> GroupedMeshNodes()
        => model.LogicalNodes
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

    private void BuildMeshForGroup(IEnumerable<Node> subMeshNodes, int index)
    {
        // Record some offsets we'll be using later, before they get mutated with mesh values.
        var subMeshOffset = _subMeshes.Count;
        var vertexOffset  = _vertexBuffer.Count;
        var indexOffset   = _indices.Count;

        var mesh           = MeshImporter.Import(subMeshNodes, notifier.WithContext($"Mesh {index}"));
        var meshStartIndex = (uint)(mesh.MeshStruct.StartIndex + indexOffset);

        var materialIndex = mesh.Material != null
            ? GetMaterialIndex(mesh.Material)
            : (ushort)0;

        // If no bone table is used for a mesh, the index is set to 255.
        var boneTableIndex = mesh.Bones != null
            ? BuildBoneTable(mesh.Bones)
            : (ushort)255;

        _meshes.Add(mesh.MeshStruct with
        {
            MaterialIndex = materialIndex,
            SubMeshIndex = (ushort)(mesh.MeshStruct.SubMeshIndex + subMeshOffset),
            BoneTableIndex = boneTableIndex,
            StartIndex = meshStartIndex,
            VertexBufferOffset1 = (uint)(mesh.MeshStruct.VertexBufferOffset1 + vertexOffset),
            VertexBufferOffset2 = (uint)(mesh.MeshStruct.VertexBufferOffset2 + vertexOffset),
            VertexBufferOffset3 = (uint)(mesh.MeshStruct.VertexBufferOffset3 + vertexOffset),
        });

        _boundingBox.Merge(mesh.BoundingBox);

        _subMeshes.AddRange(mesh.SubMeshStructs.Select(m => m with
        {
            AttributeIndexMask = Utility.GetMergedAttributeMask(
                m.AttributeIndexMask, mesh.MetaAttributes, _metaAttributes),
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
                ShapeValueOffset = (uint)_shapeValues.Count,
            });

            _shapeValues.AddRange(meshShapeKey.ShapeValues);
        }

        // The number of shape values in a model is bounded by the count
        // value, which is stored as a u16.
        // While technically there are similar bounds on other shape struct
        // arrays, values is practically guaranteed to be the highest of the
        // group, so a failure on any of them will be a failure on it.
        if (_shapeValues.Count > ushort.MaxValue)
            throw notifier.Exception(
                $"Importing this file would require more than the maximum of {ushort.MaxValue} shape values.\nTry removing or applying shape keys that do not need to be changed at runtime in-game.");
    }

    private ushort GetMaterialIndex(string materialName)
    {
        // If we already have this material, grab the current index.
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

    // #TODO @ackwell fix for V6 Models
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
            throw notifier.Exception("XIV does not support meshes weighted to a total of more than 64 bones.");

        var boneIndicesArray = new ushort[64];
        Array.Copy(boneIndices.ToArray(), boneIndicesArray, boneIndices.Count);

        var boneTableIndex = _boneTables.Count;
        _boneTables.Add(new BoneTableStruct()
        {
            BoneIndex = boneIndicesArray,
            BoneCount = (byte)boneIndices.Count,
        });

        return (ushort)boneTableIndex;
    }
}
