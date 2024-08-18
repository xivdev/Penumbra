using Lumina.Data.Parsing;
using Penumbra.GameData.Files;

namespace Penumbra.Import.Models.Import;

public static class Utility
{
    /// <summary> Merge attributes into an existing attribute array, providing an updated sub mesh mask. </summary>
    /// <param name="oldMask"> Old sub mesh attribute mask. </param>
    /// <param name="oldAttributes"> Old attribute array that should be merged. </param>
    /// <param name="newAttributes"> New attribute array. Will be mutated. </param>
    /// <returns> New sub mesh attribute mask, updated to match the merged attribute array. </returns>
    public static uint GetMergedAttributeMask(uint oldMask, IList<string> oldAttributes, List<string> newAttributes)
    {
        var metaAttributes = Enumerable.Range(0, 32)
            .Where(index => ((oldMask >> index) & 1) == 1)
            .Select(index => oldAttributes[index]);

        var newMask = 0u;

        foreach (var metaAttribute in metaAttributes)
        {
            var attributeIndex = newAttributes.IndexOf(metaAttribute);
            if (attributeIndex == -1)
            {
                if (newAttributes.Count >= 32)
                    throw new Exception("Models may utilise a maximum of 32 attributes.");

                newAttributes.Add(metaAttribute);
                attributeIndex = newAttributes.Count - 1;
            }

            newMask |= 1u << attributeIndex;
        }

        return newMask;
    }

    /// <summary> Ensures that the two vertex declarations provided are equal, throwing if not. </summary>
    public static void EnsureVertexDeclarationMatch(MdlStructs.VertexDeclarationStruct current, MdlStructs.VertexDeclarationStruct @new,
        IoNotifier notifier)
    {
        if (VertexDeclarationMismatch(current, @new))
            throw notifier.Exception(
                $"""
                 All sub-meshes of a mesh must have equivalent vertex declarations.
                 Current: {FormatVertexDeclaration(current)}
                 New:     {FormatVertexDeclaration(@new)}
                 """
            );
    }

    private static string FormatVertexDeclaration(MdlStructs.VertexDeclarationStruct vertexDeclaration)
        => string.Join(", ",
            vertexDeclaration.VertexElements.Select(element => $"{(MdlFile.VertexUsage)element.Usage} ({(MdlFile.VertexType)element.Type}@{element.Stream}:{element.Offset})"));

    private static bool VertexDeclarationMismatch(MdlStructs.VertexDeclarationStruct a, MdlStructs.VertexDeclarationStruct b)
    {
        var elA = a.VertexElements;
        var elB = b.VertexElements;

        if (elA.Length != elB.Length)
            return true;

        // NOTE: This assumes that elements will always be in the same order. Under the current implementation, that's guaranteed.
        return elA.Zip(elB).Any(pair =>
            pair.First.Usage != pair.Second.Usage
         || pair.First.Type != pair.Second.Type
         || pair.First.Offset != pair.Second.Offset
         || pair.First.Stream != pair.Second.Stream
        );
    }
}
