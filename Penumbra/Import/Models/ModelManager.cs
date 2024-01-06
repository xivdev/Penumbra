using Dalamud.Plugin.Services;
using Lumina.Data.Parsing;
using OtterGui;
using OtterGui.Tasks;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Files;
using Penumbra.Import.Models.Export;
using Penumbra.Import.Models.Import;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;

namespace Penumbra.Import.Models;

public sealed partial class ModelManager : SingleTaskQueue, IDisposable
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
        // TODO: clean this up a bit, i don't actually need all of it.
        [GeneratedRegex(@".*[_ ^](?'Mesh'[0-9]+)[\\.\\-]?([0-9]+)?$", RegexOptions.Compiled)]
        private static partial Regex MeshNameGroupingRegex();

        public MdlFile? Out;

        public ImportGltfAction()
        {
            //
        }

        private ModelRoot Build()
        {
            // Build a super simple plane as a fake gltf input.
            var material = new MaterialBuilder();
            var mesh = new MeshBuilder<VertexPositionNormalTangent, VertexColor1Texture2, VertexJoints4>("mesh 0.0");
            var prim = mesh.UsePrimitive(material);
            var tangent = new Vector4(.5f, .5f, 0, 1);
            var vert1 = new VertexBuilder<VertexPositionNormalTangent, VertexColor1Texture2, VertexJoints4>(
                new VertexPositionNormalTangent(new Vector3(-1, 0, 1), Vector3.UnitY, tangent),
                new VertexColor1Texture2(Vector4.One, Vector2.UnitY, Vector2.Zero),
                new VertexJoints4([(0, 1), (0, 0), (0, 0), (0, 0)])
            );
            var vert2 = new VertexBuilder<VertexPositionNormalTangent, VertexColor1Texture2, VertexJoints4>(
                new VertexPositionNormalTangent(new Vector3(1, 0, 1), Vector3.UnitY, tangent),
                new VertexColor1Texture2(Vector4.One, Vector2.One, Vector2.Zero),
                new VertexJoints4([(0, 1), (0, 0), (0, 0), (0, 0)])
            );
            var vert3 = new VertexBuilder<VertexPositionNormalTangent, VertexColor1Texture2, VertexJoints4>(
                new VertexPositionNormalTangent(new Vector3(-1, 0, -1), Vector3.UnitY, tangent),
                new VertexColor1Texture2(Vector4.One, Vector2.Zero, Vector2.Zero),
                new VertexJoints4([(0, 1), (0, 0), (0, 0), (0, 0)])
            );
            var vert4 = new VertexBuilder<VertexPositionNormalTangent, VertexColor1Texture2, VertexJoints4>(
                new VertexPositionNormalTangent(new Vector3(1, 0, -1), Vector3.UnitY, tangent),
                new VertexColor1Texture2(Vector4.One, Vector2.UnitX, Vector2.Zero),
                new VertexJoints4([(0, 1), (0, 0), (0, 0), (0, 0)])
            );
            prim.AddTriangle(vert2, vert3, vert1);
            prim.AddTriangle(vert2, vert4, vert3);
            var jKosi = new NodeBuilder("j_kosi");
            var scene = new SceneBuilder();
            scene.AddNode(jKosi);
            scene.AddSkinnedMesh(mesh, Matrix4x4.Identity, [jKosi]);
            var model = scene.ToGltf2();

            return model;
        }

        public void Execute(CancellationToken cancel)
        {
            var model = ModelRoot.Load("C:\\Users\\ackwell\\blender\\gltf-tests\\c0201e6180_top.gltf");

            // TODO: for grouping, should probably use `node.name ?? mesh.name`, as which are set seems to depend on the exporter.
            // var nodes = model.LogicalNodes
            //     .Where(node => node.Mesh != null)
            //     // TODO: I'm just grabbing the first 3, as that will contain 0.0, 0.1, and 1.0. testing, and all that.
            //     .Take(3);

            // tt uses this
            // ".*[_ ^]([0-9]+)[\\.\\-]?([0-9]+)?$"
            var nodes = model.LogicalNodes
                .Where(node => node.Mesh != null)
                .Take(6) // this model has all 3 lods in it - the first 6 are the real lod0
                .SelectWhere(node =>
                {
                    var name = node.Name ?? node.Mesh.Name;
                    var match = MeshNameGroupingRegex().Match(name);
                    return match.Success
                        ? (true, (node, int.Parse(match.Groups["Mesh"].Value)))
                        : (false, (node, -1));
                })
                .GroupBy(pair => pair.Item2, pair => pair.node)
                .OrderBy(group => group.Key);

            // this is a representation of a single LoD
            var vertexDeclarations = new List<MdlStructs.VertexDeclarationStruct>();
            var bones = new List<string>();
            var boneTables = new List<MdlStructs.BoneTableStruct>();
            var meshes = new List<MdlStructs.MeshStruct>();
            var submeshes = new List<MdlStructs.SubmeshStruct>();
            var vertexBuffer = new List<byte>();
            var indices = new List<byte>();

            var shapeData = new Dictionary<string, List<MdlStructs.ShapeMeshStruct>>();
            var shapeValues = new List<MdlStructs.ShapeValueStruct>();

            foreach (var submeshnodes in nodes)
            {
                var boneTableOffset = boneTables.Count;
                var meshOffset = meshes.Count;
                var subOffset = submeshes.Count;
                var vertOffset = vertexBuffer.Count;
                var idxOffset = indices.Count;
                var shapeValueOffset = shapeValues.Count;

                var meshthing = MeshImporter.Import(submeshnodes);

                var boneTableIndex = 255;
                if (meshthing.Bones != null)
                {
                    var boneIndices = new List<ushort>();
                    foreach (var mb in meshthing.Bones)
                    {
                        var boneIndex = bones.IndexOf(mb);
                        if (boneIndex == -1)
                        {
                            boneIndex = bones.Count;
                            bones.Add(mb);
                        }
                        boneIndices.Add((ushort)boneIndex);
                    }

                    if (boneIndices.Count > 64)
                        throw new Exception("One mesh cannot be weighted to more than 64 bones.");

                    var boneIndicesArray = new ushort[64];
                    Array.Copy(boneIndices.ToArray(), boneIndicesArray, boneIndices.Count);

                    boneTableIndex = boneTableOffset;
                    boneTables.Add(new MdlStructs.BoneTableStruct()
                    {
                        BoneCount = (byte)boneIndices.Count,
                        BoneIndex = boneIndicesArray,
                    });
                }

                vertexDeclarations.Add(meshthing.VertexDeclaration);
                var meshStartIndex = (uint)(meshthing.MeshStruct.StartIndex + idxOffset / sizeof(ushort));
                meshes.Add(meshthing.MeshStruct with
                {
                    SubMeshIndex = (ushort)(meshthing.MeshStruct.SubMeshIndex + subOffset),
                    // TODO: should probably define a type for index type hey.
                    BoneTableIndex = (ushort)boneTableIndex,
                    StartIndex = meshStartIndex,
                    VertexBufferOffset = meshthing.MeshStruct.VertexBufferOffset
                        .Select(offset => (uint)(offset + vertOffset))
                        .ToArray(),
                });
                // TODO: could probably do this with linq cleaner
                foreach (var xivSubmesh in meshthing.SubMeshStructs)
                    submeshes.Add(xivSubmesh with
                    {
                        // TODO: this will need to keep ticking up for each submesh in the same mesh
                        IndexOffset = (uint)(xivSubmesh.IndexOffset + idxOffset / sizeof(ushort))
                    });
                vertexBuffer.AddRange(meshthing.VertexBuffer);
                indices.AddRange(meshthing.Indicies.SelectMany(index => BitConverter.GetBytes((ushort)index)));
                foreach (var shapeKey in meshthing.ShapeKeys)
                {
                    List<MdlStructs.ShapeMeshStruct> keyshapedata;
                    if (!shapeData.TryGetValue(shapeKey.Name, out keyshapedata))
                    {
                        keyshapedata = new();
                        shapeData.Add(shapeKey.Name, keyshapedata);
                    }

                    keyshapedata.Add(shapeKey.ShapeMesh with
                    {
                        MeshIndexOffset = meshStartIndex,
                        ShapeValueOffset = (uint)shapeValueOffset,
                    });

                    shapeValues.AddRange(shapeKey.ShapeValues);
                }
            }

            var shapes = new List<MdlFile.Shape>();
            var shapeMeshes = new List<MdlStructs.ShapeMeshStruct>();

            foreach (var (name, sms) in shapeData)
            {
                var smOff = shapeMeshes.Count;

                shapeMeshes.AddRange(sms);
                shapes.Add(new MdlFile.Shape()
                {
                    ShapeName = name,
                    // TODO: THESE IS PER LOD
                    ShapeMeshStartIndex = [(ushort)smOff, 0, 0],
                    ShapeMeshCount = [(ushort)sms.Count, 0, 0],
                });
            }

            var mdl = new MdlFile()
            {
                Radius = 1,
                // todo: lod calcs... probably handled in penum? we probably only need to think about lod0 for actual import workflow.
                VertexOffset = [0, 0, 0],
                IndexOffset = [(uint)vertexBuffer.Count, 0, 0],
                VertexBufferSize = [(uint)vertexBuffer.Count, 0, 0],
                IndexBufferSize = [(uint)indices.Count, 0, 0],
                LodCount = 1,
                BoundingBoxes = new MdlStructs.BoundingBoxStruct()
                {
                    Min = [-1, 0, -1, 1],
                    Max = [1, 0, 1, 1],
                },
                VertexDeclarations = vertexDeclarations.ToArray(),
                Meshes = meshes.ToArray(),
                BoneTables = boneTables.ToArray(),
                BoneBoundingBoxes = [
                    // new MdlStructs.BoundingBoxStruct()
                    // {
                    //     Min = [
                    //         -0.081672676f,
                    //         -0.113717034f,
                    //         -0.11905348f,
                    //         1.0f,
                    //     ],
                    //     Max = [
                    //         0.03941727f,
                    //         0.09845419f,
                    //         0.107391916f,
                    //         1.0f,
                    //     ],
                    // },

                    // _would_ be nice if i didn't need to fill out this
                    new MdlStructs.BoundingBoxStruct()
                    {
                        Min = [0, 0, 0, 0],
                        Max  = [0, 0, 0, 0],
                    }
                ],
                SubMeshes = submeshes.ToArray(),

                // TODO pretty sure this is garbage data as far as textools functions
                // game clearly doesn't rely on this, but the "correct" values are a listing of the bones used by each submesh
                SubMeshBoneMap = [0],

                Shapes = shapes.ToArray(),
                ShapeMeshes = shapeMeshes.ToArray(),
                ShapeValues = shapeValues.ToArray(),

                Lods = [new MdlStructs.LodStruct()
                {
                    MeshIndex = 0,
                    MeshCount = (ushort)meshes.Count,
                    ModelLodRange = 0,
                    TextureLodRange = 0,
                    VertexBufferSize = (uint)vertexBuffer.Count,
                    VertexDataOffset = 0,
                    IndexBufferSize = (uint)indices.Count,
                    IndexDataOffset = (uint)vertexBuffer.Count,
                },
                ],
                Bones = bones.ToArray(),
                Materials = [
                    "/mt_c0201e6180_top_a.mtrl",
                ],
                RemainingData = vertexBuffer.Concat(indices).ToArray(),
            };

            Out = mdl;
        }

        public bool Equals(IAction? other)
        {
            if (other is not ImportGltfAction rhs)
                return false;

            return true;
        }
    }
}
