using System.Xml;
using OtterGui;
using Penumbra.Import.Models.Export;

namespace Penumbra.Import.Models;

public static class SkeletonConverter
{
    /// <summary> Parse XIV skeleton data from a havok XML tagfile. </summary>
    /// <param name="xml"> Havok XML tagfile containing skeleton data. </param>
    public static XivSkeleton FromXml(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);

        var mainSkeletonId = GetMainSkeletonId(document);

        var skeletonNode = document.SelectSingleNode($"/hktagfile/object[@type='hkaSkeleton'][@id='{mainSkeletonId}']")
         ?? throw new InvalidDataException($"Failed to find skeleton with id {mainSkeletonId}.");
        var referencePose = ReadReferencePose(skeletonNode);
        var parentIndices = ReadParentIndices(skeletonNode);
        var boneNames     = ReadBoneNames(skeletonNode);

        if (boneNames.Length != parentIndices.Length || boneNames.Length != referencePose.Length)
            throw new InvalidDataException(
                $"Mismatch in bone value array lengths: names({boneNames.Length}) parents({parentIndices.Length}) pose({referencePose.Length})");

        var bones = referencePose
            .Zip(parentIndices, boneNames)
            .Select(values =>
            {
                var (transform, parentIndex, name) = values;
                return new XivSkeleton.Bone()
                {
                    Transform   = transform,
                    ParentIndex = parentIndex,
                    Name        = name,
                };
            })
            .ToArray();

        return new XivSkeleton(bones);
    }

    /// <summary> Get the main skeleton ID for a given skeleton document. </summary>
    /// <param name="node"> XML skeleton document. </param>
    private static string GetMainSkeletonId(XmlNode node)
    {
        var animationSkeletons = node
            .SelectSingleNode("/hktagfile/object[@type='hkaAnimationContainer']/array[@name='skeletons']")?
            .ChildNodes;

        if (animationSkeletons?.Count != 1)
            throw new Exception($"Assumption broken: Expected 1 hkaAnimationContainer skeleton, got {animationSkeletons?.Count ?? 0}.");

        return animationSkeletons[0]!.InnerText;
    }

    /// <summary> Read the reference pose transforms for a skeleton. </summary>
    /// <param name="node"> XML node for the skeleton. </param>
    private static XivSkeleton.Transform[] ReadReferencePose(XmlNode node)
    {
        return ReadArray(
            CheckExists(node.SelectSingleNode("array[@name='referencePose']")),
            n =>
            {
                var raw = ReadVec12(n);
                return new XivSkeleton.Transform()
                {
                    Translation = new Vector3(raw[0], raw[1], raw[2]),
                    Rotation    = new Quaternion(raw[4], raw[5], raw[6], raw[7]),
                    Scale       = new Vector3(raw[8], raw[9], raw[10]),
                };
            }
        );
    }

    /// <summary> Read a 12-item vector from a tagfile. </summary>
    /// <param name="node"> Havok Vec12 XML node. </param>
    private static float[] ReadVec12(XmlNode node)
    {
        var array = node.ChildNodes
            .Cast<XmlNode>()
            .Where(n => n.NodeType != XmlNodeType.Comment)
            .Select(n =>
            {
                var text = n.InnerText.AsSpan().Trim()[1..];
                return BitConverter.Int32BitsToSingle(int.Parse(text, NumberStyles.HexNumber));
            })
            .ToArray();

        if (array.Length != 12)
            throw new InvalidDataException($"Unexpected Vector12 length ({array.Length}).");

        return array;
    }

    /// <summary> Read the bone parent relations for a skeleton. </summary>
    /// <param name="node"> XML node for the skeleton. </param>
    private static int[] ReadParentIndices(XmlNode node)
        // todo: would be neat to genericise array between bare and children
        => CheckExists(node.SelectSingleNode("array[@name='parentIndices']"))
            .InnerText
            .Split((char[]) [' ', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToArray();

    /// <summary> Read the names of bones in a skeleton. </summary>
    /// <param name="node"> XML node for the skeleton. </param>
    private static string[] ReadBoneNames(XmlNode node)
        => ReadArray(
            CheckExists(node.SelectSingleNode("array[@name='bones']")),
            n => CheckExists(n.SelectSingleNode("string[@name='name']")).InnerText
        );

    /// <summary> Read an XML tagfile array, converting it via the provided conversion function. </summary>
    /// <param name="node"> Tagfile XML array node. </param>
    /// <param name="convert"> Function to convert array item nodes to required data types. </param>
    private static T[] ReadArray<T>(XmlNode node, Func<XmlNode, T> convert)
    {
        var element = (XmlElement)node;
        var size    = int.Parse(element.GetAttribute("size"));
        var array   = new T[size];

        foreach (var (childNode, index) in element.ChildNodes.Cast<XmlElement>().WithIndex())
            array[index] = convert(childNode);

        return array;
    }

    /// <summary> Check if the argument is null, returning a non-nullable value if it exists, and throwing if not. </summary>
    private static T CheckExists<T>(T? value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }
}
