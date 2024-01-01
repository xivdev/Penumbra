using System.Collections.Immutable;
using Lumina.Data.Parsing;
using Lumina.Extensions;
using Penumbra.GameData.Files;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.IO;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;

namespace Penumbra.Import.Models.Export;

public class MeshExporter
{
    public class Mesh
    {
        private IMeshBuilder<MaterialBuilder>[] _meshes;
        private NodeBuilder[]? _joints;

        public Mesh(IMeshBuilder<MaterialBuilder>[] meshes, NodeBuilder[]? joints)
        {
            _meshes = meshes;
            _joints = joints;
        }

        public void AddToScene(SceneBuilder scene)
        {
            // TODO: throw if mesh has skinned vertices but no joints are available?
            foreach (var mesh in _meshes)
                if (_joints == null)
                    scene.AddRigidMesh(mesh, Matrix4x4.Identity);
                else
                    scene.AddSkinnedMesh(mesh, Matrix4x4.Identity, _joints);
        }
    }

    public static Mesh Export(MdlFile mdl, byte lod, ushort meshIndex, GltfSkeleton? skeleton)
    {
        var self = new MeshExporter(mdl, lod, meshIndex, skeleton?.Names);
        return new Mesh(self.BuildMeshes(), skeleton?.Joints);
    }

    private const byte MaximumMeshBufferStreams = 3;

    private readonly MdlFile _mdl;
    private readonly byte _lod;
    private readonly ushort _meshIndex;
    private MdlStructs.MeshStruct XivMesh => _mdl.Meshes[_meshIndex];

    private readonly Dictionary<ushort, int>? _boneIndexMap;

    private readonly Type _geometryType;
    private readonly Type _materialType;
    private readonly Type _skinningType;

    private MeshExporter(MdlFile mdl, byte lod, ushort meshIndex, Dictionary<string, int>? boneNameMap)
    {
        _mdl = mdl;
        _lod = lod;
        _meshIndex = meshIndex;

        if (boneNameMap != null)
            _boneIndexMap = BuildBoneIndexMap(boneNameMap);

        var usages = _mdl.VertexDeclarations[_meshIndex].VertexElements
            .Select(element => (MdlFile.VertexUsage)element.Usage)
            .ToImmutableHashSet();

        _geometryType = GetGeometryType(usages);
        _materialType = GetMaterialType(usages);
        _skinningType = GetSkinningType(usages);

        // If there's skinning usages but no bone mapping, there's probably something wrong with the data.
        if (_skinningType != typeof(VertexEmpty) && _boneIndexMap == null)
            Penumbra.Log.Warning($"Mesh {meshIndex} has skinned vertex usages but no bone information was provided.");
    }

    private Dictionary<ushort, int>? BuildBoneIndexMap(Dictionary<string, int> boneNameMap)
    {
        // A BoneTableIndex of 255 means that this mesh is not skinned.
        if (XivMesh.BoneTableIndex == 255)
            return null;

        var xivBoneTable = _mdl.BoneTables[XivMesh.BoneTableIndex];

        var indexMap = new Dictionary<ushort, int>();

        foreach (var xivBoneIndex in xivBoneTable.BoneIndex.Take(xivBoneTable.BoneCount))
        {
            var boneName = _mdl.Bones[xivBoneIndex];
            if (!boneNameMap.TryGetValue(boneName, out var gltfBoneIndex))
                throw new Exception($"Armature does not contain bone \"{boneName}\" requested by mesh {_meshIndex}.");

            indexMap.Add(xivBoneIndex, gltfBoneIndex);
        }

        return indexMap;
    }

    private IMeshBuilder<MaterialBuilder>[] BuildMeshes()
    {
        var indices = BuildIndices();
        var vertices = BuildVertices();

        // NOTE: Index indices are specified relative to the LOD's 0, but we're reading chunks for each mesh, so we're specifying the index base relative to the mesh's base.

        if (XivMesh.SubMeshCount == 0)
            return [BuildMesh(indices, vertices, 0, (int)XivMesh.IndexCount)];

        return _mdl.SubMeshes
            .Skip(XivMesh.SubMeshIndex)
            .Take(XivMesh.SubMeshCount)
            .Select(submesh => BuildMesh(indices, vertices, (int)(submesh.IndexOffset - XivMesh.StartIndex), (int)submesh.IndexCount))
            .ToArray();
    }

