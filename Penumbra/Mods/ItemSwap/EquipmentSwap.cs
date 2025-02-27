using Penumbra.Api.Enums;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;

namespace Penumbra.Mods.ItemSwap;

public static class EquipmentSwap
{
    private static EquipSlot[] ConvertSlots(EquipSlot slot, bool rFinger, bool lFinger)
    {
        if (slot != EquipSlot.RFinger)
            return [slot];

        return rFinger
            ? lFinger
                ? [EquipSlot.RFinger, EquipSlot.LFinger]
                : [EquipSlot.RFinger]
            : lFinger
                ? [EquipSlot.LFinger]
                : [];
    }

    public static HashSet<EquipItem> CreateTypeSwap(MetaFileManager manager, ObjectIdentification identifier, List<Swap> swaps,
        Func<Utf8GamePath, FullPath> redirections, MetaDictionary manips,
        EquipSlot slotFrom, EquipItem itemFrom, EquipSlot slotTo, EquipItem itemTo)
    {
        LookupItem(itemFrom, out var actualSlotFrom, out var idFrom, out var variantFrom);
        LookupItem(itemTo,   out var actualSlotTo,   out var idTo,   out var variantTo);
        if (actualSlotFrom != slotFrom.ToSlot() || actualSlotTo != slotTo.ToSlot())
            throw new ItemSwap.InvalidItemTypeException();

        var (imcFileFrom, variants, affectedItems) = GetVariants(manager, identifier, slotFrom, idFrom, idTo, variantFrom);
        var imcIdentifierTo = new ImcIdentifier(slotTo, idTo, variantTo);
        var imcFileTo       = new ImcFile(manager, imcIdentifierTo);
        var imcEntry = manips.TryGetValue(imcIdentifierTo, out var entry)
            ? entry
            : imcFileTo.GetEntry(imcIdentifierTo.EquipSlot, imcIdentifierTo.Variant);
        var mtrlVariantTo = imcEntry.MaterialId;
        var skipFemale    = false;
        var skipMale      = false;
        foreach (var gr in Enum.GetValues<GenderRace>())
        {
            switch (gr.Split().Item1)
            {
                case Gender.Male when skipMale:        continue;
                case Gender.Female when skipFemale:    continue;
                case Gender.MaleNpc when skipMale:     continue;
                case Gender.FemaleNpc when skipFemale: continue;
            }

            if (CharacterUtilityData.EqdpIdx(gr, true) < 0)
                continue;

            try
            {
                var eqdp = CreateEqdp(manager, redirections, manips, slotFrom, slotTo, gr, idFrom, idTo, mtrlVariantTo);
                if (eqdp != null)
                    swaps.Add(eqdp);
            }
            catch (ItemSwap.MissingFileException e)
            {
                switch (gr)
                {
                    case GenderRace.MidlanderMale when e.Type == ResourceType.Mdl:
                        skipMale = true;
                        continue;
                    case GenderRace.MidlanderFemale when e.Type == ResourceType.Mdl:
                        skipFemale = true;
                        continue;
                    default: throw;
                }
            }
        }

        foreach (var variant in variants)
        {
            var imc = CreateImc(manager, redirections, manips, slotFrom, slotTo, idFrom, idTo, variant, variantTo, imcFileFrom, imcFileTo);
            swaps.Add(imc);
        }

        return affectedItems;
    }

