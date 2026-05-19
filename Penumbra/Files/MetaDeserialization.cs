using System.Text.Json;
using ImSharp;
using Luna;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files.AtchStructs;
using Penumbra.GameData.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods.SubMods;

namespace Penumbra.Files;

public static class MetaDeserialization
{
    private static readonly ThreadLocal<WeakReference<IModDataContainer>> CurrentContainer =
        new(() => new WeakReference<IModDataContainer>(null!));

    public static void SetCurrentContainer(IModDataContainer? container)
        => CurrentContainer.Value!.SetTarget(container!);

    public static MetaDictionary ReadMetaDictionary(ref Utf8JsonReader reader)
    {
        var ret = new MetaDictionary();
        // Null, empty.
        if (reader.TokenType is JsonTokenType.Null)
            return ret;

        if (reader.TokenType is not JsonTokenType.StartArray)
            throw new JsonException($"Meta Dictionary must be an array but starts with {reader.TokenType}.");

        var arrayReader = reader.CreateObjectReader();
        var container   = CurrentContainer.IsValueCreated && CurrentContainer.Value!.TryGetTarget(out var c) ? c : null;
        var objectCount = 0;

        while (arrayReader.Read(ref reader))
        {
            // Invalid.
            if (reader.TokenType is not JsonTokenType.StartObject)
                throw new JsonException($"Every meta entry must be an object, but encountered {reader.TokenType}.");

            var objectReader = reader.CreateObjectReader();
            ++objectCount;

            // Try to get the type. Skip the respective object if it has an invalid or no type, but keep reading the dictionary.
            switch (reader.TryPeekEnumProperty("Type"u8, out MetaManipulationType type))
            {
                case JsonFunctions.PeekError.Invalid:
                    if (container is not null)
                        Penumbra.Log.Warning(
                            $"Invalid manipulation type encountered in {container.Mod.Name} - {container.GetFullName()}, manipulation {objectCount}.");
                    else
                        Penumbra.Log.Warning($"Invalid manipulation type encountered in manipulation {objectCount}.");

                    while (objectReader.Read(ref reader))
                        ; // Skip current object.
                    continue;
                case JsonFunctions.PeekError.Missing:
                    if (container is not null)
                        Penumbra.Log.Warning(
                            $"Manipulation without type encountered in {container.Mod.Name} - {container.GetFullName()}, manipulation {objectCount}.");
                    else
                        Penumbra.Log.Warning($"Manipulation without type encountered in manipulation {objectCount}.");

                    while (objectReader.Read(ref reader))
                        ; // Skip current object.
                    continue;
                // Malformed JSON can not be ignored, throw.
                case JsonFunctions.PeekError.Malformed:
                    if (container is not null)
                        throw new JsonException(
                            $"Meta Dictionary with malformed JSON encountered in {container.Mod.Name} - {container.GetFullName()}.");

                    throw new JsonException("Meta Dictionary with malformed JSON encountered.");
            }

            try
            {
                AddSingleManipulation(objectReader, ref reader, ret, type);
            }
            catch
            {
                // TODO
            }
        }

        return ret;
    }

    public static EqpIdentifier? ReadEqp(Utf8JsonObjectReader objectReader, ref Utf8JsonReader j, out EqpEntry? entry)
    {
        entry = null;
        var setId = PrimaryId.Zero;
        var slot  = EquipSlot.Unknown;

        while (objectReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
            {
                j.SkipCurrentObject();
                return null;
            }

            if (j.EnumProperty("Slot"u8, out slot))
                continue;

            if (j.NumberProperty("SetId"u8, out ushort i))
                setId = new PrimaryId(i);
            else if (j.NumberProperty("Entry"u8, out ulong e))
                entry = (EqpEntry)e;
            else
                j.Skip();
        }

        var ret = new EqpIdentifier(setId, slot);
        return ret.Validate() ? ret : null;
    }

