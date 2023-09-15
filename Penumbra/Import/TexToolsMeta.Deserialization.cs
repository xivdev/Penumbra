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
        if (data == null || !metaFileInfo.EquipSlot.IsEquipment())
            return;

        var value = Eqp.FromSlotAndBytes(metaFileInfo.EquipSlot, data);
        var def = new EqpManipulation(ExpandedEqpFile.GetDefault(_metaFileManager, metaFileInfo.PrimaryId), metaFileInfo.EquipSlot,
            metaFileInfo.PrimaryId);
        var manip = new EqpManipulation(value, metaFileInfo.EquipSlot, metaFileInfo.PrimaryId);
        if (_keepDefault || def.Entry != manip.Entry)
            MetaManipulations.Add(manip);
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

            var value = Eqdp.FromSlotAndBits(metaFileInfo.EquipSlot, (byteValue & 1) == 1, (byteValue & 2) == 2);
            var def = new EqdpManipulation(
                ExpandedEqdpFile.GetDefault(_metaFileManager, gr, metaFileInfo.EquipSlot.IsAccessory(), metaFileInfo.PrimaryId),
                metaFileInfo.EquipSlot,
                gr.Split().Item1, gr.Split().Item2, metaFileInfo.PrimaryId);
            var manip = new EqdpManipulation(value, metaFileInfo.EquipSlot, gr.Split().Item1, gr.Split().Item2, metaFileInfo.PrimaryId);
            if (_keepDefault || def.Entry != manip.Entry)
                MetaManipulations.Add(manip);
        }
    }

    // Deserialize and check Gmp Entries and add them to the list if they are non-default.
    private void DeserializeGmpEntry(MetaFileInfo metaFileInfo, byte[]? data)
    {
        if (data == null)
            return;

        using var reader = new BinaryReader(new MemoryStream(data));
        var       value  = (GmpEntry)reader.ReadUInt32();
        value.UnknownTotal = reader.ReadByte();
        var def = ExpandedGmpFile.GetDefault(_metaFileManager, metaFileInfo.PrimaryId);
        if (_keepDefault || value != def)
            MetaManipulations.Add(new GmpManipulation(value, metaFileInfo.PrimaryId));
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
            var id    = reader.ReadUInt16();
            var value = reader.ReadUInt16();
            var type = (metaFileInfo.SecondaryType, metaFileInfo.EquipSlot) switch
            {
                (BodySlot.Face, _)  => EstManipulation.EstType.Face,
                (BodySlot.Hair, _)  => EstManipulation.EstType.Hair,
                (_, EquipSlot.Head) => EstManipulation.EstType.Head,
                (_, EquipSlot.Body) => EstManipulation.EstType.Body,
                _                   => (EstManipulation.EstType)0,
            };
            if (!gr.IsValid() || type == 0)
                continue;

            var def = EstFile.GetDefault(_metaFileManager, type, gr, id);
            if (_keepDefault || def != value)
                MetaManipulations.Add(new EstManipulation(gr.Split().Item1, gr.Split().Item2, type, id, value));
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
            var manip = new ImcManipulation(metaFileInfo.PrimaryType, metaFileInfo.SecondaryType, metaFileInfo.PrimaryId,
                metaFileInfo.SecondaryId, i, metaFileInfo.EquipSlot,
                new ImcEntry());
            var def     = new ImcFile(_metaFileManager, manip);
            var partIdx = ImcFile.PartIndex(manip.EquipSlot); // Gets turned to unknown for things without equip, and unknown turns to 0.
            foreach (var value in values)
            {
                if (_keepDefault || !value.Equals(def.GetEntry(partIdx, (Variant) i)))
                {
                    var imc = new ImcManipulation(manip.ObjectType, manip.BodySlot, manip.PrimaryId, manip.SecondaryId, i, manip.EquipSlot,
                        value);
                    if (imc.Validate())
                        MetaManipulations.Add(imc);
                }

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