    public static HashSet<EquipItem> CreateItemSwap(MetaFileManager manager, ObjectIdentification identifier, List<Swap> swaps,
        Func<Utf8GamePath, FullPath> redirections, MetaDictionary manips, EquipItem itemFrom,
        EquipItem itemTo, bool rFinger = true, bool lFinger = true)
    {
        // Check actual ids, variants and slots. We only support using the same slot.
        LookupItem(itemFrom, out var slotFrom, out var idFrom, out var variantFrom);
        LookupItem(itemTo,   out var slotTo,   out var idTo,   out var variantTo);
        if (slotFrom != slotTo)
            throw new ItemSwap.InvalidItemTypeException();

        HashSet<EquipItem> affectedItems = [];
        var                eqp           = CreateEqp(manager, manips, slotFrom, idFrom, idTo);
        if (eqp != null)
        {
            swaps.Add(eqp);
            // Add items affected through multi-slot EQP edits.
            foreach (var child in eqp.ChildSwaps.SelectMany(c => c.WithChildren()).OfType<MetaSwap<EqpIdentifier, EqpEntryInternal>>())
            {
                affectedItems.UnionWith(identifier
                    .Identify(GamePaths.Mdl.Equipment(idFrom, GenderRace.MidlanderMale, child.SwapFromIdentifier.Slot))
                    .Select(kvp => kvp.Value).OfType<IdentifiedItem>().Select(i => i.Item));
            }
        }

        var gmp = CreateGmp(manager, manips, slotFrom, idFrom, idTo);
        if (gmp != null)
            swaps.Add(gmp);

        foreach (var slot in ConvertSlots(slotFrom, rFinger, lFinger))
        {
            var (imcFileFrom, variants, affectedItemsLocal) = GetVariants(manager, identifier, slot, idFrom, idTo, variantFrom);
            affectedItems.UnionWith(affectedItemsLocal);
            var imcIdentifierTo = new ImcIdentifier(slotTo, idTo, variantTo);
            var imcFileTo       = new ImcFile(manager, imcIdentifierTo);
            var imcEntry = manips.TryGetValue(imcIdentifierTo, out var entry)
                ? entry
                : imcFileTo.GetEntry(imcIdentifierTo.EquipSlot, imcIdentifierTo.Variant);
            var mtrlVariantTo = imcEntry.MaterialId;

            var isAccessory = slot.IsAccessory();
            var estType = slot switch
            {
                EquipSlot.Head => EstType.Head,
                EquipSlot.Body => EstType.Body,
                _              => (EstType)0,
            };

            var skipFemale = false;
            var skipMale   = false;
            foreach (var gr in Enum.GetValues<GenderRace>())
            {
                switch (gr.Split().Item1)
                {
                    case Gender.Male when skipMale:        continue;
                    case Gender.Female when skipFemale:    continue;
                    case Gender.MaleNpc when skipMale:     continue;
                    case Gender.FemaleNpc when skipFemale: continue;
                }

                if (CharacterUtilityData.EqdpIdx(gr, isAccessory) < 0)
                    continue;


                try
                {
                    var eqdp = CreateEqdp(manager, redirections, manips, slot, gr, idFrom, idTo, mtrlVariantTo);
                    if (eqdp != null)
                        swaps.Add(eqdp);

                    var ownMdl = eqdp?.SwapToModdedEntry.Model ?? false;
                    var est    = ItemSwap.CreateEst(manager, redirections, manips, estType, gr, idFrom, idTo, ownMdl);
                    if (est != null)
                        swaps.Add(est);
                }
                catch (ItemSwap.MissingFileException e)
                {
                    switch (gr)
                    {
                        case GenderRace.MidlanderMale when e.Type == ResourceType.Mdl:
                            skipMale = true;
                            continue;
                        case GenderRace.MidlanderFemale when e.Type == ResourceType.Mdl:
                            skipFemale = true;
                            continue;
                        default: throw;
                    }
                }
            }

            foreach (var variant in variants)
            {
                var imc = CreateImc(manager, redirections, manips, slot, idFrom, idTo, variant, variantTo, imcFileFrom, imcFileTo);
                swaps.Add(imc);
            }
        }

        return affectedItems;
    }

    public static MetaSwap<EqdpIdentifier, EqdpEntryInternal>? CreateEqdp(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections,
        MetaDictionary manips, EquipSlot slot, GenderRace gr, PrimaryId idFrom, PrimaryId idTo, byte mtrlTo)
        => CreateEqdp(manager, redirections, manips, slot, slot, gr, idFrom, idTo, mtrlTo);

    public static MetaSwap<EqdpIdentifier, EqdpEntryInternal>? CreateEqdp(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections,
        MetaDictionary manips, EquipSlot slotFrom, EquipSlot slotTo, GenderRace gr, PrimaryId idFrom,
        PrimaryId idTo, byte mtrlTo)
    {
        var eqdpFromIdentifier = new EqdpIdentifier(idFrom, slotFrom, gr);
        var eqdpToIdentifier   = new EqdpIdentifier(idTo,   slotTo,   gr);
        var eqdpFromDefault    = new EqdpEntryInternal(ExpandedEqdpFile.GetDefault(manager, eqdpFromIdentifier), slotFrom);
        var eqdpToDefault      = new EqdpEntryInternal(ExpandedEqdpFile.GetDefault(manager, eqdpToIdentifier),   slotTo);
        var meta = new MetaSwap<EqdpIdentifier, EqdpEntryInternal>(i => manips.TryGetValue(i, out var e) ? e : null, eqdpFromIdentifier,
            eqdpFromDefault, eqdpToIdentifier,
            eqdpToDefault);
        var (ownMtrl, ownMdl) = meta.SwapToModdedEntry;
        if (ownMdl)
        {
            var mdl = CreateMdl(manager, redirections, slotFrom, slotTo, gr, idFrom, idTo, mtrlTo);
            meta.ChildSwaps.Add(mdl);
        }
        else if (!ownMtrl && meta.SwapAppliedIsDefault)
        {
            meta = null;
        }

        return meta;
    }

