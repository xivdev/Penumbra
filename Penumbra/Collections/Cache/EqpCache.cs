using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Collections.Cache;

public sealed class EqpCache(MetaFileManager manager, ModCollection collection) : MetaCacheBase<EqpIdentifier, EqpEntry>(manager, collection)
{
    public unsafe EqpEntry GetValues(CharacterArmor* armor)
        => GetSingleValue(armor[0].Set,  EquipSlot.Head)
          | GetSingleValue(armor[1].Set, EquipSlot.Body)
          | GetSingleValue(armor[2].Set, EquipSlot.Hands)
          | GetSingleValue(armor[3].Set, EquipSlot.Legs)
          | GetSingleValue(armor[4].Set, EquipSlot.Feet);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private EqpEntry GetSingleValue(PrimaryId id, EquipSlot slot)
        => TryGetValue(new EqpIdentifier(id, slot), out var pair) ? pair.Entry : ExpandedEqpFile.GetDefault(Manager, id) & Eqp.Mask(slot);

    public void Reset()
        => Clear();

    protected override void Dispose(bool _)
        => Clear();
}
