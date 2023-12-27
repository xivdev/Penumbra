using OtterGui.Tasks;
using Penumbra.GameData.Files;
using Penumbra.Import.Modules;
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
            var scene = new SceneBuilder();

            // TODO: group by LoD in output tree
            for (byte lodIndex = 0; lodIndex < _mdl.LodCount; lodIndex++)
            {
                var lod = _mdl.Lods[lodIndex];

                // TODO: consider other types?
                for (ushort meshOffset = 0; meshOffset < lod.MeshCount; meshOffset++)
                {
                    var meshBuilder = MeshConverter.ToGltf(_mdl, lodIndex, (ushort)(lod.MeshIndex + meshOffset));
                    scene.AddRigidMesh(meshBuilder, Matrix4x4.Identity);
                }
            }

            var model = scene.ToGltf2();
            model.SaveGLTF(_path);
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
