using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class EqdpCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<EqdpIdentifier, EqdpEntry>(manager, collection)
{
    private readonly Dictionary<(PrimaryId Id, GenderRace GenderRace, bool Accessory), EqdpEntry> _fullEntries = [];

    public override void SetFiles()
    { }

    public bool TryGetFullEntry(PrimaryId id, GenderRace genderRace, bool accessory, out EqdpEntry entry)
        => _fullEntries.TryGetValue((id, genderRace, accessory), out entry);

    protected override void IncorporateChangesInternal()
    { }

    public void Reset()
    {
        Clear();
        _fullEntries.Clear();
    }

    protected override void ApplyModInternal(EqdpIdentifier identifier, EqdpEntry entry)
    {
        var tuple = (identifier.SetId, identifier.GenderRace, identifier.Slot.IsAccessory());
        var mask  = Eqdp.Mask(identifier.Slot);
        if (!_fullEntries.TryGetValue(tuple, out var currentEntry))
            currentEntry = ExpandedEqdpFile.GetDefault(Manager, identifier);

        _fullEntries[tuple] = (currentEntry & ~mask) | (entry & mask);
    }

    protected override void RevertModInternal(EqdpIdentifier identifier)
    {
        var tuple = (identifier.SetId, identifier.GenderRace, identifier.Slot.IsAccessory());
        var mask  = Eqdp.Mask(identifier.Slot);

        if (_fullEntries.TryGetValue(tuple, out var currentEntry))
        {
            var def      = ExpandedEqdpFile.GetDefault(Manager, identifier);
            var newEntry = (currentEntry & ~mask) | (def & mask);
            if (currentEntry != newEntry)
            {
                _fullEntries[tuple] = newEntry;
            }
            else
            {
                var slots = tuple.Item3 ? EquipSlotExtensions.AccessorySlots : EquipSlotExtensions.EquipmentSlots;
                if (slots.All(s => !ContainsKey(identifier with { Slot = s })))
                    _fullEntries.Remove(tuple);
                else
                    _fullEntries[tuple] = newEntry;
            }
        }
    }

    protected override void Dispose(bool _)
    {
        Clear();
        _fullEntries.Clear();
    }
}
