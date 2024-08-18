using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class EqpCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<EqpIdentifier, EqpEntry>(manager, collection)
{
    public unsafe EqpEntry GetValues(CharacterArmor* armor)
    {
        var bodyEntry = GetSingleValue(armor[1].Set, EquipSlot.Body);
        var headEntry = bodyEntry.HasFlag(EqpEntry.BodyShowHead)
            ? GetSingleValue(armor[0].Set, EquipSlot.Head)
            : GetSingleValue(armor[1].Set, EquipSlot.Head);
        var handEntry = bodyEntry.HasFlag(EqpEntry.BodyShowHand)
            ? GetSingleValue(armor[2].Set, EquipSlot.Hands)
            : GetSingleValue(armor[1].Set, EquipSlot.Hands);
        var (legsEntry, legsId) = bodyEntry.HasFlag(EqpEntry.BodyShowLeg)
            ? (GetSingleValue(armor[3].Set, EquipSlot.Legs), 3)
            : (GetSingleValue(armor[1].Set, EquipSlot.Legs), 1);
        var footEntry = legsEntry.HasFlag(EqpEntry.LegsShowFoot)
            ? GetSingleValue(armor[4].Set,      EquipSlot.Feet)
            : GetSingleValue(armor[legsId].Set, EquipSlot.Feet);

        var combined = bodyEntry | headEntry | handEntry | legsEntry | footEntry;
        return PostProcessFeet(PostProcessHands(combined));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private EqpEntry GetSingleValue(PrimaryId id, EquipSlot slot)
        => TryGetValue(new EqpIdentifier(id, slot), out var pair) ? pair.Entry : ExpandedEqpFile.GetDefault(Manager, id) & Eqp.Mask(slot);

    public void Reset()
        => Clear();

    protected override void Dispose(bool _)
        => Clear();

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static EqpEntry PostProcessHands(EqpEntry entry)
    {
        if (!entry.HasFlag(EqpEntry.HandsHideForearm))
            return entry;

        var testFlag = entry.HasFlag(EqpEntry.HandsHideElbow)
            ? entry.HasFlag(EqpEntry.BodyHideGlovesL)
            : entry.HasFlag(EqpEntry.BodyHideGlovesM);
        return testFlag
            ? (entry | EqpEntry._4) & ~EqpEntry.BodyHideGlovesS
            : entry & ~(EqpEntry._4 | EqpEntry.BodyHideGlovesS);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static EqpEntry PostProcessFeet(EqpEntry entry)
    {
        if (!entry.HasFlag(EqpEntry.FeetHideCalf))
            return entry;

        if (entry.HasFlag(EqpEntry.FeetHideKnee) || !entry.HasFlag(EqpEntry._20))
            return entry & ~(EqpEntry.LegsHideBootsS | EqpEntry.LegsHideBootsM);

        return (entry | EqpEntry.LegsHideBootsM) & ~EqpEntry.LegsHideBootsS;
    }
}
