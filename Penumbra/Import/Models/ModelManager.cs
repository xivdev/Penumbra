using Dalamud.Plugin.Services;
using OtterGui.Tasks;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Files;
using Penumbra.Import.Modules;
using SharpGLTF.Scenes;
using SharpGLTF.Transforms;

namespace Penumbra.Import.Models;

public sealed class ModelManager : SingleTaskQueue, IDisposable
{
    private readonly IFramework _framework;
    private readonly IDataManager _gameData;
    private readonly ActiveCollectionData _activeCollectionData;

    private readonly ConcurrentDictionary<IAction, (Task, CancellationTokenSource)> _tasks = new();
    private bool _disposed = false;

    public ModelManager(IFramework framework, IDataManager gameData, ActiveCollectionData activeCollectionData)
    {
        _framework = framework;
        _gameData = gameData;
        _activeCollectionData = activeCollectionData;
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

    public Task ExportToGltf(MdlFile mdl, SklbFile? sklb, string outputPath)
        => Enqueue(new ExportToGltfAction(this, mdl, sklb, outputPath));

    private class ExportToGltfAction : IAction
    {
        private readonly ModelManager _manager;

        private readonly MdlFile _mdl;
        private readonly SklbFile? _sklb;
        private readonly string _outputPath;

        public ExportToGltfAction(ModelManager manager, MdlFile mdl, SklbFile? sklb, string outputPath)
        {
            _manager = manager;
            _mdl = mdl;
            _sklb = sklb;
            _outputPath = outputPath;
        }

        public void Execute(CancellationToken cancel)
        {
            var scene = new SceneBuilder();

            var skeleton = BuildSkeleton(cancel);
            if (skeleton != null)
                scene.AddNode(skeleton.Value.Root);

            // TODO: group by LoD in output tree
            for (byte lodIndex = 0; lodIndex < _mdl.LodCount; lodIndex++)
            {
                var lod = _mdl.Lods[lodIndex];

                // TODO: consider other types of mesh?
                for (ushort meshOffset = 0; meshOffset < lod.MeshCount; meshOffset++)
                {
                    var meshBuilders = MeshConverter.ToGltf(_mdl, lodIndex, (ushort)(lod.MeshIndex + meshOffset), skeleton?.Names);
                    // TODO: use a value from the mesh converter for this check, rather than assuming that it has joints
                    foreach (var meshBuilder in meshBuilders)
                        if (skeleton == null)
                            scene.AddRigidMesh(meshBuilder, Matrix4x4.Identity);
                        else
                            scene.AddSkinnedMesh(meshBuilder, Matrix4x4.Identity, skeleton?.Joints);
                }
            }

            var model = scene.ToGltf2();
            model.SaveGLTF(_outputPath);
        }

        // TODO: this should be moved to a seperate model converter or something
        private (NodeBuilder Root, NodeBuilder[] Joints, Dictionary<string, int> Names)? BuildSkeleton(CancellationToken cancel)
        {
            if (_sklb == null)
                return null;

            // TODO: Consider making these static methods.
            // TODO: work out how i handle this havok deal. running it outside the framework causes an immediate ctd.
            var havokConverter = new HavokConverter();
            var xmlTask = _manager._framework.RunOnFrameworkThread(() => havokConverter.HkxToXml(_sklb.Skeleton));
            xmlTask.Wait(cancel);
            var xml = xmlTask.Result;

            var skeletonConverter = new SkeletonConverter();
            var skeleton = skeletonConverter.FromXml(xml);

            // this is (less) atrocious
            NodeBuilder? root = null;
            var names = new Dictionary<string, int>();
            var joints = new List<NodeBuilder>();
            for (var boneIndex = 0; boneIndex < skeleton.Bones.Length; boneIndex++)
            {
                var bone = skeleton.Bones[boneIndex];

                if (names.ContainsKey(bone.Name)) continue;

                var node = new NodeBuilder(bone.Name);
                names[bone.Name] = joints.Count;
                joints.Add(node);

                node.SetLocalTransform(new AffineTransform(
                    bone.Transform.Scale,
                    bone.Transform.Rotation,
                    bone.Transform.Translation
                ), false);

                if (bone.ParentIndex == -1)
                {
                    root = node;
                    continue;
                }

                var parent = joints[names[skeleton.Bones[bone.ParentIndex].Name]];
                parent.AddNode(node);
            }

            if (root == null)
                return null;

            return (root, joints.ToArray(), names);
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