    public static FileSwap CreateMdl(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, EquipSlot slot, GenderRace gr,
        PrimaryId idFrom, PrimaryId idTo, byte mtrlTo)
        => CreateMdl(manager, redirections, slot, slot, gr, idFrom, idTo, mtrlTo);

    public static FileSwap CreateMdl(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, EquipSlot slotFrom, EquipSlot slotTo,
        GenderRace gr, PrimaryId idFrom, PrimaryId idTo, byte mtrlTo)
    {
        var mdlPathFrom = GamePaths.Mdl.Gear(idFrom, gr, slotFrom);
        var mdlPathTo   = GamePaths.Mdl.Gear(idTo,   gr, slotTo);
        var mdl         = FileSwap.CreateSwap(manager, ResourceType.Mdl, redirections, mdlPathFrom, mdlPathTo);

        foreach (ref var fileName in mdl.AsMdl()!.Materials.AsSpan())
        {
            var mtrl = CreateMtrl(manager, redirections, slotFrom, slotTo, idFrom, idTo, mtrlTo, ref fileName, ref mdl.DataWasChanged);
            if (mtrl != null)
                mdl.ChildSwaps.Add(mtrl);
        }

        return mdl;
    }

    private static void LookupItem(EquipItem i, out EquipSlot slot, out PrimaryId modelId, out Variant variant)
    {
        slot = i.Type.ToSlot();
        if (!slot.IsEquipmentPiece())
            throw new ItemSwap.InvalidItemTypeException();

        modelId = i.PrimaryId;
        variant = i.Variant;
    }

    private static (ImcFile, Variant[], HashSet<EquipItem>) GetVariants(MetaFileManager manager, ObjectIdentification identifier,
        EquipSlot slotFrom,
        PrimaryId idFrom, PrimaryId idTo, Variant variantFrom)
    {
        var                ident = new ImcIdentifier(slotFrom, idFrom, variantFrom);
        var                imc   = new ImcFile(manager, ident);
        HashSet<EquipItem> items;
        Variant[]          variants;
        if (idFrom == idTo)
        {
            items    = identifier.Identify(idFrom, 0, variantFrom, slotFrom).ToHashSet();
            variants = [variantFrom];
        }
        else
        {
            items = identifier.Identify(GamePaths.Mdl.Gear(idFrom, GenderRace.MidlanderMale, slotFrom))
                .Select(kvp => kvp.Value).OfType<IdentifiedItem>().Select(i => i.Item)
                .ToHashSet();
            variants = Enumerable.Range(0, imc.Count + 1).Select(i => (Variant)i).ToArray();
        }

        return (imc, variants, items);
    }

    public static MetaSwap<GmpIdentifier, GmpEntry>? CreateGmp(MetaFileManager manager, MetaDictionary manips,
        EquipSlot slot, PrimaryId idFrom, PrimaryId idTo)
    {
        if (slot is not EquipSlot.Head)
            return null;

        var manipFromIdentifier = new GmpIdentifier(idFrom);
        var manipToIdentifier   = new GmpIdentifier(idTo);
        var manipFromDefault    = ExpandedGmpFile.GetDefault(manager, manipFromIdentifier);
        var manipToDefault      = ExpandedGmpFile.GetDefault(manager, manipToIdentifier);
        return new MetaSwap<GmpIdentifier, GmpEntry>(i => manips.TryGetValue(i, out var e) ? e : null, manipFromIdentifier, manipFromDefault,
            manipToIdentifier, manipToDefault);
    }

    public static MetaSwap<ImcIdentifier, ImcEntry> CreateImc(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections,
        MetaDictionary manips, EquipSlot slot, PrimaryId idFrom, PrimaryId idTo, Variant variantFrom, Variant variantTo,
        ImcFile imcFileFrom, ImcFile imcFileTo)
        => CreateImc(manager, redirections, manips, slot, slot, idFrom, idTo, variantFrom, variantTo, imcFileFrom, imcFileTo);

