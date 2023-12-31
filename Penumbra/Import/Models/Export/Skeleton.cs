using SharpGLTF.Scenes;

namespace Penumbra.Import.Models.Export;

public class XivSkeleton
{
    public Bone[] Bones;

    public XivSkeleton(Bone[] bones)
    {
        Bones = bones;
    }

    public struct Bone
    {
        public string Name;
        public int ParentIndex;
        public Transform Transform;
    }

    public struct Transform {
        public Vector3 Scale;
        public Quaternion Rotation;
        public Vector3 Translation;
    }
}

public struct GltfSkeleton
{
    public NodeBuilder Root;
    public NodeBuilder[] Joints;
    public Dictionary<string, int> Names;
}
