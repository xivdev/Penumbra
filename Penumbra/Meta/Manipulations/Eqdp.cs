using Newtonsoft.Json.Linq;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Manipulations;

public readonly record struct EqdpIdentifier(PrimaryId SetId, EquipSlot Slot, GenderRace GenderRace)
    : IMetaIdentifier, IComparable<EqdpIdentifier>
{
    public ModelRace Race
        => GenderRace.Split().Item2;

    public Gender Gender
        => GenderRace.Split().Item1;

    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData> changedItems)
        => identifier.Identify(changedItems, GamePaths.Mdl.Equipment(SetId, GenderRace, Slot));

    public MetaIndex FileIndex()
        => CharacterUtilityData.EqdpIdx(GenderRace, Slot.IsAccessory());

    public override string ToString()
        => $"Eqdp - {SetId} - {Slot.ToName()} - {GenderRace.ToName()}";

    public bool Validate()
    {
        var mask = Eqdp.Mask(Slot);
        if (mask == 0)
            return false;

        if (FileIndex() == (MetaIndex)(-1))
            return false;

        // No check for set id.
        return true;
    }

    public int CompareTo(EqdpIdentifier other)
    {
        var gr = GenderRace.CompareTo(other.GenderRace);
        if (gr != 0)
            return gr;

        var set = SetId.Id.CompareTo(other.SetId.Id);
        if (set != 0)
            return set;

        return Slot.CompareTo(other.Slot);
    }

    public static EqdpIdentifier? FromJson(JObject jObj)
    {
        var gender = jObj["Gender"]?.ToObject<Gender>() ?? Gender.Unknown;
        var race   = jObj["Race"]?.ToObject<ModelRace>() ?? ModelRace.Unknown;
        var setId  = new PrimaryId(jObj["SetId"]?.ToObject<ushort>() ?? 0);
        var slot   = jObj["Slot"]?.ToObject<EquipSlot>() ?? EquipSlot.Unknown;
        var ret    = new EqdpIdentifier(setId, slot, Names.CombinedRace(gender, race));
        return ret.Validate() ? ret : null;
    }

    public JObject AddToJson(JObject jObj)
    {
        var (gender, race) = GenderRace.Split();
        jObj["Gender"]     = gender.ToString();
        jObj["Race"]       = race.ToString();
        jObj["SetId"]      = SetId.Id.ToString();
        jObj["Slot"]       = Slot.ToString();
        return jObj;
    }

    public MetaManipulationType Type
        => MetaManipulationType.Eqdp;
}

public readonly record struct EqdpEntryInternal(bool Material, bool Model)
{
    public byte AsByte
        => (byte)(Material ? Model ? 3 : 1 : Model ? 2 : 0);

    private EqdpEntryInternal((bool, bool) val)
        : this(val.Item1, val.Item2)
    { }

    public EqdpEntryInternal(EqdpEntry entry, EquipSlot slot)
        : this(entry.ToBits(slot))
    { }

    public EqdpEntry ToEntry(EquipSlot slot)
        => Eqdp.FromSlotAndBits(slot, Material, Model);

    public override string ToString()
        => $"Material: {Material}, Model: {Model}";
}