    public static EqdpIdentifier? ReadEqdp(Utf8JsonObjectReader objectReader, ref Utf8JsonReader j, out EqdpEntry? entry)
    {
        entry = null;
        var gender = Gender.Unknown;
        var race   = ModelRace.Unknown;
        var setId  = PrimaryId.Zero;
        var slot   = EquipSlot.Unknown;

        while (objectReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
            {
                j.SkipCurrentObject();
                return null;
            }

            if (j.EnumProperty("Gender"u8, out gender))
                continue;
            if (j.EnumProperty("Race"u8, out race))
                continue;
            if (j.EnumProperty("Slot"u8, out slot))
                continue;

            if (j.NumberProperty("SetId"u8, out ushort i))
                setId = new PrimaryId(i);
            else if (j.NumberProperty("Entry"u8, out ushort e))
                entry = (EqdpEntry)e;
            else
                j.Skip();
        }

        var ret = new EqdpIdentifier(setId, slot, Names.CombinedRace(gender, race));
        return ret.Validate() ? ret : null;
    }

    public static EstIdentifier? ReadEst(Utf8JsonObjectReader objectReader, ref Utf8JsonReader j, out EstEntry? entry)
    {
        entry = null;
        var gender = Gender.Unknown;
        var race   = ModelRace.Unknown;
        var setId  = PrimaryId.Zero;
        var slot   = default(EstType);

        while (objectReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
            {
                j.SkipCurrentObject();
                return null;
            }

            if (j.EnumProperty("Gender"u8, out gender))
                continue;
            if (j.EnumProperty("Race"u8, out race))
                continue;
            if (j.EnumProperty("Slot"u8, out slot))
                continue;

            if (j.NumberProperty("SetId"u8, out ushort i))
                setId = new PrimaryId(i);
            else if (j.NumberProperty("Entry"u8, out ushort e))
                entry = new EstEntry(e);
            else
                j.Skip();
        }

        var ret = new EstIdentifier(setId, slot, Names.CombinedRace(gender, race));
        return ret.Validate() ? ret : null;
    }

    public static GmpEntry? ReadGmpEntry(Utf8JsonObjectReader entryReader, ref Utf8JsonReader j)
    {
        var    enabled   = false;
        var    animated  = false;
        ushort rotationA = 0;
        ushort rotationB = 0;
        ushort rotationC = 0;
        byte   unknownA  = 0;
        byte   unknownB  = 0;

        while (entryReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
            {
                j.SkipCurrentObject();
                return null;
            }

            if (j.BoolProperty("Enabled"u8, out enabled))
                continue;
            if (j.BoolProperty("Animated"u8, out animated))
                continue;
            if (j.NumberProperty("RotationA"u8, out rotationA))
                continue;
            if (j.NumberProperty("RotationB"u8, out rotationB))
                continue;
            if (j.NumberProperty("RotationC"u8, out rotationC))
                continue;
            if (j.NumberProperty("UnknownA"u8, out unknownA))
                continue;
            if (j.NumberProperty("UnknownB"u8, out unknownB))
                continue;

            j.Skip();
        }

        return new GmpEntry
        {
            Enabled   = enabled,
            Animated  = animated,
            RotationA = rotationA,
            RotationB = rotationB,
            RotationC = rotationC,
            UnknownA  = unknownA,
            UnknownB  = unknownB,
        };
    }

    public static GmpIdentifier? ReadGmp(Utf8JsonObjectReader objectReader, ref Utf8JsonReader j, out GmpEntry? entry)
    {
        entry = null;
        var setId = PrimaryId.Zero;

        while (objectReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
            {
                j.SkipCurrentObject();
                return null;
            }

            if (j.NumberProperty("SetId"u8, out ushort i))
                setId = new PrimaryId(i);
            else if (j.ObjectProperty("Entry"u8, out var entryReader))
                entry = ReadGmpEntry(entryReader, ref j);
            else
                j.Skip();
        }

        var ret = new GmpIdentifier(setId);
        return ret.Validate() ? ret : null;
    }

