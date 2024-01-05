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
                .SelectWhere(node => {
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

                var (
                    vertexDeclaration,
                    boneTable,
                    xivMesh,
                    xivSubmeshes,
                    meshVertexBuffer,
                    meshIndices,
                    meshShapeData // fasdfasd
                ) = MeshThing(submeshnodes);

                vertexDeclarations.Add(vertexDeclaration);
                boneTables.Add(boneTable);
                var meshStartIndex = (uint)(xivMesh.StartIndex + idxOffset / sizeof(ushort));
                meshes.Add(xivMesh with {
                    SubMeshIndex = (ushort)(xivMesh.SubMeshIndex + subOffset),
                    // TODO: should probably define a type for index type hey.
                    BoneTableIndex = (ushort)(xivMesh.BoneTableIndex + boneTableOffset),
                    StartIndex = meshStartIndex,
                    VertexBufferOffset = xivMesh.VertexBufferOffset
                        .Select(offset => (uint)(offset + vertOffset))
                        .ToArray(),
                });
                // TODO: could probably do this with linq cleaner
                foreach (var xivSubmesh in xivSubmeshes)
                    submeshes.Add(xivSubmesh with {
                        // TODO: this will need to keep ticking up for each submesh in the same mesh
                        IndexOffset = (uint)(xivSubmesh.IndexOffset + idxOffset / sizeof(ushort))
                    });
                vertexBuffer.AddRange(meshVertexBuffer);
                indices.AddRange(meshIndices.SelectMany(index => BitConverter.GetBytes((ushort)index)));
                foreach (var (key, (shapeMesh, meshShapeValues)) in meshShapeData)
                {
                    List<MdlStructs.ShapeMeshStruct> keyshapedata;
                    if (!shapeData.TryGetValue(key, out keyshapedata))
                    {
                        keyshapedata = new();
                        shapeData.Add(key, keyshapedata);
                    }

                    keyshapedata.Add(shapeMesh with {
                        MeshIndexOffset = meshStartIndex,
                        ShapeValueOffset = (uint)shapeValueOffset,
                    });

                    shapeValues.AddRange(meshShapeValues);
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
                Bones = [
                    "j_kosi",
                ],
                Materials = [
                    "/mt_c0201e6180_top_a.mtrl",
                ],
                RemainingData = vertexBuffer.Concat(indices).ToArray(),
            };

            Out = mdl;
        }

        // this return type is an absolute meme, class that shit up.
        private (
            MdlStructs.VertexDeclarationStruct,
            MdlStructs.BoneTableStruct,
            MdlStructs.MeshStruct,
            IEnumerable<MdlStructs.SubmeshStruct>,
            IEnumerable<byte>,
            IEnumerable<ushort>,
            IDictionary<string, (MdlStructs.ShapeMeshStruct, List<MdlStructs.ShapeValueStruct>)>
        ) MeshThing(IEnumerable<Node> nodes)
        {
            var vertexDeclaration = new MdlStructs.VertexDeclarationStruct() { VertexElements = Array.Empty<MdlStructs.VertexElement>()};
            var vertexCount = (ushort)0;
            // there's gotta be a better way to do this with streams or enumerables or something, surely
            var streams = new List<byte>[3];
            for (var i = 0; i < 3; i++)
                streams[i] = new List<byte>();
            var indexCount = (uint)0;
            var indices = new List<ushort>();
            var strides = new byte[] {0, 0, 0};
            var submeshes = new List<MdlStructs.SubmeshStruct>();
            var morphData = new Dictionary<string, List<MdlStructs.ShapeValueStruct>>();

            // TODO: check that attrs/elems/strides match - we should be generating per-mesh stuff for sanity's sake, but we need to make sure they match if there's >1 node mesh in a mesh.
            foreach (var node in nodes)
            {
                var vertOff = vertexCount;
                var idxOff = indexCount;

                var (vertDecl, newStrides, submesh, vertCount, vertStreams, idxCount, idxs, subMorphData) = NodeMeshThing(node);
                vertexDeclaration = vertDecl; // TODO: CHECK EQUAL AFTER FIRST
                strides = newStrides; // ALSO CHECK EQUAL
                vertexCount += vertCount;
                for (var i = 0; i < 3; i++)
                    streams[i].AddRange(vertStreams[i]);
                indexCount += idxCount;
                // we need to offset the indexes to point into the new stuff
                indices.AddRange(idxs.Select(idx => (ushort)(idx + vertOff)));
                submeshes.Add(submesh with {
                    IndexOffset = submesh.IndexOffset + idxOff
                    // TODO: bone stuff probably
                });
                // TODO: HANDLE MORPHS, NEED TO ADJUST EVERY VALUE'S INDEX OFFSETS
                foreach (var (key, shapeValues) in subMorphData)
                {
                    List<MdlStructs.ShapeValueStruct> valueList;
                    if (!morphData.TryGetValue(key, out valueList))
                    {
                        valueList = new();
                        morphData.Add(key, valueList);
                    }
                    valueList.AddRange(
                        shapeValues
                            .Select(value => value with {
                                // but this is actually an index index
                                BaseIndicesIndex = (ushort)(value.BaseIndicesIndex + idxOff),
                                // this is a vert idx
                                ReplacingVertexIndex = (ushort)(value.ReplacingVertexIndex + vertOff),
                            })
                    );
                }
            }

            // one of these per skinned mesh.
            // TODO: check if mesh has skinning at all. (err if mixed?)
            var boneTable = new MdlStructs.BoneTableStruct()
            {
                BoneCount = 1,
                // this needs to be the full 64. this should be fine _here_ with 0s because i only have one bone, but will need to be fully populated properly. in real files.
                BoneIndex = new ushort[64],
            };

            // mesh
            var xivMesh = new MdlStructs.MeshStruct()
            {
                // TODO: sum across submeshes.
                // TODO: would be cool to share verts on submesh boundaries but that's way out of scope for now.
                VertexCount = vertexCount,
                IndexCount = indexCount,
                // TODO: will have to think about how to represent this - materials can be named, so maybe adjust in parent?
                MaterialIndex = 0,
                // TODO: this will need adjusting by parent
                SubMeshIndex = 0,
                SubMeshCount = (ushort)submeshes.Count,
                // TODO: update in parent
                BoneTableIndex = 0,
                // TODO: this is relative to the lod's index buffer, and is an index, not byte offset
                StartIndex = 0,
                // TODO: these are relative to the lod vertex buffer. these values are accurate for a 0 offset, but lod will need to adjust
                VertexBufferOffset = [0, (uint)streams[0].Count, (uint)(streams[0].Count + streams[1].Count)],
                VertexBufferStride = strides,
                // VertexStreamCount = /* 2 */ (byte)(attributes.Select(attribute => attribute.Element.Stream).Max() + 1),
                VertexStreamCount = (byte)(vertexDeclaration.VertexElements.Select(element => element.Stream).Max() + 1)
            };

            // TODO: can probably get away with flattening the values and blindly setting offsets in parent - mesh matters above, but the values are already Dealt With at this point
            var shapeData = morphData.ToDictionary(
                (pair) => pair.Key,
                pair => (
                    new MdlStructs.ShapeMeshStruct()
                    {
                        // TODO: this needs to be adjusted by the parent
                        MeshIndexOffset = 0,
                        ShapeValueCount = (uint)pair.Value.Count,
                        // TODO: Also update by parent
                        ShapeValueOffset = 0,
                    },
                    pair.Value
                )
            );

            return (
                vertexDeclaration,
                boneTable,
                xivMesh,
                submeshes,
                streams[0].Concat(streams[1]).Concat(streams[2]),
                indices,
                shapeData
            );
        }

        private (
            MdlStructs.VertexDeclarationStruct,
            byte[],
            // MdlStructs.MeshStruct,
            MdlStructs.SubmeshStruct,
            ushort,
            IEnumerable<byte>[],
            uint,
            IEnumerable<ushort>,
            IDictionary<string, List<MdlStructs.ShapeValueStruct>>
        ) NodeMeshThing(Node node)
        {
            // BoneTable (mesh.btidx = 255 means unskinned)
            // vertexdecl

            var mesh = node.Mesh;

            // TODO: should probably say _what_ mesh
            // TODO: would be cool to support >1 primitive (esp. given they're effectively what submeshes are modeled as), but blender doesn't really use them, so not going to prio that at all.
            if (mesh.Primitives.Count != 1)
                throw new Exception($"Mesh has {mesh.Primitives.Count} primitives, expected 1.");
            var primitive = mesh.Primitives[0];

            var accessors = primitive.VertexAccessors;
            
            // var foo = primitive.GetMorphTargetAccessors(0);
            // var bar = foo["POSITION"];
            // var baz = bar.AsVector3Array();

            var morphAccessors = Enumerable.Range(0, primitive.MorphTargetsCount)
                // todo: map by name, probably? or do that later (probably later)
                .Select(index => primitive.GetMorphTargetAccessors(index));

            // TODO: name
            var morphChangedVerts = Enumerable.Range(0, primitive.MorphTargetsCount)
                .Select(_ => new List<int>())
                .ToArray();

            var rawAttributes = new[] {
                VertexAttribute.Position(accessors, morphAccessors),
                VertexAttribute.BlendWeight(accessors),
                VertexAttribute.BlendIndex(accessors),
                VertexAttribute.Normal(accessors),
                VertexAttribute.Tangent1(accessors),
                VertexAttribute.Color(accessors),
                VertexAttribute.Uv(accessors),
            };

            var attributes = new List<VertexAttribute>();
            var offsets = new byte[] {0, 0, 0};
            foreach (var attribute in rawAttributes)
            {
                if (attribute == null) continue;
                var element = attribute.Element;
                // recreating this here really sucks - add a "withstream" or something.
                attributes.Add(new VertexAttribute(
                    element with {Offset = offsets[element.Stream]},
                    attribute.Build,
                    attribute.HasMorph,
                    attribute.BuildMorph
                ));
                offsets[element.Stream] += attribute.Size;
            }
            var strides = offsets;

            // TODO: when merging submeshes, i'll need to check that vert els are the same for all of them, as xiv only stores verts at the mesh level and shares them.
            
            var streams = new List<byte>[3];
            for (var i = 0; i < 3; i++)
                streams[i] = new List<byte>();

            // todo: this is a bit lmao but also... probably the most sane option? getting the count that is
            var vertexCount = primitive.VertexAccessors["POSITION"].Count;
            for (var vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                foreach (var attribute in attributes)
                {
                    streams[attribute.Element.Stream].AddRange(attribute.Build(vertexIndex));
                }

                // this is a meme but idk maybe it's the best approach? it's not like the attr array is ever long
                foreach (var (list, morphIndex) in morphChangedVerts.WithIndex())
                {
                    var hasMorph = attributes.Aggregate(false, (cur, attr) => cur || attr.HasMorph(morphIndex, vertexIndex));
                    // Penumbra.Log.Information($"eh? {vertexIndex} {morphIndex}: {hasMorph}");
                    if (hasMorph)
                    {
                        list.Add(vertexIndex);
                    }
                }
            }

            // indices
            // var indexCount = primitive.GetIndexAccessor().Count;
            // var indices = primitive.GetIndices()
            //     .SelectMany(index => BitConverter.GetBytes((ushort)index))
            //     .ToArray();
            var indices = primitive.GetIndices().Select(idx => (ushort)idx).ToArray();

            // BLAH
            // foreach (var (list, morphIndex) in morphChangedVerts.WithIndex())
            // {
            //     Penumbra.Log.Information($"morph {morphIndex}: {string.Join(",", list)}");
            // }
            // TODO BUILD THE MORPH VERTS
            // (source, target)
            var morphmappingstuff = new List<MdlStructs.ShapeValueStruct>[morphChangedVerts.Length];
            foreach (var (list, morphIndex) in morphChangedVerts.WithIndex())
            {
                var morphmaplist = morphmappingstuff[morphIndex] = new();
                foreach (var vertIdx in list)
                {
                    foreach (var attribute in attributes)
                    {
                        streams[attribute.Element.Stream].AddRange(attribute.BuildMorph(morphIndex, vertIdx));
                    }

                    var fuck = indices.WithIndex()
                        .Where(pair => pair.Value == vertIdx)
                        .Select(pair => pair.Index);

                    foreach (var something in fuck)
                    {
                        morphmaplist.Add(new MdlStructs.ShapeValueStruct(){
                            BaseIndicesIndex = (ushort)something,
                            ReplacingVertexIndex = (ushort)vertexCount,
                        });
                    }
                    vertexCount++;
                }
            }

            // TODO: HANDLE THIS BEING MISSING - probably warn or something, it's not the end of the world
            var morphData = new Dictionary<string, List<MdlStructs.ShapeValueStruct>>();
            if (morphmappingstuff.Length > 0)
            {
                var morphnames = mesh.Extras.GetNode("targetNames").Deserialize<List<string>>();
                morphData = morphmappingstuff
                    .Zip(morphnames)
                    .ToDictionary(
                        (pair) => pair.Second,
                        (pair) => pair.First
                    );
            }

            // one of these per mesh
            var vertexDeclaration = new MdlStructs.VertexDeclarationStruct()
            {
                VertexElements = attributes.Select(attribute => attribute.Element).ToArray(),
            };

            // mesh
            // var xivMesh = new MdlStructs.MeshStruct()
            // {
            //     // TODO: sum across submeshes.
            //     // TODO: would be cool to share verts on submesh boundaries but that's way out of scope for now.
            //     VertexCount = (ushort)vertexCount,
            //     IndexCount = (uint)indexCount,
            //     // TODO: will have to think about how to represent this - materials can be named, so maybe adjust in parent?
            //     MaterialIndex = 0,
            //     // TODO: this will need adjusting by parent
            //     SubMeshIndex = 0,
            //     SubMeshCount = 1,
            //     // TODO: update in parent
            //     BoneTableIndex = 0,
            //     // TODO: this is relative to the lod's index buffer, and is an index, not byte offset
            //     StartIndex = 0,
            //     // TODO: these are relative to the lod vertex buffer. these values are accurate for a 0 offset, but lod will need to adjust
            //     VertexBufferOffset = [0, (uint)streams[0].Count, (uint)(streams[0].Count + streams[1].Count)],
            //     VertexBufferStride = strides,
            //     VertexStreamCount = /* 2 */ (byte)(attributes.Select(attribute => attribute.Element.Stream).Max() + 1),
            // };

            // submesh
            // TODO: once we have multiple submeshes, the _first_ should probably set an index offset of 0, and then further ones delta from there - and then they can be blindly adjusted by the parent that's laying out the meshes.
            var xivSubmesh = new MdlStructs.SubmeshStruct()
            {
                IndexOffset = 0,
                IndexCount = (uint)indices.Length,
                AttributeIndexMask = 0,
                // TODO: not sure how i want to handle these ones
                BoneStartIndex = 0,
                BoneCount = 1,
            };

            // var vertexBuffer = streams[0].Concat(streams[1]).Concat(streams[2]);

            return (
                vertexDeclaration,
                strides,
                // xivMesh,
                xivSubmesh,
                (ushort)vertexCount,
                streams,
                (uint)indices.Length,
                indices,
                morphData
            );
        }

        public bool Equals(IAction? other)
        {
            if (other is not ImportGltfAction rhs)
                return false;

            return true;
        }
    }
}