    public static MetaSwap<ImcIdentifier, ImcEntry> CreateImc(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections,
        MetaDictionary manips, EquipSlot slotFrom, EquipSlot slotTo, PrimaryId idFrom, PrimaryId idTo,
        Variant variantFrom, Variant variantTo, ImcFile imcFileFrom, ImcFile imcFileTo)
    {
        var manipFromIdentifier = new ImcIdentifier(slotFrom, idFrom, variantFrom);
        var manipToIdentifier   = new ImcIdentifier(slotTo,   idTo,   variantTo);
        var manipFromDefault    = imcFileFrom.GetEntry(ImcFile.PartIndex(slotFrom), variantFrom);
        var manipToDefault      = imcFileTo.GetEntry(ImcFile.PartIndex(slotTo), variantTo);
        var imc = new MetaSwap<ImcIdentifier, ImcEntry>(i => manips.TryGetValue(i, out var e) ? e : null, manipFromIdentifier, manipFromDefault,
            manipToIdentifier, manipToDefault);

        var decal = CreateDecal(manager, redirections, imc.SwapToModdedEntry.DecalId);
        if (decal != null)
            imc.ChildSwaps.Add(decal);

        var avfx = CreateAvfx(manager, redirections, slotFrom, slotTo, idFrom, idTo, imc.SwapToModdedEntry.VfxId);
        if (avfx != null)
            imc.ChildSwaps.Add(avfx);

        // IMC also controls sound, Example: Dodore Doublet, but unknown what it does?
        // IMC also controls some material animation, Example: The Howling Spirit and The Wailing Spirit, but unknown what it does.
        return imc;
    }

    // Example: Crimson Standard Bracelet
    public static FileSwap? CreateDecal(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, byte decalId)
    {
        if (decalId == 0)
            return null;

        var decalPath = GamePaths.Tex.EquipDecal(decalId);
        return FileSwap.CreateSwap(manager, ResourceType.Tex, redirections, decalPath, decalPath);
    }


    // Example: Abyssos Helm / Body
    public static FileSwap? CreateAvfx(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, EquipSlot slotFrom, EquipSlot slotTo,
        PrimaryId idFrom, PrimaryId idTo,
        byte vfxId)
    {
        if (vfxId == 0)
            return null;

        var vfxPathFrom = GamePaths.Avfx.Path(slotFrom, idFrom, vfxId);
        vfxPathFrom = ItemSwap.ReplaceType(vfxPathFrom, slotFrom, slotTo, idFrom);
        var vfxPathTo = GamePaths.Avfx.Path(slotTo, idTo, vfxId);
        var avfx      = FileSwap.CreateSwap(manager, ResourceType.Avfx, redirections, vfxPathFrom, vfxPathTo);

        foreach (ref var filePath in avfx.AsAvfx()!.Textures.AsSpan())
        {
            var atex = CreateAtex(manager, redirections, slotFrom, slotTo, idFrom, ref filePath, ref avfx.DataWasChanged);
            avfx.ChildSwaps.Add(atex);
        }

        return avfx;
    }

    public static MetaSwap<EqpIdentifier, EqpEntryInternal>? CreateEqp(MetaFileManager manager, MetaDictionary manips, EquipSlot slot,
        PrimaryId idFrom, PrimaryId idTo)
    {
        if (slot.IsAccessory())
            return null;

        var manipFromIdentifier = new EqpIdentifier(idFrom, slot);
        var manipToIdentifier   = new EqpIdentifier(idTo,   slot);
        var manipFromDefault    = new EqpEntryInternal(ExpandedEqpFile.GetDefault(manager, idFrom), slot);
        var manipToDefault      = new EqpEntryInternal(ExpandedEqpFile.GetDefault(manager, idTo),   slot);
        var swap = new MetaSwap<EqpIdentifier, EqpEntryInternal>(i => manips.TryGetValue(i, out var e) ? e : null, manipFromIdentifier,
            manipFromDefault, manipToIdentifier, manipToDefault);
        var entry = swap.SwapToModdedEntry.ToEntry(slot);
        // Add additional EQP entries if the swapped item is a multi-slot item,
        // because those take the EQP entries of their other model-set slots when used.
        switch (slot)
        {
            case EquipSlot.Body:
                if (!entry.HasFlag(EqpEntry.BodyShowLeg)
                 && CreateEqp(manager, manips, EquipSlot.Legs, idFrom, idTo) is { } legChild)
                    swap.ChildSwaps.Add(legChild);
                if (!entry.HasFlag(EqpEntry.BodyShowHead)
                 && CreateEqp(manager, manips, EquipSlot.Head, idFrom, idTo) is { } headChild)
                    swap.ChildSwaps.Add(headChild);
                if (!entry.HasFlag(EqpEntry.BodyShowHand)
                 && CreateEqp(manager, manips, EquipSlot.Hands, idFrom, idTo) is { } handChild)
                    swap.ChildSwaps.Add(handChild);
                break;
            case EquipSlot.Legs:
                if (!entry.HasFlag(EqpEntry.LegsShowFoot)
                 && CreateEqp(manager, manips, EquipSlot.Feet, idFrom, idTo) is { } footChild)
                    swap.ChildSwaps.Add(footChild);
                break;
        }

        return swap;
    }

