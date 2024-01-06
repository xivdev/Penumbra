using Dalamud.Plugin.Services;
using OtterGui.Tasks;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Files;
using Penumbra.Import.Models.Export;
using Penumbra.Import.Models.Import;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;

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

    public Task<MdlFile> ImportGltf()
    {
        var action = new ImportGltfAction();
        return Enqueue(action).ContinueWith(_ => action.Out!);
    }

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
            Penumbra.Log.Debug("Reading skeleton.");
            var xivSkeleton = BuildSkeleton(cancel);

            Penumbra.Log.Debug("Converting model.");
            var model = ModelExporter.Export(_mdl, xivSkeleton);

            Penumbra.Log.Debug("Building scene.");
            var scene = new SceneBuilder();
            model.AddToScene(scene);

            Penumbra.Log.Debug("Saving.");
            var gltfModel = scene.ToGltf2();
            gltfModel.SaveGLTF(_outputPath);
        }

        /// <summary> Attempt to read out the pertinent information from a .sklb. </summary>
        private XivSkeleton? BuildSkeleton(CancellationToken cancel)
        {
            if (_sklb == null)
                return null;

            var xmlTask = _manager._framework.RunOnFrameworkThread(() => HavokConverter.HkxToXml(_sklb.Skeleton));
            xmlTask.Wait(cancel);
            var xml = xmlTask.Result;

            return SkeletonConverter.FromXml(xml);
        }

        public bool Equals(IAction? other)
        {
            if (other is not ExportToGltfAction rhs)
                return false;

            // TODO: compare configuration and such
            return true;
        }
    }

    private partial class ImportGltfAction : IAction
    {
        public MdlFile? Out;

        public ImportGltfAction()
        {
            //
        }

        public void Execute(CancellationToken cancel)
        {
            var model = ModelRoot.Load("C:\\Users\\ackwell\\blender\\gltf-tests\\c0201e6180_top.gltf");

            Out = ModelImporter.Import(model);
        }

        public bool Equals(IAction? other)
        {
            if (other is not ImportGltfAction rhs)
                return false;

            return true;
        }
    }
}
