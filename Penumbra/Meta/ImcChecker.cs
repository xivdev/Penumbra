using Penumbra.GameData.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta;

public class ImcChecker(MetaFileManager metaFileManager)
{
    public readonly record struct CachedEntry(ImcEntry Entry, bool FileExists, bool VariantExists);

    private readonly Dictionary<ImcIdentifier, CachedEntry> _cachedDefaultEntries = new();

    public CachedEntry GetDefaultEntry(ImcIdentifier identifier, bool storeCache)
    {
        if (_cachedDefaultEntries.TryGetValue(identifier, out var entry))
            return entry;

        try
        {
            var e = ImcFile.GetDefault(metaFileManager, identifier.GamePath(), identifier.EquipSlot, identifier.Variant, out var entryExists);
            entry = new CachedEntry(e, true, entryExists);
        }
        catch (Exception)
        {
            entry = new CachedEntry(default, false, false);
        }

        if (storeCache)
            _cachedDefaultEntries.Add(identifier, entry);
        return entry;
    }

    public CachedEntry GetDefaultEntry(ImcManipulation imcManip, bool storeCache)
        => GetDefaultEntry(new ImcIdentifier(imcManip.PrimaryId, imcManip.Variant, imcManip.ObjectType, imcManip.SecondaryId.Id,
            imcManip.EquipSlot, imcManip.BodySlot), storeCache);
}