    public static RspIdentifier? ReadRsp(Utf8JsonObjectReader objectReader, ref Utf8JsonReader j, out RspEntry? entry)
    {
        entry = null;
        var subRace   = SubRace.Unknown;
        var attribute = RspAttribute.NumAttributes;

        while (objectReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
            {
                j.SkipCurrentObject();
                return null;
            }

            if (j.EnumProperty("SubRace"u8, out subRace))
                continue;
            if (j.EnumProperty("Attribute"u8, out attribute))
                continue;

            if (j.NumberProperty("Entry"u8, out float e))
                entry = new RspEntry(e);
            else
                j.Skip();
        }

        var ret = new RspIdentifier(subRace, attribute);
        return ret.Validate() ? ret : null;
    }

    public static AtchEntry? ReadAtchEntry(Utf8JsonObjectReader entryReader, ref Utf8JsonReader j)
    {
        var ret = new AtchEntry();

        while (entryReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
            {
                j.SkipCurrentObject();
                return null;
            }

            if (j.StringProperty("Bone"u8, out StringU8 bone))
            {
                if (bone.Length is not 0 && ret.SetBoneName(bone))
                    continue;

                j.SkipCurrentObject();
                return null;
            }

            if (j.NumberProperty("Scale"u8, out ret.Scale))
                continue;

            if (j.NumberProperty("OffsetX"u8, out ret.OffsetX))
                continue;
            if (j.NumberProperty("OffsetY"u8, out ret.OffsetY))
                continue;
            if (j.NumberProperty("OffsetZ"u8, out ret.OffsetZ))
                continue;
            if (j.NumberProperty("RotationX"u8, out ret.RotationX))
                continue;
            if (j.NumberProperty("RotationY"u8, out ret.RotationY))
                continue;
            if (j.NumberProperty("RotationZ"u8, out ret.RotationZ))
                continue;

            j.Skip();
        }

        if (ret.Bone.Length is 0)
            return null;

        return ret;
    }


    public static AtchIdentifier? ReadAtch(Utf8JsonObjectReader objectReader, ref Utf8JsonReader j, out AtchEntry? entry)
    {
        entry = null;
        var    gender = Gender.Unknown;
        var    race   = ModelRace.Unknown;
        var    type   = AtchType.Unknown;
        ushort index  = 0;

        while (objectReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
            {
                j.SkipCurrentObject();
                return null;
            }

            if (j.EnumProperty("Gender"u8, out gender))
                continue;
            if (j.EnumProperty("Race"u8, out race))
                continue;
            if (j.NumberProperty("Index"u8, out index))
                continue;

            if (j.StringProperty("Type"u8, out StringU8 value))
                type = AtchType.FromString(value);
            else if (j.ObjectProperty("Entry"u8, out var entryReader))
                entry = ReadAtchEntry(entryReader, ref j);
            else
                j.Skip();
        }

        var ret = new AtchIdentifier(type, Names.CombinedRace(gender, race), index);
        return ret.Validate() ? ret : null;
    }

    public static ShpIdentifier? ReadShp(Utf8JsonObjectReader objectReader, ref Utf8JsonReader j, out ShpEntry? entry)
    {
        entry = null;
        ShapeAttributeString shape               = default;
        var                  slot                = HumanSlot.Unknown;
        PrimaryId?           id                  = null;
        var                  connectorCondition  = ShapeConnectorCondition.None;
        var                  genderRaceCondition = GenderRace.Unknown;

        while (objectReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
            {
                j.SkipCurrentObject();
                return null;
            }

            if (j.StringProperty("Shape"u8, out StringU8 shp))
            {
                if (ShapeAttributeString.TryRead(shp, out shape))
                    continue;

                j.SkipCurrentObject();
                return null;
            }

            if (j.EnumProperty("Slot"u8, out slot))
                continue;
            if (j.EnumProperty("ConnectorCondition"u8, out connectorCondition))
                continue;
            if (j.EnumProperty("GenderRaceCondition"u8, out genderRaceCondition))
                continue;

            if (j.NumberProperty("Id"u8, out ushort i))
                id = i;
            else if (j.BoolProperty("Entry"u8, out var e))
                entry = new ShpEntry(e);
            else
                j.Skip();
        }

        if (shape.Length is 0)
            return null;

        var ret = new ShpIdentifier(slot, id, shape, connectorCondition, genderRaceCondition);
        return ret.Validate() ? ret : null;
    }

