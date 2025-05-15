using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class ShpCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<ShpIdentifier, ShpEntry>(manager, collection)
{
    public bool ShouldBeEnabled(in ShapeString shape, HumanSlot slot, PrimaryId id)
        => _shpData.TryGetValue(shape, out var value) && value.Contains(slot, id);

    internal IReadOnlyDictionary<ShapeString, ShpHashSet> State
        => _shpData;

    internal IEnumerable<(ShapeString, IReadOnlyDictionary<ShapeString, ShpHashSet>)> ConditionState
        => _conditionalSet.Select(kvp => (kvp.Key, (IReadOnlyDictionary<ShapeString, ShpHashSet>)kvp.Value));

    public bool CheckConditionState(ShapeString condition, [NotNullWhen(true)] out IReadOnlyDictionary<ShapeString, ShpHashSet>? dict)
    {
        if (_conditionalSet.TryGetValue(condition, out var d))
        {
            dict = d;
            return true;
        }

        dict = null;
        return false;
    }


    public sealed class ShpHashSet : HashSet<(HumanSlot Slot, PrimaryId Id)>
    {
        private readonly BitArray _allIds = new(ShapeManager.ModelSlotSize);

        public bool All
        {
            get => _allIds[^1];
            set => _allIds[^1] = value;
        }

        public bool this[HumanSlot slot]
        {
            get
            {
                if (slot is HumanSlot.Unknown)
                    return All;

                return _allIds[(int)slot];
            }
            set
            {
                if (slot is HumanSlot.Unknown)
                    _allIds[^1] = value;
                else
                    _allIds[(int)slot] = value;
            }
        }

        public bool Contains(HumanSlot slot, PrimaryId id)
            => All || this[slot] || Contains((slot, id));

        public bool TrySet(HumanSlot slot, PrimaryId? id, ShpEntry value)
        {
            if (slot is HumanSlot.Unknown)
            {
                var old = All;
                All = value.Value;
                return old != value.Value;
            }

            if (!id.HasValue)
            {
                var old = this[slot];
                this[slot] = value.Value;
                return old != value.Value;
            }

            if (value.Value)
                return Add((slot, id.Value));

            return Remove((slot, id.Value));
        }

        public new void Clear()
        {
            base.Clear();
            _allIds.SetAll(false);
        }

        public bool IsEmpty
            => !_allIds.HasAnySet() && Count is 0;
    }

    private readonly Dictionary<ShapeString, ShpHashSet>                          _shpData        = [];
    private readonly Dictionary<ShapeString, Dictionary<ShapeString, ShpHashSet>> _conditionalSet = [];

    public void Reset()
    {
        Clear();
        _shpData.Clear();
        _conditionalSet.Clear();
    }

    protected override void Dispose(bool _)
        => Clear();

    protected override void ApplyModInternal(ShpIdentifier identifier, ShpEntry entry)
    {
        if (identifier.ShapeCondition.Length > 0)
        {
            if (!_conditionalSet.TryGetValue(identifier.ShapeCondition, out var shapes))
            {
                if (!entry.Value)
                    return;

                shapes = new Dictionary<ShapeString, ShpHashSet>();
                _conditionalSet.Add(identifier.ShapeCondition, shapes);
            }

            Func(shapes);
        }
        else
        {
            Func(_shpData);
        }

        void Func(Dictionary<ShapeString, ShpHashSet> dict)
        {
            if (!dict.TryGetValue(identifier.Shape, out var value))
            {
                if (!entry.Value)
                    return;

                value = [];
                dict.Add(identifier.Shape, value);
            }

            value.TrySet(identifier.Slot, identifier.Id, entry);
        }
    }

    protected override void RevertModInternal(ShpIdentifier identifier)
    {
        if (identifier.ShapeCondition.Length > 0)
        {
            if (!_conditionalSet.TryGetValue(identifier.ShapeCondition, out var shapes))
                return;

            Func(shapes);
        }
        else
        {
            Func(_shpData);
        }

        return;

        void Func(Dictionary<ShapeString, ShpHashSet> dict)
        {
            if (!_shpData.TryGetValue(identifier.Shape, out var value))
                return;

            if (value.TrySet(identifier.Slot, identifier.Id, ShpEntry.False) && value.IsEmpty)
                _shpData.Remove(identifier.Shape);
        }
    }
}
