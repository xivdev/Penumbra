using Dalamud.Plugin.Services;
using Lumina.Data.Parsing;
using OtterGui;
using OtterGui.Services;
using OtterGui.Tasks;
using Penumbra.Collections.Manager;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;
using Penumbra.Import.Models.Export;
using Penumbra.Import.Models.Import;
using Penumbra.Import.Textures;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using SharpGLTF.Scenes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Penumbra.Import.Models;

using Schema2 = SharpGLTF.Schema2;
using LuminaMaterial = Lumina.Models.Materials.Material;

public sealed class ModelManager(IFramework framework, MetaFileManager metaFileManager, ActiveCollections collections, GamePathParser parser)
    : SingleTaskQueue, IDisposable, IService
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

    public Task<IoNotifier> ExportToGltf(in ExportConfig config, MdlFile mdl, IEnumerable<string> sklbPaths, Func<string, byte[]?> read,
        string outputPath)
        => EnqueueWithResult(
            new ExportToGltfAction(this, config, mdl, sklbPaths, read, outputPath),
            action => action.Notifier
        );

    public Task<(MdlFile?, IoNotifier)> ImportGltf(string inputPath)
        => EnqueueWithResult(
            new ImportGltfAction(inputPath),
            action => (action.Out, action.Notifier)
        );

    /// <summary> Try to find the .sklb paths for a .mdl file. </summary>
    /// <param name="mdlPath"> .mdl file to look up the skeletons for. </param>
    /// <param name="estManipulations"> Modified extra skeleton template parameters. </param>
    public string[] ResolveSklbsForMdl(string mdlPath, KeyValuePair<EstIdentifier, EstEntry>[] estManipulations)
    {
        var info = parser.GetFileInfo(mdlPath);
        if (info.FileType is not FileType.Model)
            return [];

        var baseSkeleton = GamePaths.Skeleton.Sklb.Path(info.GenderRace, "base", 1);

        return info.ObjectType switch
        {
            ObjectType.Equipment when info.EquipSlot.ToSlot() is EquipSlot.Body
                => [baseSkeleton, ..ResolveEstSkeleton(EstType.Body, info, estManipulations)],
            ObjectType.Equipment when info.EquipSlot.ToSlot() is EquipSlot.Head
                => [baseSkeleton, ..ResolveEstSkeleton(EstType.Head, info, estManipulations)],
            ObjectType.Equipment                                                      => [baseSkeleton],
            ObjectType.Accessory                                                      => [baseSkeleton],
            ObjectType.Character when info.BodySlot is BodySlot.Body or BodySlot.Tail => [baseSkeleton],
            ObjectType.Character when info.BodySlot is BodySlot.Hair
                => [baseSkeleton, ..ResolveEstSkeleton(EstType.Hair, info, estManipulations)],
            ObjectType.Character when info.BodySlot is BodySlot.Face or BodySlot.Ear
                => [baseSkeleton, ..ResolveEstSkeleton(EstType.Face, info, estManipulations)],
            ObjectType.Character => throw new Exception($"Currently unsupported human model type \"{info.BodySlot}\"."),
            ObjectType.DemiHuman => [GamePaths.DemiHuman.Sklb.Path(info.PrimaryId)],
            ObjectType.Monster   => [GamePaths.Monster.Sklb.Path(info.PrimaryId)],
            ObjectType.Weapon    => [GamePaths.Weapon.Sklb.Path(info.PrimaryId)],
            _                    => [],
        };
    }

    private string[] ResolveEstSkeleton(EstType type, GameObjectInfo info, KeyValuePair<EstIdentifier, EstEntry>[] estManipulations)
    {
        // Try to find an EST entry from the manipulations provided.
        var modEst = estManipulations
            .FirstOrNull(
                est => est.Key.GenderRace == info.GenderRace
                 && est.Key.Slot == type
                 && est.Key.SetId == info.PrimaryId
            );

        // Try to use an entry from provided manipulations, falling back to the current collection.
        var targetId = modEst?.Value
         ?? collections.Current.MetaCache?.GetEstEntry(type, info.GenderRace, info.PrimaryId)
         ?? EstFile.GetDefault(metaFileManager, type, info.GenderRace, info.PrimaryId);

        // If there's no entries, we can assume that there's no additional skeleton.
        if (targetId == EstEntry.Zero)
            return [];

        return [GamePaths.Skeleton.Sklb.Path(info.GenderRace, type.ToName(), targetId.AsId)];
    }

    /// <summary> Try to resolve the absolute path to a .mtrl from the potentially-partial path provided by a model. </summary>
    private string? ResolveMtrlPath(string rawPath, IoNotifier notifier)
    {
        // TODO: this should probably be chosen in the export settings
        var variantId = 1;

        // Get standardised paths
        var absolutePath = rawPath.StartsWith('/')
            ? LuminaMaterial.ResolveRelativeMaterialPath(rawPath, variantId)
            : rawPath;
        var relativePath = rawPath.StartsWith('/')
            ? rawPath
            : '/' + Path.GetFileName(rawPath);

        if (absolutePath == null)
        {
            notifier.Warning($"Material path \"{rawPath}\" could not be resolved.");
            return null;
        }

        var info = parser.GetFileInfo(absolutePath);
        if (info.FileType is not FileType.Material)
        {
            notifier.Warning($"Material path {rawPath} does not conform to material conventions.");
            return null;
        }

        var resolvedPath = info.ObjectType switch
        {
            ObjectType.Character => GamePaths.Character.Mtrl.Path(
                info.GenderRace, info.BodySlot, info.PrimaryId, relativePath, out _, out _, info.Variant),
            _ => absolutePath,
        };

        Penumbra.Log.Debug($"Resolved material {rawPath} to {resolvedPath}");

        return resolvedPath;
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
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                return (t, token);
            }).Item1;
        }

        return task;
    }

    private Task<TOut> EnqueueWithResult<TAction, TOut>(TAction action, Func<TAction, TOut> process)
        where TAction : IAction
        => Enqueue(action).ContinueWith(task =>
        {
            if (task is { IsFaulted: true, Exception: not null })
                throw task.Exception;

            return process(action);
        }, TaskScheduler.Default);

    private class ExportToGltfAction(
        ModelManager manager,
        ExportConfig config,
        MdlFile mdl,
        IEnumerable<string> sklbPaths,
        Func<string, byte[]?> read,
        string outputPath)
        : IAction
    {
        public readonly IoNotifier Notifier = new();

        public void Execute(CancellationToken cancel)
        {
            Penumbra.Log.Debug($"[GLTF Export] Exporting model to {outputPath}...");

            Penumbra.Log.Debug("[GLTF Export] Reading skeletons...");
            var xivSkeletons = BuildSkeletons(cancel);

            Penumbra.Log.Debug("[GLTF Export] Reading materials...");
            var materials = mdl.Materials
                .Select(path => (path, material: BuildMaterial(path, Notifier, cancel)))
                .Where(pair => pair.material != null)
                .ToDictionary(pair => pair.path, pair => pair.material!.Value);

            Penumbra.Log.Debug("[GLTF Export] Converting model...");
            var model = ModelExporter.Export(config, mdl, xivSkeletons, materials, Notifier);

            Penumbra.Log.Debug("[GLTF Export] Building scene...");
            var scene = new SceneBuilder();
            model.AddToScene(scene);

            Penumbra.Log.Debug("[GLTF Export] Saving...");
            var gltfModel = scene.ToGltf2();
            gltfModel.Save(outputPath);
            Penumbra.Log.Debug("[GLTF Export] Done.");
        }

        /// <summary> Attempt to read out the pertinent information from the sklb file paths provided. </summary>
        private IEnumerable<XivSkeleton> BuildSkeletons(CancellationToken cancel)
        {
            // We're intentionally filtering failed reads here - the failure will
            // be picked up, if relevant, when the model tries to create mappings
            // for a bone in the failed sklb.
            var havokTasks = sklbPaths
                .Select(read)
                .Where(bytes => bytes != null)
                .Select(bytes => new SklbFile(bytes!))
                .WithIndex()
                .Select(CreateHavokTask)
                .ToArray();

            // Result waits automatically.
            return havokTasks.Select(task => SkeletonConverter.FromXml(task.Result));

            // The havok methods we're relying on for this conversion are a bit
            // finicky at the best of times, and can outright cause a CTD if they
            // get upset. Running each conversion on its own tick seems to make
            // this consistently non-crashy across my testing.
            Task<string> CreateHavokTask((SklbFile Sklb, int Index) pair)
                => manager._framework.RunOnTick(
                    () => HavokConverter.HkxToXml(pair.Sklb.Skeleton),
                    delayTicks: pair.Index, cancellationToken: cancel);
        }

        /// <summary> Read a .mtrl and populate its textures. </summary>
        private MaterialExporter.Material? BuildMaterial(string relativePath, IoNotifier notifier, CancellationToken cancel)
        {
            var path = manager.ResolveMtrlPath(relativePath, notifier);
            if (path == null)
                return null;

            var bytes = read(path);
            if (bytes == null)
                return null;

            var mtrl = new MtrlFile(bytes);

            return new MaterialExporter.Material
            {
                Mtrl = mtrl,
                Textures = mtrl.ShaderPackage.Samplers.ToDictionary(
                    sampler => (TextureUsage)sampler.SamplerId,
                    sampler => ConvertImage(mtrl.Textures[sampler.TextureIndex], cancel)
                ),
            };
        }

        /// <summary> Read a texture referenced by a .mtrl and convert it into an ImageSharp image. </summary>
        private Image<Rgba32> ConvertImage(MtrlFile.Texture texture, CancellationToken cancel)
        {
            // Work out the texture's path - the DX11 material flag controls a file name prefix.
            GamePaths.Tex.HandleDx11Path(texture, out var texturePath);
            var bytes = read(texturePath);
            if (bytes == null)
                return CreateDummyImage();

            using var textureData = new MemoryStream(bytes);
            var       image       = TexFileParser.Parse(textureData);
            var       pngImage    = TextureManager.ConvertToPng(image, cancel).AsPng;
            return pngImage ?? throw new Exception("Failed to convert texture to png.");
        }

        private static Image<Rgba32> CreateDummyImage()
        {
            var image = new Image<Rgba32>(1, 1);
            image[0, 0] = Color.White;
            return image;
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
        public          MdlFile?   Out;
        public readonly IoNotifier Notifier = new();

        public void Execute(CancellationToken cancel)
        {
            var model = Schema2.ModelRoot.Load(inputPath);

            Out = ModelImporter.Import(model, Notifier);
        }

        public bool Equals(IAction? other)
        {
            if (other is not ImportGltfAction rhs)
                return false;

            return true;
        }
    }
}