    private IMeshBuilder<MaterialBuilder> BuildMesh(
        IReadOnlyList<ushort> indices,
        IReadOnlyList<IVertexBuilder> vertices,
        int indexBase,
        int indexCount
    )
    {
        var meshBuilderType = typeof(MeshBuilder<,,,>).MakeGenericType(
            typeof(MaterialBuilder),
            _geometryType,
            _materialType,
            _skinningType
        );
        var meshBuilder = (IMeshBuilder<MaterialBuilder>)Activator.CreateInstance(meshBuilderType, $"mesh{_meshIndex}")!;

        // TODO: share materials &c
        var materialBuilder = new MaterialBuilder()
            .WithDoubleSide(true)
            .WithMetallicRoughnessShader()
            .WithChannelParam(KnownChannel.BaseColor, KnownProperty.RGBA, new Vector4(1, 1, 1, 1));

        var primitiveBuilder = meshBuilder.UsePrimitive(materialBuilder);

        // Store a list of the glTF indices. The list index will be equivalent to the xiv (submesh) index.
        var gltfIndices = new List<int>();

        // All XIV meshes use triangle lists.
        for (var indexOffset = 0; indexOffset < indexCount; indexOffset += 3)
        {
            var (a, b, c) = primitiveBuilder.AddTriangle(
                vertices[indices[indexBase + indexOffset + 0]],
                vertices[indices[indexBase + indexOffset + 1]],
                vertices[indices[indexBase + indexOffset + 2]]
            );
            gltfIndices.AddRange([a, b, c]);
        }

        var primitiveVertices = meshBuilder.Primitives.First().Vertices;
        var shapeNames = new List<string>();

        foreach (var shape in _mdl.Shapes)
        {
            // Filter down to shape values for the current mesh that sit within the bounds of the current submesh.
            var shapeValues = _mdl.ShapeMeshes
                .Skip(shape.ShapeMeshStartIndex[_lod])
                .Take(shape.ShapeMeshCount[_lod])
                .Where(shapeMesh => shapeMesh.MeshIndexOffset == XivMesh.StartIndex)
                .SelectMany(shapeMesh =>
                    _mdl.ShapeValues
                        .Skip((int)shapeMesh.ShapeValueOffset)
                        .Take((int)shapeMesh.ShapeValueCount)
                )
                .Where(shapeValue =>
                    shapeValue.BaseIndicesIndex >= indexBase
                    && shapeValue.BaseIndicesIndex < indexBase + indexCount
                )
                .ToList();

            if (shapeValues.Count == 0) continue;

            var morphBuilder = meshBuilder.UseMorphTarget(shapeNames.Count);
            shapeNames.Add(shape.ShapeName);

            foreach (var shapeValue in shapeValues)
                morphBuilder.SetVertex(
                    primitiveVertices[gltfIndices[shapeValue.BaseIndicesIndex - indexBase]].GetGeometry(),
                    vertices[shapeValue.ReplacingVertexIndex].GetGeometry()
                );
        }

        meshBuilder.Extras = JsonContent.CreateFrom(new Dictionary<string, object>() {
            {"targetNames", shapeNames}
        });

        return meshBuilder;
    }

    private IReadOnlyList<ushort> BuildIndices()
    {
        var reader = new BinaryReader(new MemoryStream(_mdl.RemainingData));
        reader.Seek(_mdl.IndexOffset[_lod] + XivMesh.StartIndex * sizeof(ushort));
        return reader.ReadStructuresAsArray<ushort>((int)XivMesh.IndexCount);
    }

