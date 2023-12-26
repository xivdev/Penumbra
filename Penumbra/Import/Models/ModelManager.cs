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

    public Task ExportToGltf(/* MdlFile mdl, */string path)
        => Enqueue(new ExportToGltfAction(path));

    private class ExportToGltfAction : IAction
    {
        private readonly string _path;

        public ExportToGltfAction(string path)
        {
            _path = path;
        }

        public void Execute(CancellationToken token)
        {
            var mesh = new MeshBuilder<VertexPosition>("mesh");

            var material1 = new MaterialBuilder()
                .WithDoubleSide(true)
                .WithMetallicRoughnessShader()
                .WithChannelParam(KnownChannel.BaseColor, KnownProperty.RGBA, new Vector4(1, 0, 0, 1));
            var primitive1 = mesh.UsePrimitive(material1);
            primitive1.AddTriangle(new VertexPosition(-10, 0, 0), new VertexPosition(10, 0, 0), new VertexPosition(0, 10, 0));
            primitive1.AddTriangle(new VertexPosition(10, 0, 0), new VertexPosition(-10, 0, 0), new VertexPosition(0, -10, 0));
            
            var material2 = new MaterialBuilder()
                .WithDoubleSide(true)
                .WithMetallicRoughnessShader()
                .WithChannelParam(KnownChannel.BaseColor, KnownProperty.RGBA, new Vector4(1, 0, 1, 1));
            var primitive2 = mesh.UsePrimitive(material2);
            primitive2.AddQuadrangle(new VertexPosition(-5, 0, 3), new VertexPosition(0, -5, 3), new VertexPosition(5, 0, 3), new VertexPosition(0, 5, 3));

            var scene = new SceneBuilder();
            scene.AddRigidMesh(mesh, Matrix4x4.Identity);

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
