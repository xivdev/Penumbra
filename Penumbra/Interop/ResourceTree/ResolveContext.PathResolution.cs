using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using OtterGui.Text.HelperObjects;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.String;
using Penumbra.String.Classes;
using static Penumbra.Interop.Structs.StructExtensions;
using CharaBase = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase;
using ModelType = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase.ModelType;

namespace Penumbra.Interop.ResourceTree;

internal partial record ResolveContext
{
    private static bool IsEquipmentOrAccessorySlot(uint slotIndex)
        => slotIndex is < 10 or 16 or 17;

    private static bool IsEquipmentSlot(uint slotIndex)
        => slotIndex is < 5 or 16 or 17;

    private unsafe Variant Variant
        => ModelType switch
        {
            ModelType.Monster => (byte)((Monster*)CharacterBase)->Variant,
            _                 => Equipment.Variant,
        };

    private Utf8GamePath ResolveModelPath()
    {
        // Correctness:
        // Resolving a model path through the game's code can use EQDP metadata for human equipment models.
        return ModelType switch
        {
            ModelType.Human when IsEquipmentOrAccessorySlot(SlotIndex) => ResolveEquipmentModelPath(),
            _                                                          => ResolveModelPathNative(),
        };
    }

    private Utf8GamePath ResolveEquipmentModelPath()
    {
        var path = IsEquipmentSlot(SlotIndex)
            ? GamePaths.Mdl.Equipment(Equipment.Set, ResolveModelRaceCode(), SlotIndex.ToEquipSlot())
            : GamePaths.Mdl.Accessory(Equipment.Set, ResolveModelRaceCode(), SlotIndex.ToEquipSlot());
        return Utf8GamePath.FromString(path, out var gamePath) ? gamePath : Utf8GamePath.Empty;
    }

    private GenderRace ResolveModelRaceCode()
        => ResolveEqdpRaceCode(SlotIndex, Equipment.Set);

    private unsafe GenderRace ResolveEqdpRaceCode(uint slotIndex, PrimaryId primaryId)
    {
        if (!IsEquipmentOrAccessorySlot(slotIndex) || ModelType != ModelType.Human)
            return GenderRace.MidlanderMale;

        var characterRaceCode = (GenderRace)((Human*)CharacterBase)->RaceSexId;
        if (characterRaceCode == GenderRace.MidlanderMale)
            return GenderRace.MidlanderMale;

        var accessory = !IsEquipmentSlot(slotIndex);
        if ((ushort)characterRaceCode % 10 != 1 && accessory)
            return GenderRace.MidlanderMale;

        var metaCache = Global.Collection.MetaCache;
        var entry = metaCache?.GetEqdpEntry(characterRaceCode, accessory, primaryId)
         ?? ExpandedEqdpFile.GetDefault(Global.MetaFileManager, characterRaceCode, accessory, primaryId);
        var slot = slotIndex.ToEquipSlot();
        if (entry.ToBits(slot).Item2)
            return characterRaceCode;

        var fallbackRaceCode = characterRaceCode.Fallback();
        if (fallbackRaceCode == GenderRace.MidlanderMale)
            return GenderRace.MidlanderMale;

        entry = metaCache?.GetEqdpEntry(fallbackRaceCode, accessory, primaryId)
         ?? ExpandedEqdpFile.GetDefault(Global.MetaFileManager, fallbackRaceCode, accessory, primaryId);
        if (entry.ToBits(slot).Item2)
            return fallbackRaceCode;

        return GenderRace.MidlanderMale;
    }

    private unsafe Utf8GamePath ResolveModelPathNative()
    {
        var path = CharacterBase->ResolveMdlPathAsByteString(SlotIndex);
        return Utf8GamePath.FromByteString(path, out var gamePath) ? gamePath : Utf8GamePath.Empty;
    }

