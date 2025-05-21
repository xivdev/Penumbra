using System.Collections.Frozen;
using OtterGui.Extensions;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;

namespace Penumbra.Collections.Cache;

public sealed class ShapeAttributeHashSet : Dictionary<(HumanSlot Slot, PrimaryId Id), ulong>
{
    public static readonly IReadOnlyList<GenderRace> GenderRaceValues =
    [
        GenderRace.Unknown, GenderRace.MidlanderMale, GenderRace.MidlanderFemale, GenderRace.HighlanderMale, GenderRace.HighlanderFemale,
        GenderRace.ElezenMale, GenderRace.ElezenFemale, GenderRace.MiqoteMale, GenderRace.MiqoteFemale, GenderRace.RoegadynMale,
        GenderRace.RoegadynFemale, GenderRace.LalafellMale, GenderRace.LalafellFemale, GenderRace.AuRaMale, GenderRace.AuRaFemale,
        GenderRace.HrothgarMale, GenderRace.HrothgarFemale, GenderRace.VieraMale, GenderRace.VieraFemale,
    ];

    public static readonly FrozenDictionary<GenderRace, int> GenderRaceIndices =
        GenderRaceValues.WithIndex().ToFrozenDictionary(p => p.Value, p => p.Index);

    private readonly BitArray _allIds = new((ShapeAttributeManager.ModelSlotSize + 1) * GenderRaceValues.Count);

    public bool this[HumanSlot slot]
        => slot is HumanSlot.Unknown ? All : _allIds[(int)slot * GenderRaceIndices.Count];

    public bool this[GenderRace genderRace]
        => GenderRaceIndices.TryGetValue(genderRace, out var index)
         && _allIds[ShapeAttributeManager.ModelSlotSize * GenderRaceIndices.Count + index];

    public bool this[HumanSlot slot, GenderRace genderRace]
    {
        get
        {
            if (!GenderRaceIndices.TryGetValue(genderRace, out var index))
                return false;

            if (_allIds[ShapeAttributeManager.ModelSlotSize * GenderRaceIndices.Count + index])
                return true;

            return _allIds[(int)slot * GenderRaceIndices.Count + index];
        }
        set
        {
            if (!GenderRaceIndices.TryGetValue(genderRace, out var index))
                return;

            var genderRaceCount = GenderRaceValues.Count;
            if (slot is HumanSlot.Unknown)
                _allIds[ShapeAttributeManager.ModelSlotSize * genderRaceCount + index] = value;
            else
                _allIds[(int)slot * genderRaceCount + index] = value;
        }
    }

    public bool All
        => _allIds[ShapeAttributeManager.ModelSlotSize * GenderRaceIndices.Count];

    public bool Contains(HumanSlot slot, PrimaryId id, GenderRace genderRace)
        => All || this[slot, genderRace] || ContainsEntry(slot, id, genderRace);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool ContainsEntry(HumanSlot slot, PrimaryId id, GenderRace genderRace)
        => GenderRaceIndices.TryGetValue(genderRace, out var index)
         && TryGetValue((slot, id), out var flags)
         && (flags & (1ul << index)) is not 0;

    public bool TrySet(HumanSlot slot, PrimaryId? id, GenderRace genderRace, bool value)
    {
        if (!GenderRaceIndices.TryGetValue(genderRace, out var index))
            return false;

        if (!id.HasValue)
        {
            var slotIndex = slot is HumanSlot.Unknown ? ShapeAttributeManager.ModelSlotSize : (int)slot;
            var old       = _allIds[slotIndex * GenderRaceIndices.Count + index];
            _allIds[slotIndex * GenderRaceIndices.Count + index] = value;
            return old != value;
        }

        if (value)
        {
            if (TryGetValue((slot, id.Value), out var flags))
            {
                var newFlags = flags | (1ul << index);
                if (newFlags == flags)
                    return false;

                this[(slot, id.Value)] = newFlags;
                return true;
            }

            this[(slot, id.Value)] = 1ul << index;
            return true;
        }
        else if (TryGetValue((slot, id.Value), out var flags))
        {
            var newFlags = flags & ~(1ul << index);
            if (newFlags == flags)
                return false;

            if (newFlags is 0)
            {
                Remove((slot, id.Value));
                return true;
            }

            this[(slot, id.Value)] = newFlags;
            return true;
        }

        return false;
    }

    public new void Clear()
    {
        base.Clear();
        _allIds.SetAll(false);
    }

    public bool IsEmpty
        => !_allIds.HasAnySet() && Count is 0;
}
