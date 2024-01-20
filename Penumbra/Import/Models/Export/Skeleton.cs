using SharpGLTF.Scenes;

namespace Penumbra.Import.Models.Export;

/// <summary> Representation of a skeleton within XIV. </summary>
public class XivSkeleton(XivSkeleton.Bone[] bones)
{
    public Bone[] Bones = bones;

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

/// <summary> Representation of a glTF-compatible skeleton. </summary>
public struct GltfSkeleton
{
    /// <summary> Root node of the skeleton. </summary>
    public NodeBuilder Root;

    /// <summary> Flattened list of skeleton nodes. </summary>
    public List<NodeBuilder> Joints;

    /// <summary> Mapping of bone names to their index within the joints array. </summary>
    public Dictionary<string, int> Names;

    public (NodeBuilder, int) GenerateBone(string name)
    {
        var node = new NodeBuilder(name);
        var index = Joints.Count;
        Names[name] = index;
        Joints.Add(node);
        Root.AddNode(node);
        return (node, index);
    }
}
