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

    private readonly BitArray _allIds = new(2 * (ShapeAttributeManager.ModelSlotSize + 1) * GenderRaceValues.Count);

    public bool? this[HumanSlot slot]
        => AllCheck(ToIndex(slot, 0));

    public bool? this[GenderRace genderRace]
        => ToIndex(HumanSlot.Unknown, genderRace, out var index) ? AllCheck(index) : null;

    public bool? this[HumanSlot slot, GenderRace genderRace]
        => ToIndex(slot, genderRace, out var index) ? AllCheck(index) : null;

    public bool? All
        => Convert(_allIds[2 * AllIndex], _allIds[2 * AllIndex + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool? AllCheck(int idx)
        => Convert(_allIds[idx], _allIds[idx + 1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static int ToIndex(HumanSlot slot, int genderRaceIndex)
        => 2 * (slot is HumanSlot.Unknown ? genderRaceIndex + AllIndex : genderRaceIndex + (int)slot * GenderRaceValues.Count);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool? CheckEntry(HumanSlot slot, PrimaryId id, GenderRace genderRace)
    {
        if (!GenderRaceIndices.TryGetValue(genderRace, out var index))
            return null;

        // Check for specific ID.
        if (TryGetValue((slot, id), out var flags))
        {
            // Check completely specified entry.
            if (Convert(flags, 2 * index) is { } specified)
                return specified;

            // Check any gender / race.
            if (Convert(flags, 0) is { } anyGr)
                return anyGr;
        }

        // Check for specified gender / race and slot, but no ID.
        if (AllCheck(ToIndex(slot, index)) is { } noIdButGr)
            return noIdButGr;

        // Check for specified gender / race but no slot or ID.
        if (AllCheck(ToIndex(HumanSlot.Unknown, index)) is { } noSlotButGr)
            return noSlotButGr;

        // Check for specified slot but no gender / race or ID.
        if (AllCheck(ToIndex(slot, 0)) is { } noGrButSlot)
            return noGrButSlot;

        return All;
    }

    public bool TrySet(HumanSlot slot, PrimaryId? id, GenderRace genderRace, bool? value, out bool which)
    {
        which = false;
        if (!GenderRaceIndices.TryGetValue(genderRace, out var index))
            return false;

        if (!id.HasValue)
        {
            var slotIndex = ToIndex(slot, index);
            var ret       = false;
            if (value is true)
            {
                if (!_allIds[slotIndex])
                    ret = true;
                _allIds[slotIndex]     = true;
                _allIds[slotIndex + 1] = false;
            }
            else if (value is false)
            {
                if (!_allIds[slotIndex + 1])
                    ret = true;
                _allIds[slotIndex]     = false;
                _allIds[slotIndex + 1] = true;
            }
            else
            {
                if (_allIds[slotIndex])
                {
                    which = true;
                    ret   = true;
                }
                else if (_allIds[slotIndex + 1])
                {
                    which = false;
                    ret   = true;
                }

                _allIds[slotIndex]     = false;
                _allIds[slotIndex + 1] = false;
            }

            return ret;
        }

        if (TryGetValue((slot, id.Value), out var flags))
        {
            index *= 2;
            var newFlags = value switch
            {
                true  => (flags | (1ul << index)) & ~(1ul << (index + 1)),
                false => (flags & ~(1ul << index)) | (1ul << (index + 1)),
                _     => flags & ~(1ul << index) & ~(1ul << (index + 1)),
            };
            if (newFlags == flags)
                return false;

            this[(slot, id.Value)] = newFlags;
            which                  = (flags & (1ul << index)) is not 0;
            return true;
        }

        if (value is null)
            return false;

        this[(slot, id.Value)] = 1ul << (2 * index + (value.Value ? 0 : 1));
        return true;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool? Convert(bool trueValue, bool falseValue)
        => trueValue ? true : falseValue ? false : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool? Convert(ulong mask, int idx)
    {
        mask >>= idx;
        return (mask & 3) switch
        {
            1 => true,
            2 => false,
            _ => null,
        };
    }
}
