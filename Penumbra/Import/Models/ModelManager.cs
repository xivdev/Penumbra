using System.Collections.Immutable;
using Lumina.Data.Parsing;
using Lumina.Extensions;
using OtterGui.Tasks;
using Penumbra.GameData.Files;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;

namespace Penumbra.Import.Models;

public sealed class ModelManager : SingleTaskQueue, IDisposable
{
    private readonly ConcurrentDictionary<IAction, (Task, CancellationTokenSource)> _tasks = new();
    private bool _disposed = false;

    public ModelManager()
    {
        //
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var (_, cancel) in _tasks.Values.ToArray())
            cancel.Cancel();
        _tasks.Clear();
    }

    private Task Enqueue(IAction action)
    {
        if (_disposed)
            return Task.FromException(new ObjectDisposedException(nameof(ModelManager)));

        Task task;
        lock (_tasks)
        {
            task = _tasks.GetOrAdd(action, action =>
            {
                var token = new CancellationTokenSource();
                var task = Enqueue(action, token.Token);
                task.ContinueWith(_ => _tasks.TryRemove(action, out var unused), CancellationToken.None);
                return (task, token);
            }).Item1;
        }

        return task;
    }

    public Task ExportToGltf(MdlFile mdl, string path)
        => Enqueue(new ExportToGltfAction(mdl, path));

    private class ExportToGltfAction : IAction
    {
        private readonly MdlFile _mdl;
        private readonly string _path;

        public ExportToGltfAction(MdlFile mdl, string path)
        {
            _mdl = mdl;
            _path = path;
        }

        public void Execute(CancellationToken token)
        {
            // lol, lmao even
            var meshIndex = 2;
            var lod = 0;

            var elements = _mdl.VertexDeclarations[meshIndex].VertexElements;

            var usages = elements
                .Select(element => (MdlFile.VertexUsage)element.Usage)
                .ToImmutableHashSet();
            var geometryType = GetGeometryType(usages);

            // TODO: probablly can do this a bit later but w/e
            var meshBuilderType = typeof(MeshBuilder<,,>).MakeGenericType(geometryType, typeof(VertexEmpty), typeof(VertexEmpty));
            var meshBuilder = (IMeshBuilder<MaterialBuilder>)Activator.CreateInstance(meshBuilderType, "mesh2")!;

            var material = new MaterialBuilder()
                .WithDoubleSide(true)
                .WithMetallicRoughnessShader()
                .WithChannelParam(KnownChannel.BaseColor, KnownProperty.RGBA, new Vector4(1, 1, 1, 1));

            var mesh = _mdl.Meshes[meshIndex];
            var submesh = _mdl.SubMeshes[mesh.SubMeshIndex]; // just first for now

            var positionVertexElement = _mdl.VertexDeclarations[meshIndex].VertexElements
                .Where(decl => (MdlFile.VertexUsage)decl.Usage == MdlFile.VertexUsage.Position)
                .First();

            // reading in the entire indices list
            var dataReader = new BinaryReader(new MemoryStream(_mdl.RemainingData));
            dataReader.Seek(_mdl.IndexOffset[lod]);
            var indices = dataReader.ReadStructuresAsArray<ushort>((int)_mdl.IndexBufferSize[lod] / sizeof(ushort));

            // read in verts for this mesh
            var vertices = BuildVertices(lod, mesh, _mdl.VertexDeclarations[meshIndex].VertexElements, geometryType);

            // build a primitive for the submesh
            var primitiveBuilder = meshBuilder.UsePrimitive(material);
            // they're all tri list
            for (var indexOffset = 0; indexOffset < submesh.IndexCount; indexOffset += 3)
            {
                var index = indexOffset + submesh.IndexOffset;

                primitiveBuilder.AddTriangle(
                    vertices[indices[index + 0]],
                    vertices[indices[index + 1]],
                    vertices[indices[index + 2]]
                );
            }

            var scene = new SceneBuilder();
            scene.AddRigidMesh(meshBuilder, Matrix4x4.Identity);

            var model = scene.ToGltf2();
            model.SaveGLTF(_path);
        }

        // todo all of this is mesh specific so probably should be a class per mesh? with the lod, too?
        private IReadOnlyList<IVertexBuilder> BuildVertices(int lod, MdlStructs.MeshStruct mesh, IEnumerable<MdlStructs.VertexElement> elements, Type geometryType)
        {
            var vertexBuilderType = typeof(VertexBuilder<,,>).MakeGenericType(geometryType, typeof(VertexEmpty), typeof(VertexEmpty));

            // todo: demagic the 3
            // todo note this assumes that the buffer streams are tightly packed. that's a safe assumption - right? lumina assumes as much
            var streams = new BinaryReader[3];
            for (var streamIndex = 0; streamIndex < 3; streamIndex++)
            {
                streams[streamIndex] = new BinaryReader(new MemoryStream(_mdl.RemainingData));
                streams[streamIndex].Seek(_mdl.VertexOffset[lod] + mesh.VertexBufferOffset[streamIndex]);
            }

            var sortedElements = elements
                .OrderBy(element => element.Offset)
                .ToList();

            var vertices = new List<IVertexBuilder>();

            // note this is being reused
            var attributes = new Dictionary<MdlFile.VertexUsage, object>();
            for (var vertexIndex = 0; vertexIndex < mesh.VertexCount; vertexIndex++)
            {
                attributes.Clear();

                foreach (var element in sortedElements)
                    attributes[(MdlFile.VertexUsage)element.Usage] = ReadVertexAttribute(streams[element.Stream], element);

                var vertexGeometry = BuildVertexGeometry(geometryType, attributes);

                var vertexBuilder = (IVertexBuilder)Activator.CreateInstance(vertexBuilderType, vertexGeometry, new VertexEmpty(), new VertexEmpty())!;
                vertices.Add(vertexBuilder);
            }

            return vertices;
        }

        // todo i fucking hate this `object` type god i hate c# gimme sum types pls
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

        private IVertexGeometry BuildVertexGeometry(Type geometryType, IReadOnlyDictionary<MdlFile.VertexUsage, object> attributes)
        {
            if (geometryType == typeof(VertexPosition))
                return new VertexPosition(
                    ToVector3(attributes[MdlFile.VertexUsage.Position])
                );

            if (geometryType == typeof(VertexPositionNormal))
                return new VertexPositionNormal(
                    ToVector3(attributes[MdlFile.VertexUsage.Position]),
                    ToVector3(attributes[MdlFile.VertexUsage.Normal])
                );

            if (geometryType == typeof(VertexPositionNormalTangent))
                return new VertexPositionNormalTangent(
                    ToVector3(attributes[MdlFile.VertexUsage.Position]),
                    ToVector3(attributes[MdlFile.VertexUsage.Normal]),
                    ToVector4(attributes[MdlFile.VertexUsage.Tangent1])
                );

            throw new Exception($"Unknown geometry type {geometryType}.");
        }

        private Vector3 ToVector3(object data)
        {
            return data switch
            {
                Vector2 v2 => new Vector3(v2.X, v2.Y, 0),
                Vector3 v3 => v3,
                Vector4 v4 => new Vector3(v4.X, v4.Y, v4.Z),
                _ => throw new ArgumentOutOfRangeException($"Invalid Vector3 input {data}")
            };
        }

        private Vector4 ToVector4(object data)
        {
            return data switch
            {
                Vector2 v2 => new Vector4(v2.X, v2.Y, 0, 0),
                Vector3 v3 => new Vector4(v3.X, v3.Y, v3.Z, 1),
                Vector4 v4 => v4,
                _ => throw new ArgumentOutOfRangeException($"Invalid Vector3 input {data}")
            };
        }

        public bool Equals(IAction? other)
        {
            if (other is not ExportToGltfAction rhs)
                return false;

            // TODO: compare configuration and such
            return true;
        }
    }
}