    private IReadOnlyList<IVertexBuilder> BuildVertices()
    {
        var vertexBuilderType = typeof(VertexBuilder<,,>)
            .MakeGenericType(_geometryType, _materialType, _skinningType);

        // NOTE: This assumes that buffer streams are tightly packed, which has proven safe across tested files. If this assumption is broken, seeks will need to be moved into the vertex element loop.
        var streams = new BinaryReader[MaximumMeshBufferStreams];
        for (var streamIndex = 0; streamIndex < MaximumMeshBufferStreams; streamIndex++)
        {
            streams[streamIndex] = new BinaryReader(new MemoryStream(_mdl.RemainingData));
            streams[streamIndex].Seek(_mdl.VertexOffset[_lod] + XivMesh.VertexBufferOffset[streamIndex]);
        }

        var sortedElements = _mdl.VertexDeclarations[_meshIndex].VertexElements
            .OrderBy(element => element.Offset)
            .Select(element => ((MdlFile.VertexUsage)element.Usage, element))
            .ToList();

        var vertices = new List<IVertexBuilder>();

        var attributes = new Dictionary<MdlFile.VertexUsage, object>();
        for (var vertexIndex = 0; vertexIndex < XivMesh.VertexCount; vertexIndex++)
        {
            attributes.Clear();

            foreach (var (usage, element) in sortedElements)
                attributes[usage] = ReadVertexAttribute(streams[element.Stream], element);

            var vertexGeometry = BuildVertexGeometry(attributes);
            var vertexMaterial = BuildVertexMaterial(attributes);
            var vertexSkinning = BuildVertexSkinning(attributes);

            var vertexBuilder = (IVertexBuilder)Activator.CreateInstance(vertexBuilderType, vertexGeometry, vertexMaterial, vertexSkinning)!;
            vertices.Add(vertexBuilder);
        }

        return vertices;
    }