    public static FileSwap? CreateMtrl(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, EquipSlot slot, PrimaryId idFrom,
        PrimaryId idTo, byte variantTo, ref string fileName,
        ref bool dataWasChanged)
        => CreateMtrl(manager, redirections, slot, slot, idFrom, idTo, variantTo, ref fileName, ref dataWasChanged);

    public static FileSwap? CreateMtrl(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, EquipSlot slotFrom, EquipSlot slotTo,
        PrimaryId idFrom, PrimaryId idTo, byte variantTo, ref string fileName,
        ref bool dataWasChanged)
    {
        var prefix = slotTo.IsAccessory() ? 'a' : 'e';
        if (!fileName.Contains($"{prefix}{idTo.Id:D4}"))
            return null;

        var folderTo = GamePaths.Mtrl.GearFolder(slotTo, idTo, variantTo);
        var pathTo = $"{folderTo}{fileName}";

        var folderFrom  = GamePaths.Mtrl.GearFolder(slotFrom, idFrom, variantTo);
        var newFileName = ItemSwap.ReplaceId(fileName, prefix, idTo, idFrom);
        newFileName = ItemSwap.ReplaceSlot(newFileName, slotTo, slotFrom, slotTo != slotFrom);
        var pathFrom = $"{folderFrom}{newFileName}";

        if (newFileName != fileName)
        {
            fileName       = newFileName;
            dataWasChanged = true;
        }

        var mtrl = FileSwap.CreateSwap(manager, ResourceType.Mtrl, redirections, pathFrom, pathTo);
        var shpk = CreateShader(manager, redirections, ref mtrl.AsMtrl()!.ShaderPackage.Name, ref mtrl.DataWasChanged);
        mtrl.ChildSwaps.Add(shpk);

        foreach (ref var texture in mtrl.AsMtrl()!.Textures.AsSpan())
        {
            var tex = CreateTex(manager, redirections, prefix, slotFrom, slotTo, idFrom, idTo, ref texture, ref mtrl.DataWasChanged);
            mtrl.ChildSwaps.Add(tex);
        }

        return mtrl;
    }

    public static FileSwap CreateTex(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, char prefix, PrimaryId idFrom,
        PrimaryId idTo, ref MtrlFile.Texture texture, ref bool dataWasChanged)
        => CreateTex(manager, redirections, prefix, EquipSlot.Unknown, EquipSlot.Unknown, idFrom, idTo, ref texture, ref dataWasChanged);

    public static FileSwap CreateTex(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, char prefix, EquipSlot slotFrom,
        EquipSlot slotTo, PrimaryId idFrom, PrimaryId idTo, ref MtrlFile.Texture texture, ref bool dataWasChanged)
    {
        var addedDashes = GamePaths.Tex.HandleDx11Path(texture, out var path);
        var newPath     = ItemSwap.ReplaceAnyId(path, prefix, idFrom);
        newPath = ItemSwap.ReplaceSlot(newPath, slotTo, slotFrom, slotTo != slotFrom);
        newPath = ItemSwap.ReplaceType(newPath, slotFrom, slotTo, idFrom);
        newPath = ItemSwap.AddSuffix(newPath, ".tex", $"_{Path.GetFileName(texture.Path).GetStableHashCode():x8}");
        if (newPath != path)
        {
            texture.Path   = addedDashes ? newPath.Replace("--", string.Empty) : newPath;
            dataWasChanged = true;
        }

        return FileSwap.CreateSwap(manager, ResourceType.Tex, redirections, newPath, path, path);
    }

    public static FileSwap CreateShader(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, ref string shaderName,
        ref bool dataWasChanged)
    {
        var path = GamePaths.Shader(shaderName);
        return FileSwap.CreateSwap(manager, ResourceType.Shpk, redirections, path, path);
    }

    public static FileSwap CreateAtex(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, EquipSlot slotFrom, EquipSlot slotTo,
        PrimaryId idFrom, ref string filePath, ref bool dataWasChanged)
    {
        var oldPath = filePath;
        filePath       = ItemSwap.AddSuffix(filePath, ".atex", $"_{Path.GetFileName(filePath).GetStableHashCode():x8}");
        filePath       = ItemSwap.ReplaceType(filePath, slotFrom, slotTo, idFrom);
        dataWasChanged = true;

        return FileSwap.CreateSwap(manager, ResourceType.Atex, redirections, filePath, oldPath, oldPath);
    }
}
