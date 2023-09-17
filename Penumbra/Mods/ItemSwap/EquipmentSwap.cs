using Penumbra.Api.Enums;
using Penumbra.GameData;
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
            return new[]
            {
                slot,
            };

        return rFinger
            ? lFinger
                ? new[]
                {
                    EquipSlot.RFinger,
                    EquipSlot.LFinger,
                }
                : new[]
                {
                    EquipSlot.RFinger,
                }
            : lFinger
                ? new[]
                {
                    EquipSlot.LFinger,
                }
                : Array.Empty<EquipSlot>();
    }

    public static EquipItem[] CreateTypeSwap(MetaFileManager manager, IObjectIdentifier identifier, List<Swap> swaps,
        Func<Utf8GamePath, FullPath> redirections, Func<MetaManipulation, MetaManipulation> manips,
        EquipSlot slotFrom, EquipItem itemFrom, EquipSlot slotTo, EquipItem itemTo)
    {
        LookupItem(itemFrom, out var actualSlotFrom, out var idFrom, out var variantFrom);
        LookupItem(itemTo,   out var actualSlotTo,   out var idTo,   out var variantTo);
        if (actualSlotFrom != slotFrom.ToSlot() || actualSlotTo != slotTo.ToSlot())
            throw new ItemSwap.InvalidItemTypeException();

        var (imcFileFrom, variants, affectedItems) = GetVariants(manager, identifier, slotFrom, idFrom, idTo, variantFrom);
        var imcManip      = new ImcManipulation(slotTo, variantTo.Id, idTo.Id, default);
        var imcFileTo     = new ImcFile(manager, imcManip);
        var skipFemale    = false;
        var skipMale      = false;
        var mtrlVariantTo = manips(imcManip.Copy(imcFileTo.GetEntry(ImcFile.PartIndex(slotTo), variantTo.Id))).Imc.Entry.MaterialId;
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

    public static EquipItem[] CreateItemSwap(MetaFileManager manager, IObjectIdentifier identifier, List<Swap> swaps,
        Func<Utf8GamePath, FullPath> redirections, Func<MetaManipulation, MetaManipulation> manips, EquipItem itemFrom,
        EquipItem itemTo, bool rFinger = true, bool lFinger = true)
    {
        // Check actual ids, variants and slots. We only support using the same slot.
        LookupItem(itemFrom, out var slotFrom, out var idFrom, out var variantFrom);
        LookupItem(itemTo,   out var slotTo,   out var idTo,   out var variantTo);
        if (slotFrom != slotTo)
            throw new ItemSwap.InvalidItemTypeException();

        var eqp = CreateEqp(manager, manips, slotFrom, idFrom, idTo);
        if (eqp != null)
            swaps.Add(eqp);

        var gmp = CreateGmp(manager, manips, slotFrom, idFrom, idTo);
        if (gmp != null)
            swaps.Add(gmp);

        var affectedItems = Array.Empty<EquipItem>();
        foreach (var slot in ConvertSlots(slotFrom, rFinger, lFinger))
        {
            (var imcFileFrom, var variants, affectedItems) = GetVariants(manager, identifier, slot, idFrom, idTo, variantFrom);
            var imcManip  = new ImcManipulation(slot, variantTo.Id, idTo, default);
            var imcFileTo = new ImcFile(manager, imcManip);

            var isAccessory = slot.IsAccessory();
            var estType = slot switch
            {
                EquipSlot.Head => EstManipulation.EstType.Head,
                EquipSlot.Body => EstManipulation.EstType.Body,
                _              => (EstManipulation.EstType)0,
            };

            var skipFemale    = false;
            var skipMale      = false;
            var mtrlVariantTo = manips(imcManip.Copy(imcFileTo.GetEntry(ImcFile.PartIndex(slot), variantTo))).Imc.Entry.MaterialId;
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

                    var ownMdl = eqdp?.SwapApplied.Eqdp.Entry.ToBits(slot).Item2 ?? false;
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

    public static MetaSwap? CreateEqdp(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections,
        Func<MetaManipulation, MetaManipulation> manips, EquipSlot slot, GenderRace gr, SetId idFrom,
        SetId idTo, byte mtrlTo)
        => CreateEqdp(manager, redirections, manips, slot, slot, gr, idFrom, idTo, mtrlTo);

    public static MetaSwap? CreateEqdp(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections,
        Func<MetaManipulation, MetaManipulation> manips, EquipSlot slotFrom, EquipSlot slotTo, GenderRace gr, SetId idFrom,
        SetId idTo, byte mtrlTo)
    {
        var (gender, race) = gr.Split();
        var eqdpFrom = new EqdpManipulation(ExpandedEqdpFile.GetDefault(manager, gr, slotFrom.IsAccessory(), idFrom), slotFrom, gender,
            race, idFrom);
        var eqdpTo = new EqdpManipulation(ExpandedEqdpFile.GetDefault(manager, gr, slotTo.IsAccessory(), idTo), slotTo, gender, race,
            idTo);
        var meta = new MetaSwap(manips, eqdpFrom, eqdpTo);
        var (ownMtrl, ownMdl) = meta.SwapApplied.Eqdp.Entry.ToBits(slotFrom);
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
        SetId idFrom, SetId idTo, byte mtrlTo)
        => CreateMdl(manager, redirections, slot, slot, gr, idFrom, idTo, mtrlTo);

    public static FileSwap CreateMdl(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, EquipSlot slotFrom, EquipSlot slotTo,
        GenderRace gr, SetId idFrom, SetId idTo, byte mtrlTo)
    {
        var mdlPathFrom = slotFrom.IsAccessory()
            ? GamePaths.Accessory.Mdl.Path(idFrom, gr, slotFrom)
            : GamePaths.Equipment.Mdl.Path(idFrom, gr, slotFrom);
        var mdlPathTo = slotTo.IsAccessory() ? GamePaths.Accessory.Mdl.Path(idTo, gr, slotTo) : GamePaths.Equipment.Mdl.Path(idTo, gr, slotTo);
        var mdl       = FileSwap.CreateSwap(manager, ResourceType.Mdl, redirections, mdlPathFrom, mdlPathTo);

        foreach (ref var fileName in mdl.AsMdl()!.Materials.AsSpan())
        {
            var mtrl = CreateMtrl(manager, redirections, slotFrom, slotTo, idFrom, idTo, mtrlTo, ref fileName, ref mdl.DataWasChanged);
            if (mtrl != null)
                mdl.ChildSwaps.Add(mtrl);
        }

        return mdl;
    }

    private static void LookupItem(EquipItem i, out EquipSlot slot, out SetId modelId, out Variant variant)
    {
        slot = i.Type.ToSlot();
        if (!slot.IsEquipmentPiece())
            throw new ItemSwap.InvalidItemTypeException();

        modelId = i.ModelId;
        variant = i.Variant;
    }

    private static (ImcFile, Variant[], EquipItem[]) GetVariants(MetaFileManager manager, IObjectIdentifier identifier, EquipSlot slotFrom,
        SetId idFrom, SetId idTo, Variant variantFrom)
    {
        var         entry = new ImcManipulation(slotFrom, variantFrom.Id, idFrom, default);
        var         imc   = new ImcFile(manager, entry);
        EquipItem[] items;
        Variant[]   variants;
        if (idFrom == idTo)
        {
            items = identifier.Identify(idFrom, variantFrom, slotFrom).ToArray();
            variants = new[]
            {
                variantFrom,
            };
        }
        else
        {
            items = identifier.Identify(slotFrom.IsEquipment()
                    ? GamePaths.Equipment.Mdl.Path(idFrom, GenderRace.MidlanderMale, slotFrom)
                    : GamePaths.Accessory.Mdl.Path(idFrom, GenderRace.MidlanderMale, slotFrom)).Select(kvp => kvp.Value).OfType<EquipItem>()
                .ToArray();
            variants = Enumerable.Range(0, imc.Count + 1).Select(i => (Variant)i).ToArray();
        }

        return (imc, variants, items);
    }

    public static MetaSwap? CreateGmp(MetaFileManager manager, Func<MetaManipulation, MetaManipulation> manips, EquipSlot slot, SetId idFrom,
        SetId idTo)
    {
        if (slot is not EquipSlot.Head)
            return null;

        var manipFrom = new GmpManipulation(ExpandedGmpFile.GetDefault(manager, idFrom), idFrom);
        var manipTo   = new GmpManipulation(ExpandedGmpFile.GetDefault(manager, idTo),   idTo);
        return new MetaSwap(manips, manipFrom, manipTo);
    }

    public static MetaSwap CreateImc(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections,
        Func<MetaManipulation, MetaManipulation> manips, EquipSlot slot,
        SetId idFrom, SetId idTo, Variant variantFrom, Variant variantTo, ImcFile imcFileFrom, ImcFile imcFileTo)
        => CreateImc(manager, redirections, manips, slot, slot, idFrom, idTo, variantFrom, variantTo, imcFileFrom, imcFileTo);

    public static MetaSwap CreateImc(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections,
        Func<MetaManipulation, MetaManipulation> manips,
        EquipSlot slotFrom, EquipSlot slotTo, SetId idFrom, SetId idTo,
        Variant variantFrom, Variant variantTo, ImcFile imcFileFrom, ImcFile imcFileTo)
    {
        var entryFrom        = imcFileFrom.GetEntry(ImcFile.PartIndex(slotFrom), variantFrom);
        var entryTo          = imcFileTo.GetEntry(ImcFile.PartIndex(slotTo), variantTo);
        var manipulationFrom = new ImcManipulation(slotFrom, variantFrom.Id, idFrom, entryFrom);
        var manipulationTo   = new ImcManipulation(slotTo,   variantTo.Id,   idTo,   entryTo);
        var imc              = new MetaSwap(manips, manipulationFrom, manipulationTo);

        var decal = CreateDecal(manager, redirections, imc.SwapToModded.Imc.Entry.DecalId);
        if (decal != null)
            imc.ChildSwaps.Add(decal);

        var avfx = CreateAvfx(manager, redirections, idFrom, idTo, imc.SwapToModded.Imc.Entry.VfxId);
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

        var decalPath = GamePaths.Equipment.Decal.Path(decalId);
        return FileSwap.CreateSwap(manager, ResourceType.Tex, redirections, decalPath, decalPath);
    }


    // Example: Abyssos Helm / Body
    public static FileSwap? CreateAvfx(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, SetId idFrom, SetId idTo, byte vfxId)
    {
        if (vfxId == 0)
            return null;

        var vfxPathFrom = GamePaths.Equipment.Avfx.Path(idFrom, vfxId);
        var vfxPathTo   = GamePaths.Equipment.Avfx.Path(idTo,   vfxId);
        var avfx        = FileSwap.CreateSwap(manager, ResourceType.Avfx, redirections, vfxPathFrom, vfxPathTo);

        foreach (ref var filePath in avfx.AsAvfx()!.Textures.AsSpan())
        {
            var atex = CreateAtex(manager, redirections, ref filePath, ref avfx.DataWasChanged);
            avfx.ChildSwaps.Add(atex);
        }

        return avfx;
    }

    public static MetaSwap? CreateEqp(MetaFileManager manager, Func<MetaManipulation, MetaManipulation> manips, EquipSlot slot, SetId idFrom,
        SetId idTo)
    {
        if (slot.IsAccessory())
            return null;

        var eqpValueFrom = ExpandedEqpFile.GetDefault(manager, idFrom);
        var eqpValueTo   = ExpandedEqpFile.GetDefault(manager, idTo);
        var eqpFrom      = new EqpManipulation(eqpValueFrom, slot, idFrom);
        var eqpTo        = new EqpManipulation(eqpValueTo,   slot, idFrom);
        return new MetaSwap(manips, eqpFrom, eqpTo);
    }

    public static FileSwap? CreateMtrl(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, EquipSlot slot, SetId idFrom,
        SetId idTo, byte variantTo, ref string fileName,
        ref bool dataWasChanged)
        => CreateMtrl(manager, redirections, slot, slot, idFrom, idTo, variantTo, ref fileName, ref dataWasChanged);

    public static FileSwap? CreateMtrl(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, EquipSlot slotFrom, EquipSlot slotTo,
        SetId idFrom, SetId idTo, byte variantTo, ref string fileName,
        ref bool dataWasChanged)
    {
        var prefix = slotTo.IsAccessory() ? 'a' : 'e';
        if (!fileName.Contains($"{prefix}{idTo.Id:D4}"))
            return null;

        var folderTo = slotTo.IsAccessory()
            ? GamePaths.Accessory.Mtrl.FolderPath(idTo, variantTo)
            : GamePaths.Equipment.Mtrl.FolderPath(idTo, variantTo);
        var pathTo = $"{folderTo}{fileName}";

        var folderFrom = slotFrom.IsAccessory()
            ? GamePaths.Accessory.Mtrl.FolderPath(idFrom, variantTo)
            : GamePaths.Equipment.Mtrl.FolderPath(idFrom, variantTo);
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

    public static FileSwap CreateTex(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, char prefix, SetId idFrom, SetId idTo,
        ref MtrlFile.Texture texture, ref bool dataWasChanged)
        => CreateTex(manager, redirections, prefix, EquipSlot.Unknown, EquipSlot.Unknown, idFrom, idTo, ref texture, ref dataWasChanged);

    public static FileSwap CreateTex(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, char prefix, EquipSlot slotFrom,
        EquipSlot slotTo, SetId idFrom,
        SetId idTo, ref MtrlFile.Texture texture, ref bool dataWasChanged)
    {
        var path        = texture.Path;
        var addedDashes = false;
        if (texture.DX11)
        {
            var fileName = Path.GetFileName(path);
            if (!fileName.StartsWith("--"))
            {
                path        = path.Replace(fileName, $"--{fileName}");
                addedDashes = true;
            }
        }

        var newPath = ItemSwap.ReplaceAnyId(path, prefix, idFrom);
        newPath = ItemSwap.ReplaceSlot(newPath, slotTo, slotFrom, slotTo != slotFrom);
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
        var path = $"shader/sm5/shpk/{shaderName}";
        return FileSwap.CreateSwap(manager, ResourceType.Shpk, redirections, path, path);
    }

    public static FileSwap CreateAtex(MetaFileManager manager, Func<Utf8GamePath, FullPath> redirections, ref string filePath,
        ref bool dataWasChanged)
    {
        var oldPath = filePath;
        filePath       = ItemSwap.AddSuffix(filePath, ".atex", $"_{Path.GetFileName(filePath).GetStableHashCode():x8}");
        dataWasChanged = true;

        return FileSwap.CreateSwap(manager, ResourceType.Atex, redirections, filePath, oldPath, oldPath);
    }
}
