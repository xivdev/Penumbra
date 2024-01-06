using System.Collections.Immutable;
using Lumina.Data.Parsing;
using Lumina.Extensions;
using OtterGui;
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
            .ToImmutableDictionary(
                element => (MdlFile.VertexUsage)element.Usage,
                element => (MdlFile.VertexType)element.Type
            );

        _geometryType = GetGeometryType(usages);
        _materialType = GetMaterialType(usages);
        _skinningType = GetSkinningType(usages);

        // If there's skinning usages but no bone mapping, there's probably something wrong with the data.
        if (_skinningType != typeof(VertexEmpty) && _boneIndexMap == null)
            Penumbra.Log.Warning($"Mesh {meshIndex} has skinned vertex usages but no bone information was provided.");

        Penumbra.Log.Debug($"Mesh {meshIndex} using vertex types geometry: {_geometryType.Name}, material: {_materialType.Name}, skinning: {_skinningType.Name}");
    }

    /// <summary> Build a mapping between indices in this mesh's bone table (if any), and the glTF joint indices provdied. </summary>
    private Dictionary<ushort, int>? BuildBoneIndexMap(Dictionary<string, int> boneNameMap)
    {
        // A BoneTableIndex of 255 means that this mesh is not skinned.
        if (XivMesh.BoneTableIndex == 255)
            return null;

        var xivBoneTable = _mdl.BoneTables[XivMesh.BoneTableIndex];

        var indexMap = new Dictionary<ushort, int>();

        foreach (var (xivBoneIndex, tableIndex) in xivBoneTable.BoneIndex.Take(xivBoneTable.BoneCount).WithIndex())
        {
            var boneName = _mdl.Bones[xivBoneIndex];
            if (!boneNameMap.TryGetValue(boneName, out var gltfBoneIndex))
                throw new Exception($"Armature does not contain bone \"{boneName}\" requested by mesh {_meshIndex}.");

            indexMap.Add((ushort)tableIndex, gltfBoneIndex);
        }

        return indexMap;
    }

    /// <summary> Build glTF meshes for this XIV mesh. </summary>
    private IMeshBuilder<MaterialBuilder>[] BuildMeshes()
    {
        var indices = BuildIndices();
        var vertices = BuildVertices();

        // NOTE: Index indices are specified relative to the LOD's 0, but we're reading chunks for each mesh, so we're specifying the index base relative to the mesh's base.

        if (XivMesh.SubMeshCount == 0)
            return [BuildMesh($"mesh {_meshIndex}", indices, vertices, 0, (int)XivMesh.IndexCount)];

        return _mdl.SubMeshes
            .Skip(XivMesh.SubMeshIndex)
            .Take(XivMesh.SubMeshCount)
            .WithIndex()
            .Select(submesh => BuildMesh($"mesh {_meshIndex}.{submesh.Index}", indices, vertices, (int)(submesh.Value.IndexOffset - XivMesh.StartIndex), (int)submesh.Value.IndexCount))
            .ToArray();
    }

    /// <summary> Build a mesh from the provided indices and vertices. A subset of the full indices may be built by providing an index base and count. </summary>
    private IMeshBuilder<MaterialBuilder> BuildMesh(
        string name,
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
        var meshBuilder = (IMeshBuilder<MaterialBuilder>)Activator.CreateInstance(meshBuilderType, name)!;

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

    /// <summary> Read in the indices for this mesh. </summary>
    private IReadOnlyList<ushort> BuildIndices()
    {
        var reader = new BinaryReader(new MemoryStream(_mdl.RemainingData));
        reader.Seek(_mdl.IndexOffset[_lod] + XivMesh.StartIndex * sizeof(ushort));
        return reader.ReadStructuresAsArray<ushort>((int)XivMesh.IndexCount);
    }

    /// <summary> Build glTF-compatible vertex data for all vertices in this mesh. </summary>
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
                attributes[usage] = ReadVertexAttribute((MdlFile.VertexType)element.Type, streams[element.Stream]);

            var vertexGeometry = BuildVertexGeometry(attributes);
            var vertexMaterial = BuildVertexMaterial(attributes);
            var vertexSkinning = BuildVertexSkinning(attributes);

            var vertexBuilder = (IVertexBuilder)Activator.CreateInstance(vertexBuilderType, vertexGeometry, vertexMaterial, vertexSkinning)!;
            vertices.Add(vertexBuilder);
        }

        return vertices;
    }

    /// <summary> Read a vertex attribute of the specified type from a vertex buffer stream. </summary>
    private object ReadVertexAttribute(MdlFile.VertexType type, BinaryReader reader)
    {
        return type switch
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

    /// <summary> Get the vertex geometry type for this mesh's vertex usages. </summary>
    private Type GetGeometryType(IReadOnlyDictionary<MdlFile.VertexUsage, MdlFile.VertexType> usages)
    {
        if (!usages.ContainsKey(MdlFile.VertexUsage.Position))
            throw new Exception("Mesh does not contain position vertex elements.");

        if (!usages.ContainsKey(MdlFile.VertexUsage.Normal))
            return typeof(VertexPosition);

        if (!usages.ContainsKey(MdlFile.VertexUsage.Tangent1))
            return typeof(VertexPositionNormal);

        return typeof(VertexPositionNormalTangent);
    }

    /// <summary> Build a geometry vertex from a vertex's attributes. </summary>
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

    /// <summary> Get the vertex material type for this mesh's vertex usages. </summary>
    private Type GetMaterialType(IReadOnlyDictionary<MdlFile.VertexUsage, MdlFile.VertexType> usages)
    {
        var uvCount = 0;
        if (usages.TryGetValue(MdlFile.VertexUsage.UV, out var type))
            uvCount = type switch
            {
                MdlFile.VertexType.Half2 => 1,
                MdlFile.VertexType.Half4 => 2,
                MdlFile.VertexType.Single4 => 2,
                _ => throw new Exception($"Unexpected UV vertex type {type}.")
            };

        var materialUsages = (
            uvCount,
            usages.ContainsKey(MdlFile.VertexUsage.Color)
        );

        return materialUsages switch
        {
            (2, true) => typeof(VertexColor1Texture2),
            (2, false) => typeof(VertexTexture2),
            (1, true) => typeof(VertexColor1Texture1),
            (1, false) => typeof(VertexTexture1),
            (0, true) => typeof(VertexColor1),
            (0, false) => typeof(VertexEmpty),

            _ => throw new Exception("Unreachable."),
        };
    }

    /// <summary> Build a material vertex from a vertex's attributes. </summary>
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

        // XIV packs two UVs into a single vec4 attribute.

        if (_materialType == typeof(VertexTexture2))
        {
            var uv = ToVector4(attributes[MdlFile.VertexUsage.UV]);
            return new VertexTexture2(
                new Vector2(uv.X, uv.Y),
                new Vector2(uv.Z, uv.W)
            );
        }

        if (_materialType == typeof(VertexColor1Texture2))
        {
            var uv = ToVector4(attributes[MdlFile.VertexUsage.UV]);
            return new VertexColor1Texture2(
                ToVector4(attributes[MdlFile.VertexUsage.Color]),
                new Vector2(uv.X, uv.Y),
                new Vector2(uv.Z, uv.W)
            );
        }

        throw new Exception($"Unknown material type {_skinningType}");
    }

    /// <summary> Get the vertex skinning type for this mesh's vertex usages. </summary>
    private Type GetSkinningType(IReadOnlyDictionary<MdlFile.VertexUsage, MdlFile.VertexType> usages)
    {
        if (usages.ContainsKey(MdlFile.VertexUsage.BlendWeights) && usages.ContainsKey(MdlFile.VertexUsage.BlendIndices))
            return typeof(VertexJoints4);

        return typeof(VertexEmpty);
    }

    /// <summary> Build a skinning vertex from a vertex's attributes. </summary>
    private IVertexSkinning BuildVertexSkinning(IReadOnlyDictionary<MdlFile.VertexUsage, object> attributes)
    {
        if (_skinningType == typeof(VertexEmpty))
            return new VertexEmpty();

        if (_skinningType == typeof(VertexJoints4))
        {
            if (_boneIndexMap == null)
                throw new Exception("Tried to build skinned vertex but no bone mappings are available.");

            var indices = ToByteArray(attributes[MdlFile.VertexUsage.BlendIndices]);
            var weights = ToVector4(attributes[MdlFile.VertexUsage.BlendWeights]);

            var bindings = Enumerable.Range(0, 4)
                .Select(bindingIndex => {
                    // NOTE: I've not seen any files that throw this error that aren't completely broken.
                    var xivBoneIndex = indices[bindingIndex];
                    if (!_boneIndexMap.TryGetValue(xivBoneIndex, out var jointIndex))
                        throw new Exception($"Vertex contains weight for unknown bone index {xivBoneIndex}.");

                    return (jointIndex, weights[bindingIndex]);
                })
                .ToArray();
            return new VertexJoints4(bindings);
        }

        throw new Exception($"Unknown skinning type {_skinningType}");
    }

    /// <summary> Clamps any tangent W value other than 1 to -1. </summary>
    /// <remarks> Some XIV models seemingly store -1 as 0, this patches over that. </remarks>
    private Vector4 FixTangentVector(Vector4 tangent)
        => tangent with { W = tangent.W == 1 ? 1 : -1 };

    /// <summary> Convert a vertex attribute value to a Vector2. Supported inputs are Vector2, Vector3, and Vector4. </summary>
    private Vector2 ToVector2(object data)
        => data switch
        {
            Vector2 v2 => v2,
            Vector3 v3 => new Vector2(v3.X, v3.Y),
            Vector4 v4 => new Vector2(v4.X, v4.Y),
            _ => throw new ArgumentOutOfRangeException($"Invalid Vector2 input {data}")
        };

    /// <summary> Convert a vertex attribute value to a Vector3. Supported inputs are Vector2, Vector3, and Vector4. </summary>
    private Vector3 ToVector3(object data)
        => data switch
        {
            Vector2 v2 => new Vector3(v2.X, v2.Y, 0),
            Vector3 v3 => v3,
            Vector4 v4 => new Vector3(v4.X, v4.Y, v4.Z),
            _ => throw new ArgumentOutOfRangeException($"Invalid Vector3 input {data}")
        };

    /// <summary> Convert a vertex attribute value to a Vector4. Supported inputs are Vector2, Vector3, and Vector4. </summary>
    private Vector4 ToVector4(object data)
        => data switch
        {
            Vector2 v2 => new Vector4(v2.X, v2.Y, 0, 0),
            Vector3 v3 => new Vector4(v3.X, v3.Y, v3.Z, 1),
            Vector4 v4 => v4,
            _ => throw new ArgumentOutOfRangeException($"Invalid Vector3 input {data}")
        };

    /// <summary> Convert a vertex attribute value to a byte array. </summary>
    private byte[] ToByteArray(object data)
        => data switch
        {
            byte[] value => value,
            _ => throw new ArgumentOutOfRangeException($"Invalid byte[] input {data}")
        };
}