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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool CheckGroups(HumanSlot slot, GenderRace genderRace)
    {
        if (All || this[slot])
            return true;

        if (!GenderRaceIndices.TryGetValue(genderRace, out var index))
            return false;

        if (_allIds[ToIndex(HumanSlot.Unknown, index)])
            return true;

        return _allIds[ToIndex(slot, index)];
    }

    public bool this[HumanSlot slot]
        => _allIds[ToIndex(slot, 0)];

    public bool this[GenderRace genderRace]
        => ToIndex(HumanSlot.Unknown, genderRace, out var index) && _allIds[index];

    public bool this[HumanSlot slot, GenderRace genderRace]
        => ToIndex(slot, genderRace, out var index) && _allIds[index];

    public bool All
        => _allIds[AllIndex];

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static int ToIndex(HumanSlot slot, int genderRaceIndex)
        => slot is HumanSlot.Unknown ? genderRaceIndex + AllIndex : genderRaceIndex + (int)slot * GenderRaceValues.Count;

    public bool Contains(HumanSlot slot, PrimaryId id, GenderRace genderRace)
        => CheckGroups(slot, genderRace) || ContainsEntry(slot, id, genderRace);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool ContainsEntry(HumanSlot slot, PrimaryId id, GenderRace genderRace)
        => GenderRaceIndices.TryGetValue(genderRace, out var index)
         && TryGetValue((slot, id), out var flags)
         && ((flags & 1ul) is not 0 || (flags & (1ul << index)) is not 0);

    public bool TrySet(HumanSlot slot, PrimaryId? id, GenderRace genderRace, bool value)
    {
        if (!GenderRaceIndices.TryGetValue(genderRace, out var index))
            return false;

        if (!id.HasValue)
        {
            var slotIndex = ToIndex(slot, index);
            var old       = _allIds[slotIndex];
            _allIds[slotIndex] = value;
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

    private static readonly int AllIndex = ShapeAttributeManager.ModelSlotSize * GenderRaceValues.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool ToIndex(HumanSlot slot, GenderRace genderRace, out int index)
    {
        if (!GenderRaceIndices.TryGetValue(genderRace, out index))
            return false;

        index = ToIndex(slot, index);
        return true;
    }
}