    public static AtrIdentifier? ReadAtr(Utf8JsonObjectReader objectReader, ref Utf8JsonReader j, out AtrEntry? entry)
    {
        entry = null;
        ShapeAttributeString attribute           = default;
        var                  slot                = HumanSlot.Unknown;
        PrimaryId?           id                  = null;
        var                  genderRaceCondition = GenderRace.Unknown;

        while (objectReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
            {
                j.SkipCurrentObject();
                return null;
            }

            if (j.StringProperty("Attribute"u8, out StringU8 atr))
            {
                if (ShapeAttributeString.TryRead(atr, out attribute))
                    continue;

                j.SkipCurrentObject();
                return null;
            }

            if (j.EnumProperty("Slot"u8, out slot))
                continue;
            if (j.EnumProperty("GenderRaceCondition"u8, out genderRaceCondition))
                continue;

            if (j.NumberProperty("Id"u8, out ushort i))
                id = i;
            else if (j.BoolProperty("Entry"u8, out var e))
                entry = new AtrEntry(e);
            else
                j.Skip();
        }

        if (attribute.Length is 0)
            return null;

        var ret = new AtrIdentifier(slot, id, attribute, genderRaceCondition);
        return ret.Validate() ? ret : null;
    }

    public static ImcEntry? ReadImcEntry(Utf8JsonObjectReader entryReader, ref Utf8JsonReader j)
    {
        byte   materialId          = 0;
        byte   decalId             = 0;
        byte   vfxId               = 0;
        byte   materialAnimationId = 0;
        byte   soundId             = 0;
        ushort attributeMask       = 0;

        while (entryReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
            {
                j.SkipCurrentObject();
                return null;
            }

            if (j.NumberProperty("MaterialId"u8, out materialId))
                continue;
            if (j.NumberProperty("DecalId"u8, out decalId))
                continue;
            if (j.NumberProperty("VfxId"u8, out vfxId))
                continue;
            if (j.NumberProperty("MaterialAnimationId"u8, out materialAnimationId))
                continue;
            if (j.NumberProperty("AttributeMask"u8, out attributeMask))
                continue;
            if (j.NumberProperty("SoundId"u8, out decalId))
                continue;

            j.Skip();
        }

        return new ImcEntry(materialId, decalId, attributeMask, soundId, vfxId, materialAnimationId);
    }

    public static ImcIdentifier? ReadImc(Utf8JsonObjectReader objectReader, ref Utf8JsonReader j, out ImcEntry? entry)
    {
        entry = null;
        var    objectType   = ObjectType.Unknown;
        var    primaryId    = PrimaryId.Zero;
        var    variant      = Variant.Zero;
        var    secondaryId  = SecondaryId.Zero;
        var    equipSlot    = EquipSlot.Unknown;
        ushort variantParse = 0;
        while (objectReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
            {
                j.SkipCurrentObject();
                return null;
            }

            if (j.EnumProperty("ObjectType"u8, out objectType))
                continue;
            if (j.EnumProperty("EquipSlot"u8, out equipSlot))
                continue;

            if (j.NumberProperty("PrimaryId"u8, out ushort i1))
                primaryId = new PrimaryId(i1);
            else if (j.NumberProperty("Variant"u8, out variantParse))
                variant = new Variant((byte)variantParse);
            else if (j.NumberProperty("SecondaryId"u8, out ushort i2))
                secondaryId = new SecondaryId(i2);
            else if (j.ObjectProperty("Entry"u8, out var entryReader))
                entry = ReadImcEntry(entryReader, ref j);
            else
                j.Skip();
        }

        if (variantParse > byte.MaxValue)
            return null;

        var ret = new ImcIdentifier(primaryId, variant, objectType, secondaryId, equipSlot,
            objectType is ObjectType.Monster or ObjectType.Weapon ? BodySlot.Body : BodySlot.Unknown);
        return ret.Validate() ? ret : null;
    }

