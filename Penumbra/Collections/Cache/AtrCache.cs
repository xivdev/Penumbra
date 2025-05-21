using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class AtrCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<AtrIdentifier, AtrEntry>(manager, collection)
{
    public bool ShouldBeDisabled(in ShapeAttributeString attribute, HumanSlot slot, PrimaryId id, GenderRace genderRace)
        => DisabledCount > 0 && _atrData.TryGetValue(attribute, out var value) && value.Contains(slot, id, genderRace);

    public int DisabledCount { get; private set; }

    internal IReadOnlyDictionary<ShapeAttributeString, ShapeAttributeHashSet> Data
        => _atrData;

    private readonly Dictionary<ShapeAttributeString, ShapeAttributeHashSet> _atrData = [];

    public void Reset()
    {
        Clear();
        _atrData.Clear();
    }

    protected override void Dispose(bool _)
        => Clear();

    protected override void ApplyModInternal(AtrIdentifier identifier, AtrEntry entry)
    {
        if (!_atrData.TryGetValue(identifier.Attribute, out var value))
        {
            if (entry.Value)
                return;

            value = [];
            _atrData.Add(identifier.Attribute, value);
        }

        if (value.TrySet(identifier.Slot, identifier.Id, identifier.GenderRaceCondition, !entry.Value))
            ++DisabledCount;
    }

    protected override void RevertModInternal(AtrIdentifier identifier)
    {
        if (!_atrData.TryGetValue(identifier.Attribute, out var value))
            return;

        if (value.TrySet(identifier.Slot, identifier.Id, identifier.GenderRaceCondition, false))
        {
            --DisabledCount;
            if (value.IsEmpty)
                _atrData.Remove(identifier.Attribute);
        }
    }
}
