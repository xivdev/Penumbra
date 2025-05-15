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

    internal sealed class ShpHashSet : HashSet<(HumanSlot Slot, PrimaryId Id)>
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

    private readonly Dictionary<ShapeString, ShpHashSet> _shpData = [];

    public void Reset()
    {
        Clear();
        _shpData.Clear();
    }

    protected override void Dispose(bool _)
        => Clear();

    protected override void ApplyModInternal(ShpIdentifier identifier, ShpEntry entry)
    {
        if (!_shpData.TryGetValue(identifier.Shape, out var value))
        {
            value = [];
            _shpData.Add(identifier.Shape, value);
        }

        value.TrySet(identifier.Slot, identifier.Id, entry);
    }

    protected override void RevertModInternal(ShpIdentifier identifier)
    {
        if (!_shpData.TryGetValue(identifier.Shape, out var value))
            return;

        if (value.TrySet(identifier.Slot, identifier.Id, ShpEntry.False) && value.IsEmpty)
            _shpData.Remove(identifier.Shape);
    }
}
