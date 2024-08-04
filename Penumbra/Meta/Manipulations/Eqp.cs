using Newtonsoft.Json.Linq;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Manipulations;

public readonly record struct EqpIdentifier(PrimaryId SetId, EquipSlot Slot) : IMetaIdentifier, IComparable<EqpIdentifier>
{
    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData?> changedItems)
        => identifier.Identify(changedItems, GamePaths.Equipment.Mdl.Path(SetId, GenderRace.MidlanderMale, Slot));

    public MetaIndex FileIndex()
        => MetaIndex.Eqp;

    public override string ToString()
        => $"Eqp - {SetId} - {Slot}";

    public bool Validate()
    {
        var mask = Eqp.Mask(Slot);
        if (mask == 0)
            return false;

        // No check for set id.
        return true;
    }

    public int CompareTo(EqpIdentifier other)
    {
        var set = SetId.Id.CompareTo(other.SetId.Id);
        if (set != 0)
            return set;

        return Slot.CompareTo(other.Slot);
    }

    public static EqpIdentifier? FromJson(JObject jObj)
    {
        var setId = new PrimaryId(jObj["SetId"]?.ToObject<ushort>() ?? 0);
        var slot  = jObj["Slot"]?.ToObject<EquipSlot>() ?? EquipSlot.Unknown;
        var ret   = new EqpIdentifier(setId, slot);
        return ret.Validate() ? ret : null;
    }

    public JObject AddToJson(JObject jObj)
    {
        jObj["SetId"] = SetId.Id.ToString();
        jObj["Slot"]  = Slot.ToString();
        return jObj;
    }

    public MetaManipulationType Type
        => MetaManipulationType.Eqp;
}

public readonly record struct EqpEntryInternal(uint Value)
{
    public EqpEntryInternal(EqpEntry entry, EquipSlot slot)
        : this(GetValue(entry, slot))
    { }

    public EqpEntry ToEntry(EquipSlot slot)
    {
        var (offset, mask) = Eqp.OffsetAndMask(slot);
        return (EqpEntry)((ulong)Value << offset) & mask;
    }

    private static uint GetValue(EqpEntry entry, EquipSlot slot)
    {
        var (offset, mask) = Eqp.OffsetAndMask(slot);
        return (uint)((ulong)(entry & mask) >> offset);
    }

    public override string ToString()
        => Value.ToString("X8");
}
