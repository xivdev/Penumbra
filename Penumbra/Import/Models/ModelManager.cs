using Dalamud.Plugin.Services;
using OtterGui.Tasks;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.Import.Models.Export;
using Penumbra.Import.Models.Import;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models;

public sealed class ModelManager(IFramework framework, GamePathParser parser) : SingleTaskQueue, IDisposable
{
    private readonly IFramework _framework = framework;

    private readonly ConcurrentDictionary<IAction, (Task, CancellationTokenSource)> _tasks = new();

    private bool _disposed;

    public void Dispose()
    {
        _disposed = true;
        foreach (var (_, cancel) in _tasks.Values.ToArray())
            cancel.Cancel();
        _tasks.Clear();
    }

    public Task ExportToGltf(MdlFile mdl, SklbFile? sklb, string outputPath)
        => Enqueue(new ExportToGltfAction(this, mdl, sklb, outputPath));

    public Task<MdlFile?> ImportGltf(string inputPath)
    {
        var action = new ImportGltfAction(inputPath);
        return Enqueue(action).ContinueWith(_ => action.Out);
    }

    /// <summary> Try to find the .sklb path for a .mdl file. </summary>
    /// <param name="mdlPath"> .mdl file to look up the skeleton for. </param>
    public string? ResolveSklbForMdl(string mdlPath)
    {
        var info = parser.GetFileInfo(mdlPath);
        if (info.FileType is not FileType.Model)
            return null;

        return info.ObjectType switch
        {
            ObjectType.Equipment => GamePaths.Skeleton.Sklb.Path(info.GenderRace, "base", 1),
            ObjectType.Accessory => GamePaths.Skeleton.Sklb.Path(info.GenderRace, "base", 1),
            ObjectType.Character when info.BodySlot is BodySlot.Body or BodySlot.Tail => GamePaths.Skeleton.Sklb.Path(info.GenderRace, "base",
                1),
            ObjectType.Character => throw new Exception($"Currently unsupported human model type \"{info.BodySlot}\"."),
            ObjectType.DemiHuman => GamePaths.DemiHuman.Sklb.Path(info.PrimaryId),
            ObjectType.Monster   => GamePaths.Monster.Sklb.Path(info.PrimaryId),
            ObjectType.Weapon    => GamePaths.Weapon.Sklb.Path(info.PrimaryId),
            _                    => null,
        };
    }

    private Task Enqueue(IAction action)
    {
        if (_disposed)
            return Task.FromException(new ObjectDisposedException(nameof(ModelManager)));

        Task task;
        lock (_tasks)
        {
            task = _tasks.GetOrAdd(action, a =>
            {
                var token = new CancellationTokenSource();
                var t     = Enqueue(a, token.Token);
                t.ContinueWith(_ =>
                {
                    lock (_tasks)
                    {
                        return _tasks.TryRemove(a, out var unused);
                    }
                }, CancellationToken.None);
                return (t, token);
            }).Item1;
        }

        return task;
    }

    private class ExportToGltfAction(ModelManager manager, MdlFile mdl, SklbFile? sklb, string outputPath)
        : IAction
    {
        public void Execute(CancellationToken cancel)
        {
            Penumbra.Log.Debug("Reading skeleton.");
            var xivSkeleton = BuildSkeleton(cancel);

            Penumbra.Log.Debug("Converting model.");
            var model = ModelExporter.Export(mdl, xivSkeleton);

            Penumbra.Log.Debug("Building scene.");
            var scene = new SceneBuilder();
            model.AddToScene(scene);

            Penumbra.Log.Debug("Saving.");
            var gltfModel = scene.ToGltf2();
            gltfModel.SaveGLTF(outputPath);
        }

        /// <summary> Attempt to read out the pertinent information from a .sklb. </summary>
        private XivSkeleton? BuildSkeleton(CancellationToken cancel)
        {
            if (sklb == null)
                return null;

            var xmlTask = manager._framework.RunOnFrameworkThread(() => HavokConverter.HkxToXml(sklb.Skeleton));
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

    private partial class ImportGltfAction(string inputPath) : IAction
    {
        public MdlFile? Out;

        public void Execute(CancellationToken cancel)
        {
            var model = ModelRoot.Load(inputPath);

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
