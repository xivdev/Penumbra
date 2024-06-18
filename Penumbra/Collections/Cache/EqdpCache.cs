using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class EqdpCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<EqdpIdentifier, EqdpEntry>(manager, collection)
{
    private readonly Dictionary<(PrimaryId Id, GenderRace GenderRace, bool Accessory), (EqdpEntry Entry, EqdpEntry InverseMask)> _fullEntries =
        [];

    public EqdpEntry ApplyFullEntry(PrimaryId id, GenderRace genderRace, bool accessory, EqdpEntry originalEntry)
        => _fullEntries.TryGetValue((id, genderRace, accessory), out var pair)
            ? (originalEntry & pair.InverseMask) | pair.Entry
            : originalEntry;

    public void Reset()
    {
        Clear();
        _fullEntries.Clear();
    }

    protected override void ApplyModInternal(EqdpIdentifier identifier, EqdpEntry entry)
    {
        var tuple       = (identifier.SetId, identifier.GenderRace, identifier.Slot.IsAccessory());
        var mask        = Eqdp.Mask(identifier.Slot);
        var inverseMask = ~mask;
        if (_fullEntries.TryGetValue(tuple, out var pair))
            pair = ((pair.Entry & inverseMask) | (entry & mask), pair.InverseMask & inverseMask);
        else
            pair = (entry & mask, inverseMask);
        _fullEntries[tuple] = pair;
    }

    protected override void RevertModInternal(EqdpIdentifier identifier)
    {
        var tuple = (identifier.SetId, identifier.GenderRace, identifier.Slot.IsAccessory());

        if (!_fullEntries.Remove(tuple, out var pair))
            return;

        var mask    = Eqdp.Mask(identifier.Slot);
        var newMask = pair.InverseMask | mask;
        if (newMask is not EqdpEntry.FullMask)
            _fullEntries[tuple] = (pair.Entry & ~mask, newMask);
    }

    protected override void Dispose(bool _)
    {
        Clear();
        _fullEntries.Clear();
    }
}
