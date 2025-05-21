using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class ShpCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<ShpIdentifier, ShpEntry>(manager, collection)
{
    public bool ShouldBeEnabled(in ShapeAttributeString shape, HumanSlot slot, PrimaryId id, GenderRace genderRace)
        => EnabledCount > 0 && _shpData.TryGetValue(shape, out var value) && value.Contains(slot, id, genderRace);

    internal IReadOnlyDictionary<ShapeAttributeString, ShapeAttributeHashSet> State(ShapeConnectorCondition connector)
        => connector switch
        {
            ShapeConnectorCondition.None   => _shpData,
            ShapeConnectorCondition.Wrists => _wristConnectors,
            ShapeConnectorCondition.Waist  => _waistConnectors,
            ShapeConnectorCondition.Ankles => _ankleConnectors,
            _                              => [],
        };

    public int EnabledCount { get; private set; }

    private readonly Dictionary<ShapeAttributeString, ShapeAttributeHashSet> _shpData         = [];
    private readonly Dictionary<ShapeAttributeString, ShapeAttributeHashSet> _wristConnectors = [];
    private readonly Dictionary<ShapeAttributeString, ShapeAttributeHashSet> _waistConnectors = [];
    private readonly Dictionary<ShapeAttributeString, ShapeAttributeHashSet> _ankleConnectors = [];

    public void Reset()
    {
        Clear();
        _shpData.Clear();
        _wristConnectors.Clear();
        _waistConnectors.Clear();
        _ankleConnectors.Clear();
    }

    protected override void Dispose(bool _)
        => Clear();

    protected override void ApplyModInternal(ShpIdentifier identifier, ShpEntry entry)
    {
        switch (identifier.ConnectorCondition)
        {
            case ShapeConnectorCondition.None:   Func(_shpData); break;
            case ShapeConnectorCondition.Wrists: Func(_wristConnectors); break;
            case ShapeConnectorCondition.Waist:  Func(_waistConnectors); break;
            case ShapeConnectorCondition.Ankles: Func(_ankleConnectors); break;
        }

        return;

        void Func(Dictionary<ShapeAttributeString, ShapeAttributeHashSet> dict)
        {
            if (!dict.TryGetValue(identifier.Shape, out var value))
            {
                if (!entry.Value)
                    return;

                value = [];
                dict.Add(identifier.Shape, value);
            }

            if (value.TrySet(identifier.Slot, identifier.Id, identifier.GenderRaceCondition, entry.Value))
                ++EnabledCount;
        }
    }

    protected override void RevertModInternal(ShpIdentifier identifier)
    {
        switch (identifier.ConnectorCondition)
        {
            case ShapeConnectorCondition.None:   Func(_shpData); break;
            case ShapeConnectorCondition.Wrists: Func(_wristConnectors); break;
            case ShapeConnectorCondition.Waist:  Func(_waistConnectors); break;
            case ShapeConnectorCondition.Ankles: Func(_ankleConnectors); break;
        }

        return;

        void Func(Dictionary<ShapeAttributeString, ShapeAttributeHashSet> dict)
        {
            if (!dict.TryGetValue(identifier.Shape, out var value))
                return;

            if (value.TrySet(identifier.Slot, identifier.Id, identifier.GenderRaceCondition, false))
            {
                --EnabledCount;
                if (value.IsEmpty)
                    dict.Remove(identifier.Shape);
            }
        }
    }
}
