namespace Penumbra.Import.Models;

// TODO: this should almost certainly live in gamedata. if not, it should at _least_ be adjacent to the model handling.
public class Skeleton
{
    public Bone[] Bones;

    public Skeleton(Bone[] bones)
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
