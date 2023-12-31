using Penumbra.GameData.Files;
using SharpGLTF.Scenes;
using SharpGLTF.Transforms;

namespace Penumbra.Import.Models.Export;

public class ModelExporter
{
    public class Model
    {
        private List<MeshExporter.Mesh> _meshes;
        private GltfSkeleton? _skeleton;

        public Model(List<MeshExporter.Mesh> meshes, GltfSkeleton? skeleton)
        {
            _meshes = meshes;
            _skeleton = skeleton;
        }

        public void AddToScene(SceneBuilder scene)
        {
            // If there's a skeleton, the root node should be added before we add any potentially skinned meshes.
            var skeletonRoot = _skeleton?.Root;
            if (skeletonRoot != null)
                scene.AddNode(skeletonRoot);
            
            // Add all the meshes to the scene.
            foreach (var mesh in _meshes)
                mesh.AddToScene(scene);
        }
    }

    public static Model Export(MdlFile mdl, XivSkeleton? xivSkeleton)
    {
        var gltfSkeleton = xivSkeleton != null ? ConvertSkeleton(xivSkeleton) : null;
        var meshes = ConvertMeshes(mdl, gltfSkeleton);
        return new Model(meshes, gltfSkeleton);
    }

    private static List<MeshExporter.Mesh> ConvertMeshes(MdlFile mdl, GltfSkeleton? skeleton)
    {
        var meshes = new List<MeshExporter.Mesh>();

        for (byte lodIndex = 0; lodIndex < mdl.LodCount; lodIndex++)
        {
            var lod = mdl.Lods[lodIndex];

            // TODO: consider other types of mesh?
            for (ushort meshOffset = 0; meshOffset < lod.MeshCount; meshOffset++)
            {
                var mesh = MeshExporter.Export(mdl, lodIndex, (ushort)(lod.MeshIndex + meshOffset), skeleton);
                meshes.Add(mesh);
            }
        }

        return meshes;
    }

    private static GltfSkeleton? ConvertSkeleton(XivSkeleton skeleton)
    {
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

        return new()
        {
            Root = root,
            Joints = joints.ToArray(),
            Names = names,
        };
    }
}
