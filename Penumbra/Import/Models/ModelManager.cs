using System.Xml;
using Dalamud.Plugin.Services;
using Lumina.Extensions;
using OtterGui;
using OtterGui.Tasks;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Files;
using Penumbra.Import.Modules;
using Penumbra.String.Classes;
using SharpGLTF.Scenes;
using SharpGLTF.Transforms;

namespace Penumbra.Import.Models;

public sealed class ModelManager : SingleTaskQueue, IDisposable
{
    private readonly IDataManager _gameData;
    private readonly ActiveCollectionData _activeCollectionData;

    private readonly ConcurrentDictionary<IAction, (Task, CancellationTokenSource)> _tasks = new();
    private bool _disposed = false;

    public ModelManager(IDataManager gameData, ActiveCollectionData activeCollectionData)
    {
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

    public Task ExportToGltf(MdlFile mdl, string path)
        => Enqueue(new ExportToGltfAction(mdl, path));

    public void SkeletonTest()
    {
        var sklbPath = "chara/human/c0201/skeleton/base/b0001/skl_c0201b0001.sklb";

        // NOTE: to resolve game path from _mod_, will need to wire the mod class via the modeditwindow to the model editor, through to here.
        // NOTE: to get the game path for a model we'll probably need to use a reverse resolve - there's no guarantee for a modded model that they're named per game path, nor that there's only one name.
        var succeeded = Utf8GamePath.FromString(sklbPath, out var utf8Path, true);
        var testResolve = _activeCollectionData.Current.ResolvePath(utf8Path);
        Penumbra.Log.Information($"resolved: {(testResolve == null ? "NULL" : testResolve.ToString())}");

        // TODO: is it worth trying to use streams for these instead? i'll need to do this for mtrl/tex too, so might be a good idea. that said, the mtrl reader doesn't accept streams, so...
        var bytes = testResolve switch
        {
            null => _gameData.GetFile(sklbPath).Data,
            FullPath path => File.ReadAllBytes(path.ToPath())
        };
        
        var sklb = new SklbFile(bytes);

        // TODO: Consider making these static methods.
        var havokConverter = new HavokConverter();
        var xml = havokConverter.HkxToXml(sklb.Skeleton);

        var skeletonConverter = new SkeletonConverter();
        var skeleton = skeletonConverter.FromXml(xml);

        // this is (less) atrocious
        NodeBuilder? root = null;
        var boneMap = new Dictionary<string, NodeBuilder>();
        for (var boneIndex = 0; boneIndex < skeleton.Bones.Length; boneIndex++)
        {
            var bone = skeleton.Bones[boneIndex];

            if (boneMap.ContainsKey(bone.Name)) continue;

            var node = new NodeBuilder(bone.Name);
            boneMap[bone.Name] = node;

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

            var parent = boneMap[skeleton.Bones[bone.ParentIndex].Name];
            parent.AddNode(node);
        }

        var scene = new SceneBuilder();
        scene.AddNode(root);
        var model = scene.ToGltf2();
        model.SaveGLTF(@"C:\Users\ackwell\blender\gltf-tests\zoingo.gltf");

        Penumbra.Log.Information($"zoingo!");
    }

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
