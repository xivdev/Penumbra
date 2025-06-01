using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class AtrCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<AtrIdentifier, AtrEntry>(manager, collection)
{
    public bool ShouldBeDisabled(in ShapeAttributeString attribute, HumanSlot slot, PrimaryId id, GenderRace genderRace)
        => DisabledCount > 0 && _atrData.TryGetValue(attribute, out var value) && value.CheckEntry(slot, id, genderRace) is false;

    public int EnabledCount  { get; private set; }
    public int DisabledCount { get; private set; }


    internal IReadOnlyDictionary<ShapeAttributeString, ShapeAttributeHashSet> Data
        => _atrData;

    private readonly Dictionary<ShapeAttributeString, ShapeAttributeHashSet> _atrData = [];

    public void Reset()
    {
        Clear();
        _atrData.Clear();
        DisabledCount = 0;
        EnabledCount  = 0;
    }

    protected override void Dispose(bool _)
        => Reset();

    protected override void ApplyModInternal(AtrIdentifier identifier, AtrEntry entry)
    {
        if (!_atrData.TryGetValue(identifier.Attribute, out var value))
        {
            value = [];
            _atrData.Add(identifier.Attribute, value);
        }

        if (value.TrySet(identifier.Slot, identifier.Id, identifier.GenderRaceCondition, entry.Value, out _))
        {
            if (entry.Value)
                ++EnabledCount;
            else
                ++DisabledCount;
        }
    }

    protected override void RevertModInternal(AtrIdentifier identifier)
    {
        if (!_atrData.TryGetValue(identifier.Attribute, out var value))
            return;

        if (value.TrySet(identifier.Slot, identifier.Id, identifier.GenderRaceCondition, null, out var which))
        {
            if (which)
                --EnabledCount;
            else
                --DisabledCount;
            if (value.IsEmpty)
                _atrData.Remove(identifier.Attribute);
        }
    }
}