    private unsafe Utf8GamePath ResolveMaterialPath(Utf8GamePath modelPath, ResourceHandle* imc, byte* mtrlFileName)
    {
        // Safety and correctness:
        // Resolving a material path through the game's code can dereference null pointers for materials that involve IMC metadata.
        return ModelType switch
        {
            ModelType.Human when IsEquipmentOrAccessorySlot(SlotIndex) && mtrlFileName[8] != (byte)'b'
                => ResolveEquipmentMaterialPath(modelPath, imc, mtrlFileName),
            ModelType.DemiHuman => ResolveEquipmentMaterialPath(modelPath, imc, mtrlFileName),
            ModelType.Weapon    => ResolveWeaponMaterialPath(modelPath, imc, mtrlFileName),
            ModelType.Monster   => ResolveEquipmentMaterialPath(modelPath, imc, mtrlFileName),
            _                   => ResolveMaterialPathNative(mtrlFileName),
        };
    }

    [SkipLocalsInit]
    private unsafe Utf8GamePath ResolveEquipmentMaterialPath(Utf8GamePath modelPath, ResourceHandle* imc, byte* mtrlFileName)
    {
        var variant  = ResolveImcData(imc).MaterialId;
        var fileName = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(mtrlFileName);

        Span<byte> pathBuffer = stackalloc byte[CharaBase.PathBufferSize];
        pathBuffer = AssembleMaterialPath(pathBuffer, modelPath.Path.Span, variant, fileName);

        return Utf8GamePath.FromSpan(pathBuffer, MetaDataComputation.None, out var path) ? path.Clone() : Utf8GamePath.Empty;
    }

    [SkipLocalsInit]
    private unsafe Utf8GamePath ResolveWeaponMaterialPath(Utf8GamePath modelPath, ResourceHandle* imc, byte* mtrlFileName)
    {
        var setIdHigh = Equipment.Set.Id / 100;
        // All MCH (20??) weapons' materials C are one and the same
        if (setIdHigh is 20 && mtrlFileName[14] == (byte)'c')
            return Utf8GamePath.FromString(GamePaths.Mtrl.Weapon(2001, 1, 1, "c"), out var path) ? path : Utf8GamePath.Empty;

        // Some offhands share materials with the corresponding mainhand
        if (ItemData.AdaptOffhandImc(Equipment.Set, out var mirroredSetId))
        {
            var variant  = ResolveImcData(imc).MaterialId;
            var fileName = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(mtrlFileName);

            Span<byte> mirroredFileName = stackalloc byte[32];
            mirroredFileName = mirroredFileName[..fileName.Length];
            fileName.CopyTo(mirroredFileName);
            WriteZeroPaddedNumber(mirroredFileName[4..8], mirroredSetId.Id);

            Span<byte> pathBuffer = stackalloc byte[CharaBase.PathBufferSize];
            pathBuffer = AssembleMaterialPath(pathBuffer, modelPath.Path.Span, variant, mirroredFileName);

            var weaponPosition = pathBuffer.IndexOf("/weapon/w"u8);
            if (weaponPosition >= 0)
                WriteZeroPaddedNumber(pathBuffer[(weaponPosition + 9)..(weaponPosition + 13)], mirroredSetId.Id);

            return Utf8GamePath.FromSpan(pathBuffer, MetaDataComputation.None, out var path) ? path.Clone() : Utf8GamePath.Empty;
        }

        return ResolveEquipmentMaterialPath(modelPath, imc, mtrlFileName);
    }

    private unsafe ImcEntry ResolveImcData(ResourceHandle* imc)
    {
        var imcFileData = imc->GetDataSpan();
        if (imcFileData.IsEmpty)
        {
            Penumbra.Log.Warning($"IMC resource handle with path {imc->FileName.AsByteString()} doesn't have a valid data span");
            return default;
        }

        return ImcFile.GetEntry(imcFileData, SlotIndex.ToEquipSlot(), Variant, out _);
    }

    private static Span<byte> AssembleMaterialPath(Span<byte> materialPathBuffer, ReadOnlySpan<byte> modelPath, byte variant,
        ReadOnlySpan<byte> mtrlFileName)
    {
        var modelPosition = modelPath.IndexOf("/model/"u8);
        if (modelPosition < 0)
            return [];

        var baseDirectory = modelPath[..modelPosition];

        var writer = new SpanTextWriter(materialPathBuffer);
        writer.Append(baseDirectory);
        writer.Append("/material/v"u8);
        WriteZeroPaddedNumber(ref writer, 4, variant);
        writer.Append((byte)'/');
        writer.Append(mtrlFileName);
        writer.EnsureNullTerminated();

        return materialPathBuffer[..writer.Position];
    }

