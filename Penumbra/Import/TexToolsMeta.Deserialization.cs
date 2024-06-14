using Lumina.Extensions;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Import.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Import;

public partial class TexToolsMeta
{
    // Deserialize and check Eqp Entries and add them to the list if they are non-default.
    private void DeserializeEqpEntry(MetaFileInfo metaFileInfo, byte[]? data)
    {
        // Eqp can only be valid for equipment.
        var mask = Eqp.Mask(metaFileInfo.EquipSlot);
        if (data == null || mask == 0)
            return;

        var identifier = new EqpIdentifier(metaFileInfo.PrimaryId, metaFileInfo.EquipSlot);
        var value      = Eqp.FromSlotAndBytes(metaFileInfo.EquipSlot, data) & mask;
        var def        = ExpandedEqpFile.GetDefault(_metaFileManager, metaFileInfo.PrimaryId) & mask;
        if (_keepDefault || def != value)
            MetaManipulations.TryAdd(identifier, value);
    }

    // Deserialize and check Eqdp Entries and add them to the list if they are non-default.
    private void DeserializeEqdpEntries(MetaFileInfo metaFileInfo, byte[]? data)
    {
        if (data == null)
            return;

        var       num    = data.Length / 5;
        using var reader = new BinaryReader(new MemoryStream(data));
        for (var i = 0; i < num; ++i)
        {
            // Use the SE gender/race code.
            var gr        = (GenderRace)reader.ReadUInt32();
            var byteValue = reader.ReadByte();
            if (!gr.IsValid() || !metaFileInfo.EquipSlot.IsEquipment() && !metaFileInfo.EquipSlot.IsAccessory())
                continue;

            var identifier = new EqdpIdentifier(metaFileInfo.PrimaryId, metaFileInfo.EquipSlot, gr);
            var mask = Eqdp.Mask(metaFileInfo.EquipSlot);
            var value = Eqdp.FromSlotAndBits(metaFileInfo.EquipSlot, (byteValue & 1) == 1, (byteValue & 2) == 2) & mask;
            var def = ExpandedEqdpFile.GetDefault(_metaFileManager, gr, metaFileInfo.EquipSlot.IsAccessory(), metaFileInfo.PrimaryId) & mask;
            if (_keepDefault || def != value)
                MetaManipulations.TryAdd(identifier, value);
        }
    }

    // Deserialize and check Gmp Entries and add them to the list if they are non-default.
    private void DeserializeGmpEntry(MetaFileInfo metaFileInfo, byte[]? data)
    {
        if (data == null)
            return;

        var value = GmpEntry.FromTexToolsMeta(data.AsSpan(0, 5));
        var def   = ExpandedGmpFile.GetDefault(_metaFileManager, metaFileInfo.PrimaryId);
        if (_keepDefault || value != def)
            MetaManipulations.TryAdd(new GmpIdentifier(metaFileInfo.PrimaryId), value);
    }

    // Deserialize and check Est Entries and add them to the list if they are non-default.
    private void DeserializeEstEntries(MetaFileInfo metaFileInfo, byte[]? data)
    {
        if (data == null)
            return;

        var       num    = data.Length / 6;
        using var reader = new BinaryReader(new MemoryStream(data));
        for (var i = 0; i < num; ++i)
        {
            var gr    = (GenderRace)reader.ReadUInt16();
            var id    = (PrimaryId)reader.ReadUInt16();
            var value = new EstEntry(reader.ReadUInt16());
            var type = (metaFileInfo.SecondaryType, metaFileInfo.EquipSlot) switch
            {
                (BodySlot.Face, _)  => EstType.Face,
                (BodySlot.Hair, _)  => EstType.Hair,
                (_, EquipSlot.Head) => EstType.Head,
                (_, EquipSlot.Body) => EstType.Body,
                _                   => (EstType)0,
            };
            if (!gr.IsValid() || type == 0)
                continue;

            var identifier = new EstIdentifier(id, type, gr);
            var def        = EstFile.GetDefault(_metaFileManager, type, gr, id);
            if (_keepDefault || def != value)
                MetaManipulations.TryAdd(identifier, value);
        }
    }

    // Deserialize and check IMC Entries and add them to the list if they are non-default.
    // This requires requesting a file from Lumina, which may fail due to TexTools corruption or just not existing.
    // TexTools creates IMC files for off-hand weapon models which may not exist in the game files.
    private void DeserializeImcEntries(MetaFileInfo metaFileInfo, byte[]? data)
    {
        if (data == null)
            return;

        var       num    = data.Length / 6;
        using var reader = new BinaryReader(new MemoryStream(data));
        var       values = reader.ReadStructures<ImcEntry>(num);
        ushort    i      = 0;
        try
        {
            var identifier = new ImcIdentifier(metaFileInfo.PrimaryId, 0, metaFileInfo.PrimaryType, metaFileInfo.SecondaryId,
                metaFileInfo.EquipSlot, metaFileInfo.SecondaryType);
            var file    = new ImcFile(_metaFileManager, identifier);
            var partIdx = ImcFile.PartIndex(identifier.EquipSlot); // Gets turned to unknown for things without equip, and unknown turns to 0.
            foreach (var value in values)
            {
                identifier = identifier with { Variant = (Variant)i };
                var def = file.GetEntry(partIdx, (Variant)i);
                if (_keepDefault || def != value && identifier.Validate())
                    MetaManipulations.TryAdd(identifier, value);

                ++i;
            }
        }
        catch (Exception e)
        {
            Penumbra.Log.Warning(
                $"Could not compute IMC manipulation for {metaFileInfo.PrimaryType} {metaFileInfo.PrimaryId}. This is in all likelihood due to TexTools corrupting your index files.\n"
              + $"If the following error looks like Lumina is having trouble to read an IMC file, please do a do-over in TexTools:\n{e}");
        }
    }
}