    public static GlobalEqpManipulation? ReadGeqp(Utf8JsonObjectReader objectReader, ref Utf8JsonReader j)
    {
        var type      = (GlobalEqpType)100;
        var condition = PrimaryId.Zero;

        while (objectReader.Read(ref j))
        {
            if (j.TokenType is not JsonTokenType.PropertyName)
            {
                j.SkipCurrentObject();
                return null;
            }

            if (j.EnumProperty("Type"u8, out type))
                continue;

            if (j.NumberProperty("Condition"u8, out ushort i))
                condition = new PrimaryId(i);
            else
                j.Skip();
        }

        var ret = new GlobalEqpManipulation
        {
            Type      = type,
            Condition = condition,
        };
        return ret.Validate() ? ret : null;
    }

    private static void AddSingleManipulation(Utf8JsonObjectReader objectReader, ref Utf8JsonReader reader, MetaDictionary ret,
        MetaManipulationType type)
    {
        while (objectReader.Read(ref reader))
        {
            if (reader.TokenType is not JsonTokenType.PropertyName)
                throw new JsonException("Property name expected.");

            if (reader.ObjectProperty("Manipulation"u8, out var manipulationReader))
            {
                switch (type)
                {
                    case MetaManipulationType.Imc
                        when CheckIdentifier("IMC", ReadImc(manipulationReader, ref reader, out var e), e, out var identifier, out var entry):
                        ret.TryAdd(identifier, entry);
                        break;
                    case MetaManipulationType.Eqdp
                        when CheckIdentifier("EQDP", ReadEqdp(manipulationReader, ref reader, out var e), e, out var identifier, out var entry):
                        ret.TryAdd(identifier, entry);
                        break;
                    case MetaManipulationType.Eqp
                        when CheckIdentifier("EQP", ReadEqp(manipulationReader, ref reader, out var e), e, out var identifier, out var entry):
                        ret.TryAdd(identifier, entry);
                        break;
                    case MetaManipulationType.Est
                        when CheckIdentifier("EST", ReadEst(manipulationReader, ref reader, out var e), e, out var identifier, out var entry):
                        ret.TryAdd(identifier, entry);
                        break;
                    case MetaManipulationType.Gmp
                        when CheckIdentifier("GMP", ReadGmp(manipulationReader, ref reader, out var e), e, out var identifier, out var entry):
                        ret.TryAdd(identifier, entry);
                        break;
                    case MetaManipulationType.Rsp
                        when CheckIdentifier("RSP", ReadRsp(manipulationReader, ref reader, out var e), e, out var identifier, out var entry):
                        ret.TryAdd(identifier, entry);
                        break;
                    case MetaManipulationType.Atch
                        when CheckIdentifier("ATCH", ReadAtch(manipulationReader, ref reader, out var e), e, out var identifier, out var entry):
                        ret.TryAdd(identifier, entry);
                        break;
                    case MetaManipulationType.Shp
                        when CheckIdentifier("SHP", ReadShp(manipulationReader, ref reader, out var e), e, out var identifier, out var entry):
                        ret.TryAdd(identifier, entry);
                        break;
                    case MetaManipulationType.Atr
                        when CheckIdentifier("ATR", ReadAtr(manipulationReader, ref reader, out var e), e, out var identifier, out var entry):
                        ret.TryAdd(identifier, entry);
                        break;
                    case MetaManipulationType.GlobalEqp
                        when CheckIdentifier("Global EQP", ReadGeqp(manipulationReader, ref reader), (int?)0, out var identifier, out _):
                        ret.TryAdd(identifier);
                        break;
                }
            }
            else
            {
                // Skip other properties.
                reader.Skip();
            }
        }
    }

    private static bool CheckIdentifier<TIdentifier, TEntry>(string type, TIdentifier? i, TEntry? e, out TIdentifier identifier,
        out TEntry entry) where TIdentifier : struct where TEntry : struct
    {
        if (i.HasValue)
            if (e.HasValue)
            {
                identifier = i.Value;
                entry      = e.Value;
                return true;
            }

        if (CurrentContainer.IsValueCreated && CurrentContainer.Value!.TryGetTarget(out var container))
            Penumbra.Log.Warning($"Invalid {type} Manipulation encountered in {container.Mod.Name} - {container.GetFullName()}.");
        else
            Penumbra.Log.Warning($"Invalid {type} Manipulation encountered.");
        identifier = default;
        entry      = default;
        return false;
    }
}
