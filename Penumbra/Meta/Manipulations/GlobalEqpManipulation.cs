using Newtonsoft.Json.Linq;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;

namespace Penumbra.Meta.Manipulations;

public readonly struct GlobalEqpManipulation : IMetaIdentifier
{
    public GlobalEqpType Type      { get; init; }
    public PrimaryId     Condition { get; init; }

    public bool Validate()
    {
        if (!Enum.IsDefined(Type))
            return false;

        if (Type is GlobalEqpType.DoNotHideVieraHats or GlobalEqpType.DoNotHideHrothgarHats)
            return Condition == 0;

        return Condition != 0;
    }

    public JObject AddToJson(JObject jObj)
    {
        jObj[nameof(Type)]      = Type.ToString();
        jObj[nameof(Condition)] = Condition.Id;
        return jObj;
    }

    public static GlobalEqpManipulation? FromJson(JObject? jObj)
    {
        if (jObj == null)
            return null;

        var type      = jObj[nameof(Type)]?.ToObject<GlobalEqpType>() ?? (GlobalEqpType)100;
        var condition = jObj[nameof(Condition)]?.ToObject<PrimaryId>() ?? 0;
        var ret = new GlobalEqpManipulation
        {
            Type      = type,
            Condition = condition,
        };
        return ret.Validate() ? ret : null;
    }


    public bool Equals(GlobalEqpManipulation other)
        => Type == other.Type
         && Condition.Equals(other.Condition);

    public int CompareTo(GlobalEqpManipulation other)
    {
        var typeComp = Type.CompareTo(other);
        return typeComp != 0 ? typeComp : Condition.Id.CompareTo(other.Condition.Id);
    }

    public override bool Equals(object? obj)
        => obj is GlobalEqpManipulation other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine((int)Type, Condition);

    public static bool operator ==(GlobalEqpManipulation left, GlobalEqpManipulation right)
        => left.Equals(right);

    public static bool operator !=(GlobalEqpManipulation left, GlobalEqpManipulation right)
        => !left.Equals(right);

    public override string ToString()
        => $"Global EQP - {Type}{(Condition != 0 ? $" - {Condition.Id}" : string.Empty)}";

    public void AddChangedItems(ObjectIdentification identifier, IDictionary<string, IIdentifiedObjectData?> changedItems)
    {
        var path = Type switch
        {
            GlobalEqpType.DoNotHideEarrings     => GamePaths.Accessory.Mdl.Path(Condition, GenderRace.MidlanderMale, EquipSlot.Ears),
            GlobalEqpType.DoNotHideNecklace     => GamePaths.Accessory.Mdl.Path(Condition, GenderRace.MidlanderMale, EquipSlot.Neck),
            GlobalEqpType.DoNotHideBracelets    => GamePaths.Accessory.Mdl.Path(Condition, GenderRace.MidlanderMale, EquipSlot.Wrists),
            GlobalEqpType.DoNotHideRingR        => GamePaths.Accessory.Mdl.Path(Condition, GenderRace.MidlanderMale, EquipSlot.RFinger),
            GlobalEqpType.DoNotHideRingL        => GamePaths.Accessory.Mdl.Path(Condition, GenderRace.MidlanderMale, EquipSlot.LFinger),
            GlobalEqpType.DoNotHideHrothgarHats => string.Empty,
            GlobalEqpType.DoNotHideVieraHats    => string.Empty,
            _                                   => string.Empty,
        };
        if (path.Length > 0)
            identifier.Identify(changedItems, path);
        else if (Type is GlobalEqpType.DoNotHideVieraHats)
            changedItems["All Hats for Viera"] = null;
        else if (Type is GlobalEqpType.DoNotHideHrothgarHats)
            changedItems["All Hats for Hrothgar"] = null;
    }

    public MetaIndex FileIndex()
        => MetaIndex.Eqp;

    MetaManipulationType IMetaIdentifier.Type
        => MetaManipulationType.GlobalEqp;
}
