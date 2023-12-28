using System.Xml;
using Dalamud.Plugin.Services;
using Lumina.Data;
using Lumina.Extensions;
using OtterGui;
using OtterGui.Tasks;
using Penumbra.GameData.Files;
using Penumbra.Import.Modules;
using SharpGLTF.Scenes;
using SharpGLTF.Transforms;

namespace Penumbra.Import.Models;

public sealed class ModelManager : SingleTaskQueue, IDisposable
{
    private readonly IDataManager _gameData;

    private readonly ConcurrentDictionary<IAction, (Task, CancellationTokenSource)> _tasks = new();
    private bool _disposed = false;

    public ModelManager(IDataManager gameData)
    {
        _gameData = gameData;
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

        var something = _gameData.GetFile<Garbage>(sklbPath);

        var fuck = new HavokConverter();
        var killme = fuck.HkxToXml(something.Skeleton);

        var doc = new XmlDocument();
        doc.LoadXml(killme);

        var skels = doc.SelectNodes("/hktagfile/object[@type='hkaSkeleton']")
            .Cast<XmlElement>()
            .Select(element => new Skel(element))
            .ToArray();

        // todo: look into how this is selecting the skel - only first?
        var animSkel = doc.SelectSingleNode("/hktagfile/object[@type='hkaAnimationContainer']")
            .SelectNodes("array[@name='skeletons']")
            .Cast<XmlElement>()
            .First();
        var mainSkelId = animSkel.ChildNodes[0].InnerText;

        var mainSkel = skels.First(skel => skel.Id == mainSkelId);

        // this is atrocious
        NodeBuilder? root = null;
        var boneMap = new Dictionary<string, NodeBuilder>();
        for (var boneIndex = 0; boneIndex < mainSkel.BoneNames.Length; boneIndex++)
        {
            var name = mainSkel.BoneNames[boneIndex];
            if (boneMap.ContainsKey(name)) continue;

            var node = new NodeBuilder(name);

            var rp = mainSkel.ReferencePose[boneIndex];
            var transform = new AffineTransform(
                new Vector3(rp[8], rp[9], rp[10]),
                new Quaternion(rp[4], rp[5], rp[6], rp[7]),
                new Vector3([rp[0], rp[1], rp[2]])
            );
            node.SetLocalTransform(transform, false);

            boneMap[name] = node;

            var parentId = mainSkel.ParentIndices[boneIndex];
            if (parentId == -1)
            {
                root = node;
                continue;
            }

            var parent = boneMap[mainSkel.BoneNames[parentId]];
            parent.AddNode(node);
        }

        var scene = new SceneBuilder();
        scene.AddNode(root);
        var model = scene.ToGltf2();
        model.SaveGLTF(@"C:\Users\ackwell\blender\gltf-tests\zoingo.gltf");

        Penumbra.Log.Information($"zoingo {string.Join(',', mainSkel.ParentIndices)}");
    }
    
    // this is garbage that should be in gamedata

    private sealed class Garbage : FileResource
    {
        public byte[] Skeleton;

        public override void LoadFile()
        {
            var magic = Reader.ReadUInt32();
            if (magic != 0x736B6C62)
                throw new InvalidDataException("Invalid sklb magic");

            // todo do this all properly jfc
            var version = Reader.ReadUInt32();
            
            var oldHeader = version switch {
                0x31313030 or 0x31313130 or 0x31323030 => true,
                0x31333030 => false,
                _ => throw new InvalidDataException($"Unknown version {version}")
            };

            // Skeleton offset directly follows the layer offset.
            uint skeletonOffset;
            if (oldHeader)
            {
                Reader.ReadInt16();
                skeletonOffset = Reader.ReadUInt16();
            }
            else
            {
                Reader.ReadUInt32();
                skeletonOffset = Reader.ReadUInt32();
            }

            Reader.Seek(skeletonOffset);
            Skeleton = Reader.ReadBytes((int)(Reader.BaseStream.Length - skeletonOffset));
        }
    }

    private class Skel
    {
        public readonly string Id;

        public readonly float[][] ReferencePose;
        public readonly int[] ParentIndices;
        public readonly string[] BoneNames;

        // TODO: this shouldn't have any reference to the skel xml - i should just make it a bare class that can be repr'd in gamedata or whatever
        public Skel(XmlElement el)
        {
            Id = el.GetAttribute("id");

            ReferencePose = ReadReferencePose(el);
            ParentIndices = ReadParentIndices(el);
            BoneNames = ReadBoneNames(el);
        }

        private float[][] ReadReferencePose(XmlElement el)
        {
            return ReadArray(
                (XmlElement)el.SelectSingleNode("array[@name='referencePose']"),
                ReadVec12
            );
        }

        private float[] ReadVec12(XmlElement el)
        {
            return el.ChildNodes
                .Cast<XmlNode>()
                .Where(node => node.NodeType != XmlNodeType.Comment)
                .Select(node => {
                    var t = node.InnerText.Trim()[1..];
                    // todo: surely there's a less shit way to do this i mean seriously
                    return BitConverter.ToSingle(BitConverter.GetBytes(int.Parse(t, NumberStyles.HexNumber)));
                })
                .ToArray();
        }

        private int[] ReadParentIndices(XmlElement el)
        {
            // todo: would be neat to genericise array between bare and children
            return el.SelectSingleNode("array[@name='parentIndices']")
                .InnerText
                .Split(new char[] {' ', '\n'}, StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToArray();
        }

        private string[] ReadBoneNames(XmlElement el)
        {
            return ReadArray(
                (XmlElement)el.SelectSingleNode("array[@name='bones']"),
                el => el.SelectSingleNode("string[@name='name']").InnerText
            );
        }

        private T[] ReadArray<T>(XmlElement el, Func<XmlElement, T> convert)
        {
            var size = int.Parse(el.GetAttribute("size"));

            var array = new T[size];
            foreach (var (node, index) in el.ChildNodes.Cast<XmlElement>().WithIndex())
            {
                array[index] = convert(node);
            }

            return array;
        }
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
