using Newtonsoft.Json.Linq;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Manipulations;

public readonly record struct GmpIdentifier(PrimaryId SetId) : IMetaIdentifier, IComparable<GmpIdentifier>
{
    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData?> changedItems)
        => identifier.Identify(changedItems, GamePaths.Equipment.Mdl.Path(SetId, GenderRace.MidlanderMale, EquipSlot.Head));

    public MetaIndex FileIndex()
        => MetaIndex.Gmp;

    public override string ToString()
        => $"Gmp - {SetId}";

    public bool Validate()
        // No known conditions.
        => true;

    public int CompareTo(GmpIdentifier other)
        => SetId.Id.CompareTo(other.SetId.Id);

    public static GmpIdentifier? FromJson(JObject jObj)
    {
        var setId = new PrimaryId(jObj["SetId"]?.ToObject<ushort>() ?? 0);
        var ret   = new GmpIdentifier(setId);
        return ret.Validate() ? ret : null;
    }

    public JObject AddToJson(JObject jObj)
    {
        jObj["SetId"] = SetId.Id.ToString();
        return jObj;
    }

    public MetaManipulationType Type
        => MetaManipulationType.Gmp;
}
