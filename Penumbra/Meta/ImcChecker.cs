using Penumbra.GameData.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Meta;

public class ImcChecker
{
    private static readonly Dictionary<ImcIdentifier, int>                   VariantCounts = [];
    private static          MetaFileManager?                                 _dataManager;
    private static readonly ConcurrentDictionary<ImcIdentifier, CachedEntry> GlobalCachedDefaultEntries = [];

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

    public ImcChecker(MetaFileManager metaFileManager)
        => _dataManager = metaFileManager;

    public static CachedEntry GetDefaultEntry(ImcIdentifier identifier, bool storeCache)
    {
        if (GlobalCachedDefaultEntries.TryGetValue(identifier, out var entry))
            return entry;

        if (_dataManager == null)
            return new CachedEntry(default, false, false);

        try
        {
            var e = ImcFile.GetDefault(_dataManager, identifier.GamePath(), identifier.EquipSlot, identifier.Variant, out var entryExists);
            entry = new CachedEntry(e, true, entryExists);
        }
        catch (Exception)
        {
            entry = new CachedEntry(default, false, false);
        }

        if (storeCache)
            GlobalCachedDefaultEntries.TryAdd(identifier, entry);
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
