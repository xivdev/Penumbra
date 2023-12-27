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
            var meshBuilder = new MeshBuilder<VertexPosition>("mesh");

            var material = new MaterialBuilder()
                .WithDoubleSide(true)
                .WithMetallicRoughnessShader()
                .WithChannelParam(KnownChannel.BaseColor, KnownProperty.RGBA, new Vector4(1, 1, 1, 1));

            // lol, lmao even
            var meshIndex = 2;
            var lod = 0;

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
            var baseOffset = _mdl.VertexOffset[lod] + mesh.VertexBufferOffset[positionVertexElement.Stream] + positionVertexElement.Offset;
            var vertices = new List<VertexPosition>();
            for (var vertexIndex = 0; vertexIndex < mesh.VertexCount; vertexIndex++)
            {
                dataReader.Seek(baseOffset + vertexIndex * mesh.VertexBufferStride[positionVertexElement.Stream]);
                // todo handle type
                vertices.Add(new VertexPosition(
                    dataReader.ReadSingle(),
                    dataReader.ReadSingle(),
                    dataReader.ReadSingle()
                ));
            }

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

        public bool Equals(IAction? other)
        {
            if (other is not ExportToGltfAction rhs)
                return false;

            // TODO: compare configuration
            return true;
        }
    }
}
