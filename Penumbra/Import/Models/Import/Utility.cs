namespace Penumbra.Import.Models.Import;

public static class Utility
{
    /// <summary> Merge attributes into an existing attribute array, providing an updated submesh mask. </summary>
    /// <param name="oldMask"> Old submesh attribute mask. </param>
    /// <param name="oldAttributes"> Old attribute array that should be merged. </param>
    /// <param name="newAttributes"> New attribute array. Will be mutated. </param>
    /// <returns> New submesh attribute mask, updated to match the merged attribute array. </returns>
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
}
