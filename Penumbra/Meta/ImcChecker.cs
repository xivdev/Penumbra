using Penumbra.GameData.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta;

public class ImcChecker
{
    private static readonly Dictionary<ImcIdentifier, int> VariantCounts = [];
    private static          MetaFileManager?               _dataManager;


    public static int GetVariantCount(ImcIdentifier identifier)
    {
        lock (VariantCounts)
        {
            if (VariantCounts.TryGetValue(identifier, out var count))
                return count;

            count                     = GetFile(identifier)?.Count ?? 0;
            VariantCounts[identifier] = count;

            return count;
        }
    }

    public readonly record struct CachedEntry(ImcEntry Entry, bool FileExists, bool VariantExists);

    private readonly Dictionary<ImcIdentifier, CachedEntry> _cachedDefaultEntries = new();
    private readonly MetaFileManager                        _metaFileManager;

    public ImcChecker(MetaFileManager metaFileManager)
    {
        _metaFileManager = metaFileManager;
        _dataManager     = metaFileManager;
    }

    public CachedEntry GetDefaultEntry(ImcIdentifier identifier, bool storeCache)
    {
        if (_cachedDefaultEntries.TryGetValue(identifier, out var entry))
            return entry;

        try
        {
            var e = ImcFile.GetDefault(_metaFileManager, identifier.GamePath(), identifier.EquipSlot, identifier.Variant, out var entryExists);
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

    private static ImcFile? GetFile(ImcIdentifier identifier)
    {
        if (_dataManager == null)
            return null;

        try
        {
            return new ImcFile(_dataManager, identifier);
        }
        catch
        {
            return null;
        }
    }
}