    private object ReadVertexAttribute(BinaryReader reader, MdlStructs.VertexElement element)
    {
        return (MdlFile.VertexType)element.Type switch
        {
            MdlFile.VertexType.Single3 => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            MdlFile.VertexType.Single4 => new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            MdlFile.VertexType.UInt => reader.ReadBytes(4),
            MdlFile.VertexType.ByteFloat4 => new Vector4(reader.ReadByte() / 255f, reader.ReadByte() / 255f, reader.ReadByte() / 255f, reader.ReadByte() / 255f),
            MdlFile.VertexType.Half2 => new Vector2((float)reader.ReadHalf(), (float)reader.ReadHalf()),
            MdlFile.VertexType.Half4 => new Vector4((float)reader.ReadHalf(), (float)reader.ReadHalf(), (float)reader.ReadHalf(), (float)reader.ReadHalf()),

            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private Type GetGeometryType(IReadOnlySet<MdlFile.VertexUsage> usages)
    {
        if (!usages.Contains(MdlFile.VertexUsage.Position))
            throw new Exception("Mesh does not contain position vertex elements.");

        if (!usages.Contains(MdlFile.VertexUsage.Normal))
            return typeof(VertexPosition);

        if (!usages.Contains(MdlFile.VertexUsage.Tangent1))
            return typeof(VertexPositionNormal);

        return typeof(VertexPositionNormalTangent);
    }

    private IVertexGeometry BuildVertexGeometry(IReadOnlyDictionary<MdlFile.VertexUsage, object> attributes)
    {
        if (_geometryType == typeof(VertexPosition))
            return new VertexPosition(
                ToVector3(attributes[MdlFile.VertexUsage.Position])
            );

        if (_geometryType == typeof(VertexPositionNormal))
            return new VertexPositionNormal(
                ToVector3(attributes[MdlFile.VertexUsage.Position]),
                ToVector3(attributes[MdlFile.VertexUsage.Normal])
            );

        if (_geometryType == typeof(VertexPositionNormalTangent))
            return new VertexPositionNormalTangent(
                ToVector3(attributes[MdlFile.VertexUsage.Position]),
                ToVector3(attributes[MdlFile.VertexUsage.Normal]),
                FixTangentVector(ToVector4(attributes[MdlFile.VertexUsage.Tangent1]))
            );

        throw new Exception($"Unknown geometry type {_geometryType}.");
    }

    private Type GetMaterialType(IReadOnlySet<MdlFile.VertexUsage> usages)
    {
        // TODO: IIUC, xiv's uv2 is usually represented as the second two components of a vec4 uv attribute - add support.
        var materialUsages = (
            usages.Contains(MdlFile.VertexUsage.UV),
            usages.Contains(MdlFile.VertexUsage.Color)
        );

        return materialUsages switch
        {
            (true, true) => typeof(VertexColor1Texture1),
            (true, false) => typeof(VertexTexture1),
            (false, true) => typeof(VertexColor1),
            (false, false) => typeof(VertexEmpty),
        };
    }

    private IVertexMaterial BuildVertexMaterial(IReadOnlyDictionary<MdlFile.VertexUsage, object> attributes)
    {
        if (_materialType == typeof(VertexEmpty))
            return new VertexEmpty();

        if (_materialType == typeof(VertexColor1))
            return new VertexColor1(ToVector4(attributes[MdlFile.VertexUsage.Color]));

        if (_materialType == typeof(VertexTexture1))
            return new VertexTexture1(ToVector2(attributes[MdlFile.VertexUsage.UV]));

        if (_materialType == typeof(VertexColor1Texture1))
            return new VertexColor1Texture1(
                ToVector4(attributes[MdlFile.VertexUsage.Color]),
                ToVector2(attributes[MdlFile.VertexUsage.UV])
            );

        throw new Exception($"Unknown material type {_skinningType}");
    }

    private Type GetSkinningType(IReadOnlySet<MdlFile.VertexUsage> usages)
    {
        // TODO: possibly need to check only index - weight might be missing?
        if (usages.Contains(MdlFile.VertexUsage.BlendWeights) && usages.Contains(MdlFile.VertexUsage.BlendIndices))
            return typeof(VertexJoints4);

        return typeof(VertexEmpty);
    }

    private IVertexSkinning BuildVertexSkinning(IReadOnlyDictionary<MdlFile.VertexUsage, object> attributes)
    {
        if (_skinningType == typeof(VertexEmpty))
            return new VertexEmpty();

        if (_skinningType == typeof(VertexJoints4))
        {
            // todo: this shouldn't happen... right? better approach?
            if (_boneIndexMap == null)
                throw new Exception("cannot build skinned vertex without index mapping");

            var indices = ToByteArray(attributes[MdlFile.VertexUsage.BlendIndices]);
            var weights = ToVector4(attributes[MdlFile.VertexUsage.BlendWeights]);

            // todo: if this throws on the bone index map, the mod is broken, as it contains weights for bones that do not exist.
            //       i've not seen any of these that even tt can understand
            var bindings = Enumerable.Range(0, 4)
                .Select(index => (_boneIndexMap[indices[index]], weights[index]))
                .ToArray();
            return new VertexJoints4(bindings);
        }

        throw new Exception($"Unknown skinning type {_skinningType}");
    }

    // Some tangent W values that should be -1 are stored as 0.
    private Vector4 FixTangentVector(Vector4 tangent)
        => tangent with { W = tangent.W == 1 ? 1 : -1 };

    private Vector2 ToVector2(object data)
        => data switch
        {
            Vector2 v2 => v2,
            Vector3 v3 => new Vector2(v3.X, v3.Y),
            Vector4 v4 => new Vector2(v4.X, v4.Y),
            _ => throw new ArgumentOutOfRangeException($"Invalid Vector2 input {data}")
        };

    private Vector3 ToVector3(object data)
        => data switch
        {
            Vector2 v2 => new Vector3(v2.X, v2.Y, 0),
            Vector3 v3 => v3,
            Vector4 v4 => new Vector3(v4.X, v4.Y, v4.Z),
            _ => throw new ArgumentOutOfRangeException($"Invalid Vector3 input {data}")
        };

    private Vector4 ToVector4(object data)
        => data switch
        {
            Vector2 v2 => new Vector4(v2.X, v2.Y, 0, 0),
            Vector3 v3 => new Vector4(v3.X, v3.Y, v3.Z, 1),
            Vector4 v4 => v4,
            _ => throw new ArgumentOutOfRangeException($"Invalid Vector3 input {data}")
        };

    private byte[] ToByteArray(object data)
        => data switch
        {
            byte[] value => value,
            _ => throw new ArgumentOutOfRangeException($"Invalid byte[] input {data}")
        };
}
