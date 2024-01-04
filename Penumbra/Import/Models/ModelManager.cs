using Dalamud.Plugin.Services;
using Lumina.Data.Parsing;
using OtterGui.Tasks;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Files;
using Penumbra.Import.Models.Export;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
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

    private class ImportGltfAction : IAction
    {
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

        private (MdlStructs.VertexElement, Action<int, List<byte>>) GetPositionWriter(IReadOnlyDictionary<string, Accessor> accessors)
        {
            if (!accessors.TryGetValue("POSITION", out var accessor))
                throw new Exception("todo: some error about position being hard required");

            var element = new MdlStructs.VertexElement()
            {
                Stream = 0,
                Type = (byte)MdlFile.VertexType.Single3,
                Usage = (byte)MdlFile.VertexUsage.Position,
            };

            IList<Vector3> values = accessor.AsVector3Array();

            return (
                element,
                (index, bytes) => WriteSingle3(values[index], bytes)
            );
        }

        // TODO: probably should sanity check that if there's weights or indexes, both are available? game is always symmetric
        private (MdlStructs.VertexElement, Action<int, List<byte>>)? GetBlendWeightWriter(IReadOnlyDictionary<string, Accessor> accessors)
        {
            if (!accessors.TryGetValue("WEIGHTS_0", out var accessor))
                return null;

            var element = new MdlStructs.VertexElement()
            {
                Stream = 0,
                Type = (byte)MdlFile.VertexType.ByteFloat4,
                Usage = (byte)MdlFile.VertexUsage.BlendWeights,
            };

            var values = accessor.AsVector4Array();

            return (
                element,
                (index, bytes) => WriteByteFloat4(values[index], bytes)
            );
        }

        // TODO: this will need to take in a skeleton mapping of some kind so i can persist the bones used and wire up the joints correctly. hopefully by the "write vertex buffer" stage of building, we already know something about the skeleton.
        private (MdlStructs.VertexElement, Action<int, List<byte>>)? GetBlendIndexWriter(IReadOnlyDictionary<string, Accessor> accessors)
        {
            if (!accessors.TryGetValue("JOINTS_0", out var accessor))
                return null;

            var element = new MdlStructs.VertexElement()
            {
                Stream = 0,
                Type = (byte)MdlFile.VertexType.UInt,
                Usage = (byte)MdlFile.VertexUsage.BlendIndices,
            };

            var values = accessor.AsVector4Array();

            return (
                element,
                (index, bytes) => WriteUInt(values[index], bytes)
            );
        }

        private (MdlStructs.VertexElement, Action<int, List<byte>>)? GetNormalWriter(IReadOnlyDictionary<string, Accessor> accessors)
        {
            if (!accessors.TryGetValue("NORMAL", out var accessor))
                return null;

            var element = new MdlStructs.VertexElement()
            {
                Stream = 1,
                Type = (byte)MdlFile.VertexType.Half4,
                Usage = (byte)MdlFile.VertexUsage.Normal,
            };

            var values = accessor.AsVector3Array();

            return (
                element,
                (index, bytes) => WriteHalf4(new Vector4(values[index], 0), bytes)
            );
        }

        private (MdlStructs.VertexElement, Action<int, List<byte>>)? GetUvWriter(IReadOnlyDictionary<string, Accessor> accessors)
        {
            if (!accessors.TryGetValue("TEXCOORD_0", out var accessor1))
                return null;

            // We're omitting type here, and filling it in on return, as there's two different types we might use.
            var element = new MdlStructs.VertexElement()
            {
                Stream = 1,
                Usage = (byte)MdlFile.VertexUsage.UV,
            };

            var values1 = accessor1.AsVector2Array();

            if (!accessors.TryGetValue("TEXCOORD_1", out var accessor2))
                return (
                    element with {Type = (byte)MdlFile.VertexType.Half2},
                    (index, bytes) => WriteHalf2(values1[index], bytes)
                );

            var values2 = accessor2.AsVector2Array();

            return (
                element with {Type = (byte)MdlFile.VertexType.Half4},
                (index, bytes) => {
                    var value1 = values1[index];
                    var value2 = values2[index];
                    WriteHalf4(new Vector4(value1.X, value1.Y, value2.X, value2.Y), bytes);
                }
            );
        }

        private (MdlStructs.VertexElement, Action<int, List<byte>>)? GetTangent1Writer(IReadOnlyDictionary<string, Accessor> accessors)
        {
            if (!accessors.TryGetValue("TANGENT", out var accessor))
                return null;

            var element = new MdlStructs.VertexElement()
            {
                Stream = 1,
                Type = (byte)MdlFile.VertexType.ByteFloat4,
                Usage = (byte)MdlFile.VertexUsage.Tangent1,
            };

            var values = accessor.AsVector4Array();

            return (
                element,
                (index, bytes) => WriteByteFloat4(values[index], bytes)
            );
        }

        private (MdlStructs.VertexElement, Action<int, List<byte>>)? GetColorWriter(IReadOnlyDictionary<string, Accessor> accessors)
        {
            if (!accessors.TryGetValue("COLOR_0", out var accessor))
                return null;

            var element = new MdlStructs.VertexElement()
            {
                Stream = 1,
                Type = (byte)MdlFile.VertexType.ByteFloat4,
                Usage = (byte)MdlFile.VertexUsage.Color,
            };

            var values = accessor.AsVector4Array();

            return (
                element,
                (index, bytes) => WriteByteFloat4(values[index], bytes)
            );
        }

        private void WriteSingle3(Vector3 input, List<byte> bytes)
        {
            bytes.AddRange(BitConverter.GetBytes(input.X));
            bytes.AddRange(BitConverter.GetBytes(input.Y));
            bytes.AddRange(BitConverter.GetBytes(input.Z));
        }

        private void WriteUInt(Vector4 input, List<byte> bytes)
        {
            bytes.Add((byte)input.X);
            bytes.Add((byte)input.Y);
            bytes.Add((byte)input.Z);
            bytes.Add((byte)input.W);
        }

        private void WriteByteFloat4(Vector4 input, List<byte> bytes)
        {
            bytes.Add((byte)Math.Round(input.X * 255f));
            bytes.Add((byte)Math.Round(input.Y * 255f));
            bytes.Add((byte)Math.Round(input.Z * 255f));
            bytes.Add((byte)Math.Round(input.W * 255f));
        }

        private void WriteHalf2(Vector2 input, List<byte> bytes)
        {
            bytes.AddRange(BitConverter.GetBytes((Half)input.X));
            bytes.AddRange(BitConverter.GetBytes((Half)input.Y));
        }

        private void WriteHalf4(Vector4 input, List<byte> bytes)
        {
            bytes.AddRange(BitConverter.GetBytes((Half)input.X));
            bytes.AddRange(BitConverter.GetBytes((Half)input.Y));
            bytes.AddRange(BitConverter.GetBytes((Half)input.Z));
            bytes.AddRange(BitConverter.GetBytes((Half)input.W));
        }
        
        private byte TypeSize(MdlFile.VertexType type)
        {
            return type switch
            {
                MdlFile.VertexType.Single3 => 12,
                MdlFile.VertexType.Single4 => 16,
                MdlFile.VertexType.UInt => 4,
                MdlFile.VertexType.ByteFloat4 => 4,
                MdlFile.VertexType.Half2 => 4,
                MdlFile.VertexType.Half4 => 8,

                _ => throw new Exception($"Unhandled vertex type {type}"),
            };
        }

        public void Execute(CancellationToken cancel)
        {
            var model = Build();

            // ---

            // todo this'll need to check names and such. also loop. i'm relying on a single mesh here which is Wrong:tm:
            var mesh = model.LogicalNodes
                .Where(node => node.Mesh != null)
                .Select(node => node.Mesh)
                .First();

            // todo check how many prims there are - maybe throw if more than one? not sure
            var prim = mesh.Primitives[0];

            var accessors = prim.VertexAccessors;

            var rawWriters = new[] {
                GetPositionWriter(accessors),
                GetBlendWeightWriter(accessors),
                GetBlendIndexWriter(accessors),
                GetNormalWriter(accessors),
                GetTangent1Writer(accessors),
                GetColorWriter(accessors),
                GetUvWriter(accessors),
            };

            var writers = new List<(MdlStructs.VertexElement, Action<int, List<byte>>)>();
            var offsets = new byte[] {0, 0, 0};
            foreach (var writer in rawWriters)
            {
                if (writer == null) continue;
                var element = writer.Value.Item1;
                writers.Add((
                    element with {Offset = offsets[element.Stream]},
                    writer.Value.Item2
                ));
                offsets[element.Stream] += TypeSize((MdlFile.VertexType)element.Type);
            }
            var strides = offsets;
            
            var streams = new List<byte>[3];
            for (var i = 0; i < 3; i++)
                streams[i] = new List<byte>();

            // todo: this is a bit lmao but also... probably the most sane option? getting the count that is
            var vertexCount = prim.VertexAccessors["POSITION"].Count;
            for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                foreach (var (element, writer) in writers)
                {
                    writer(vertexIndex, streams[element.Stream]);
                }
            }

            // indices
            var indexCount = prim.GetIndexAccessor().Count;
            var indices = prim.GetIndices()
                .SelectMany(index => BitConverter.GetBytes((ushort)index))
                .ToArray();

            var dataBuffer = streams[0].Concat(streams[1]).Concat(streams[2]).Concat(indices);

            var lod1VertLen = (uint)(streams[0].Count + streams[1].Count + streams[2].Count);

            var mdl = new MdlFile()
            {
                Radius = 1,
                // todo: lod calcs... probably handled in penum? we probably only need to think about lod0 for actual import workflow.
                VertexOffset = [0, 0, 0],
                IndexOffset = [lod1VertLen, 0, 0],
                VertexBufferSize = [lod1VertLen, 0, 0],
                IndexBufferSize = [(uint)indices.Length, 0, 0],
                LodCount = 1,
                BoundingBoxes = new MdlStructs.BoundingBoxStruct()
                {
                    Min = [-1, 0, -1, 1],
                    Max = [1, 0, 1, 1],
                },
                VertexDeclarations = [new MdlStructs.VertexDeclarationStruct()
                {
                    VertexElements = writers.Select(x => x.Item1).ToArray(),
                }],
                Meshes = [new MdlStructs.MeshStruct()
                {
                    VertexCount = (ushort)vertexCount,
                    IndexCount = (uint)indexCount,
                    MaterialIndex = 0,
                    SubMeshIndex = 0,
                    SubMeshCount = 1,
                    BoneTableIndex = 0,
                    StartIndex = 0,
                    // todo: this will need to be composed down across multiple submeshes. given submeshes store contiguous buffers
                    VertexBufferOffset = [0, (uint)streams[0].Count, (uint)(streams[0].Count + streams[1].Count)],
                    VertexBufferStride = strides,
                    VertexStreamCount = 2,
                }],
                BoneTables = [new MdlStructs.BoneTableStruct()
                {
                    BoneCount = 1,
                    // this needs to be the full 64. this should be fine _here_ with 0s because i only have one bone, but will need to be fully populated properly. in real files.
                    BoneIndex = new ushort[64],
                }],
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
                SubMeshes = [new MdlStructs.SubmeshStruct()
                {
                    IndexOffset = 0,
                    IndexCount = (uint)indexCount,
                    AttributeIndexMask = 0,
                    BoneStartIndex = 0,
                    BoneCount = 1,
                }],

                // TODO pretty sure this is garbage data as far as textools functions
                // game clearly doesn't rely on this, but the "correct" values are a listing of the bones used by each submesh
                SubMeshBoneMap = [0],

                Lods = [new MdlStructs.LodStruct()
                {
                    MeshIndex = 0,
                    MeshCount = 1,
                    ModelLodRange = 0,
                    TextureLodRange = 0,
                    VertexBufferSize = lod1VertLen,
                    VertexDataOffset = 0,
                    IndexBufferSize = (uint)indexCount,
                    IndexDataOffset = lod1VertLen,
                },
                ],
                Bones = [
                    "j_kosi",
                ],
                Materials = [
                    "/mt_c0201e6180_top_b.mtrl",
                ],
                RemainingData = dataBuffer.ToArray(),
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