    private static void WriteZeroPaddedNumber(ref SpanTextWriter writer, int width, ushort number)
    {
        WriteZeroPaddedNumber(writer.GetRemainingSpan()[..width], number);
        writer.Advance(width);
    }

    private static void WriteZeroPaddedNumber(Span<byte> destination, ushort number)
    {
        for (var i = destination.Length; i-- > 0;)
        {
            destination[i] =  (byte)('0' + number % 10);
            number         /= 10;
        }
    }

    private unsafe Utf8GamePath ResolveMaterialPathNative(byte* mtrlFileName)
    {
        CiByteString? path;
        try
        {
            path = CharacterBase->ResolveMtrlPathAsByteString(SlotIndex, mtrlFileName);
        }
        catch (AccessViolationException)
        {
            Penumbra.Log.Error(
                $"Access violation during attempt to resolve material path\nDraw object: {(nint)CharacterBase:X} (of type {ModelType})\nSlot index: {SlotIndex}\nMaterial file name: {(nint)mtrlFileName:X} ({new string((sbyte*)mtrlFileName)})");
            return Utf8GamePath.Empty;
        }

        return Utf8GamePath.FromByteString(path, out var gamePath) ? gamePath : Utf8GamePath.Empty;
    }

    private Utf8GamePath ResolveSkeletonPath(uint partialSkeletonIndex)
    {
        // Correctness and Safety:
        // Resolving a skeleton path through the game's code can use EST metadata for human skeletons.
        // Additionally, it can dereference null pointers for human equipment skeletons.
        return ModelType switch
        {
            ModelType.Human => ResolveHumanSkeletonPath(partialSkeletonIndex),
            _               => ResolveSkeletonPathNative(partialSkeletonIndex),
        };
    }

    private Utf8GamePath ResolveHumanSkeletonPath(uint partialSkeletonIndex)
    {
        var (raceCode, slot, set) = ResolveHumanSkeletonData(partialSkeletonIndex);
        if (set == 0)
            return Utf8GamePath.Empty;

        var path = GamePaths.Sklb.Customization(raceCode, slot, set);
        return Utf8GamePath.FromString(path, out var gamePath) ? gamePath : Utf8GamePath.Empty;
    }

    private unsafe (GenderRace RaceCode, string Slot, PrimaryId Set) ResolveHumanSkeletonData(uint partialSkeletonIndex)
    {
        var human             = (Human*)CharacterBase;
        var characterRaceCode = (GenderRace)human->RaceSexId;
        switch (partialSkeletonIndex)
        {
            case 0: return (characterRaceCode, "base", 1);
            case 1:
                var faceId    = human->FaceId;
                var tribe     = human->Customize[(int)Dalamud.Game.ClientState.Objects.Enums.CustomizeIndex.Tribe];
                var modelType = human->Customize[(int)Dalamud.Game.ClientState.Objects.Enums.CustomizeIndex.ModelType];
                if (faceId < 201)
                    faceId -= tribe switch
                    {
                        0xB when modelType is 4 => 100,
                        0xE | 0xF               => 100,
                        _                       => 0,
                    };
                return ResolveHumanExtraSkeletonData(characterRaceCode, EstType.Face, faceId);
            case 2:  return ResolveHumanExtraSkeletonData(characterRaceCode, EstType.Hair, human->HairId);
            case 3:  return ResolveHumanEquipmentSkeletonData(EquipSlot.Head, EstType.Head);
            case 4:  return ResolveHumanEquipmentSkeletonData(EquipSlot.Body, EstType.Body);
            default: return (0, string.Empty, 0);
        }
    }

    private unsafe (GenderRace RaceCode, string Slot, PrimaryId Set) ResolveHumanEquipmentSkeletonData(EquipSlot slot, EstType type)
    {
        var human     = (Human*)CharacterBase;
        var equipment = ((CharacterArmor*)&human->Head)[slot.ToIndex()];
        return ResolveHumanExtraSkeletonData(ResolveEqdpRaceCode(slot.ToIndex(), equipment.Set), type, equipment.Set);
    }

