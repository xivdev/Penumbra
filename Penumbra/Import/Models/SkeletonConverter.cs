using System.Xml;
using OtterGui;

namespace Penumbra.Import.Models;

// TODO: tempted to say that this living here is more okay? that or next to havok converter, wherever that ends up.
public class SkeletonConverter
{
    public Skeleton FromXml(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);

        var mainSkeletonId = GetMainSkeletonId(document);

        var skeletonNode = document.SelectSingleNode($"/hktagfile/object[@type='hkaSkeleton'][@id='{mainSkeletonId}']");
        if (skeletonNode == null)
            throw new InvalidDataException();

        var referencePose = ReadReferencePose(skeletonNode);
        var parentIndices = ReadParentIndices(skeletonNode);
        var boneNames = ReadBoneNames(skeletonNode);

        if (boneNames.Length != parentIndices.Length || boneNames.Length != referencePose.Length)
            throw new InvalidDataException();

        var bones = referencePose
            .Zip(parentIndices, boneNames)
            .Select(values =>
            {
                var (transform, parentIndex, name) = values;
                return new Skeleton.Bone()
                {
                    Transform = transform,
                    ParentIndex = parentIndex,
                    Name = name,
                };
            })
            .ToArray();
        
        return new Skeleton(bones);
    }

    /// <summary>Get the main skeleton ID for a given skeleton document.</summary>
    /// <param name="node">XML skeleton document.</param>
    private string GetMainSkeletonId(XmlNode node)
    {
        var animationSkeletons = node
            .SelectSingleNode("/hktagfile/object[@type='hkaAnimationContainer']/array[@name='skeletons']")?
            .ChildNodes;

        if (animationSkeletons?.Count != 1)
            throw new Exception($"Assumption broken: Expected 1 hkaAnimationContainer skeleton, got {animationSkeletons?.Count ?? 0}");

        return animationSkeletons[0]!.InnerText;
    }

    /// <summary>Read the reference pose transforms for a skeleton.</summary>
    /// <param name="node">XML node for the skeleton.</param>
    private Skeleton.Transform[] ReadReferencePose(XmlNode node)
    {
        return ReadArray(
            CheckExists(node.SelectSingleNode("array[@name='referencePose']")),
            node =>
            {
                var raw = ReadVec12(node);
                return new Skeleton.Transform()
                {
                    Translation = new(raw[0], raw[1], raw[2]),
                    Rotation = new(raw[4], raw[5], raw[6], raw[7]),
                    Scale = new(raw[8], raw[9], raw[10]),
                };
            }
        );
    }

    private float[] ReadVec12(XmlNode node)
    {
        var array = node.ChildNodes
            .Cast<XmlNode>()
            .Where(node => node.NodeType != XmlNodeType.Comment)
            .Select(node =>
            {
                var text = node.InnerText.Trim()[1..];
                // TODO: surely there's a less shit way to do this i mean seriously
                return BitConverter.ToSingle(BitConverter.GetBytes(int.Parse(text, NumberStyles.HexNumber)));
            })
            .ToArray();

        if (array.Length != 12)
            throw new InvalidDataException();

        return array;
    }

    private int[] ReadParentIndices(XmlNode node)
    {
        // todo: would be neat to genericise array between bare and children
        return CheckExists(node.SelectSingleNode("array[@name='parentIndices']"))
            .InnerText
            .Split(new char[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToArray();
    }

    private string[] ReadBoneNames(XmlNode node)
    {
        return ReadArray(
            CheckExists(node.SelectSingleNode("array[@name='bones']")),
            node => CheckExists(node.SelectSingleNode("string[@name='name']")).InnerText
        );
    }

    private T[] ReadArray<T>(XmlNode node, Func<XmlNode, T> convert)
    {
        var element = (XmlElement)node;

        var size = int.Parse(element.GetAttribute("size"));

        var array = new T[size];
        foreach (var (childNode, index) in element.ChildNodes.Cast<XmlElement>().WithIndex())
            array[index] = convert(childNode);

        return array;
    }

    private static T CheckExists<T>(T? value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }
}