    private (GenderRace RaceCode, string Slot, PrimaryId Set) ResolveHumanExtraSkeletonData(GenderRace raceCode, EstType type,
        PrimaryId primary)
    {
        var metaCache = Global.Collection.MetaCache;
        var skeletonSet = metaCache?.GetEstEntry(type, raceCode, primary)
         ?? EstFile.GetDefault(Global.MetaFileManager, type, raceCode, primary);
        return (raceCode, type.ToName(), skeletonSet.AsId);
    }

    private unsafe Utf8GamePath ResolveSkeletonPathNative(uint partialSkeletonIndex)
    {
        var path = CharacterBase->ResolveSklbPathAsByteString(partialSkeletonIndex);
        return Utf8GamePath.FromByteString(path, out var gamePath) ? gamePath : Utf8GamePath.Empty;
    }

    private Utf8GamePath ResolveSkeletonParameterPath(uint partialSkeletonIndex)
    {
        // Correctness and Safety:
        // Resolving a skeleton parameter path through the game's code can use EST metadata for human skeletons.
        // Additionally, it can dereference null pointers for human equipment skeletons.
        return ModelType switch
        {
            ModelType.Human => ResolveHumanSkeletonParameterPath(partialSkeletonIndex),
            _               => ResolveSkeletonParameterPathNative(partialSkeletonIndex),
        };
    }

    private Utf8GamePath ResolveHumanSkeletonParameterPath(uint partialSkeletonIndex)
    {
        var (raceCode, slot, set) = ResolveHumanSkeletonData(partialSkeletonIndex);
        if (set.Id is 0)
            return Utf8GamePath.Empty;

        var path = GamePaths.Skp.Customization(raceCode, slot, set);
        return Utf8GamePath.FromString(path, out var gamePath) ? gamePath : Utf8GamePath.Empty;
    }

    private unsafe Utf8GamePath ResolveSkeletonParameterPathNative(uint partialSkeletonIndex)
    {
        var path = CharacterBase->ResolveSkpPathAsByteString(partialSkeletonIndex);
        return Utf8GamePath.FromByteString(path, out var gamePath) ? gamePath : Utf8GamePath.Empty;
    }

    private Utf8GamePath ResolvePhysicsModulePath(uint partialSkeletonIndex)
    {
        // Correctness and Safety:
        // Resolving a physics module path through the game's code can use EST metadata for human skeletons.
        // Additionally, it can dereference null pointers for human equipment skeletons.
        return ModelType switch
        {
            ModelType.Human => ResolveHumanPhysicsModulePath(partialSkeletonIndex),
            _               => ResolvePhysicsModulePathNative(partialSkeletonIndex),
        };
    }

    private Utf8GamePath ResolveHumanPhysicsModulePath(uint partialSkeletonIndex)
    {
        var (raceCode, slot, set) = ResolveHumanSkeletonData(partialSkeletonIndex);
        if (set.Id is 0)
            return Utf8GamePath.Empty;

        var path = GamePaths.Phyb.Customization(raceCode, slot, set);
        return Utf8GamePath.FromString(path, out var gamePath) ? gamePath : Utf8GamePath.Empty;
    }

    private unsafe Utf8GamePath ResolvePhysicsModulePathNative(uint partialSkeletonIndex)
    {
        var path = CharacterBase->ResolvePhybPathAsByteString(partialSkeletonIndex);
        return Utf8GamePath.FromByteString(path, out var gamePath) ? gamePath : Utf8GamePath.Empty;
    }

    private unsafe Utf8GamePath ResolveMaterialAnimationPath(ResourceHandle* imc)
    {
        var animation = ResolveImcData(imc).MaterialAnimationId;
        if (animation is 0)
            return Utf8GamePath.Empty;

        var path = CharacterBase->ResolveMaterialPapPathAsByteString(SlotIndex, animation);
        return Utf8GamePath.FromByteString(path, out var gamePath) ? gamePath : Utf8GamePath.Empty;
    }

    private unsafe Utf8GamePath ResolveDecalPath(ResourceHandle* imc)
    {
        var decal = ResolveImcData(imc).DecalId;
        if (decal is 0)
            return Utf8GamePath.Empty;

        var path = GamePaths.Tex.EquipDecal(decal);
        return Utf8GamePath.FromString(path, out var gamePath) ? gamePath : Utf8GamePath.Empty;
    }
}
