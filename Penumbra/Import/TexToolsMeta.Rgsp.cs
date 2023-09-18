using Penumbra.GameData.Enums;
using Penumbra.Meta;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Import;

public partial class TexToolsMeta
{
    // Parse a single rgsp file.
    public static TexToolsMeta FromRgspFile(MetaFileManager manager, string filePath, byte[] data, bool keepDefault)
    {
        if (data.Length != 45 && data.Length != 42)
        {
            Penumbra.Log.Error("Error while parsing .rgsp file:\n\tInvalid number of bytes.");
            return Invalid;
        }

        using var s  = new MemoryStream(data);
        using var br = new BinaryReader(s);
        // The first value is a flag that signifies version.
        // If it is byte.max, the following two bytes are the version,
        // otherwise it is version 1 and signifies the sub race instead.
        var flag    = br.ReadByte();
        var version = flag != 255 ? (uint)1 : br.ReadUInt16();

        var ret = new TexToolsMeta(manager, filePath, version);

        // SubRace is offset by one due to Unknown.
        var subRace = (SubRace)(version == 1 ? flag + 1 : br.ReadByte() + 1);
        if (!Enum.IsDefined(typeof(SubRace), subRace) || subRace == SubRace.Unknown)
        {
            Penumbra.Log.Error($"Error while parsing .rgsp file:\n\t{subRace} is not a valid SubRace.");
            return Invalid;
        }

        // Next byte is Gender. 1 is Female, 0 is Male.
        var gender = br.ReadByte();
        if (gender != 1 && gender != 0)
        {
            Penumbra.Log.Error($"Error while parsing .rgsp file:\n\t{gender} is neither Male nor Female.");
            return Invalid;
        }

        // Add the given values to the manipulations if they are not default.
        void Add(RspAttribute attribute, float value)
        {
            var def = CmpFile.GetDefault(manager, subRace, attribute);
            if (keepDefault || value != def)
                ret.MetaManipulations.Add(new RspManipulation(subRace, attribute, value));
        }

        if (gender == 1)
        {
            Add(RspAttribute.FemaleMinSize, br.ReadSingle());
            Add(RspAttribute.FemaleMaxSize, br.ReadSingle());
            Add(RspAttribute.FemaleMinTail, br.ReadSingle());
            Add(RspAttribute.FemaleMaxTail, br.ReadSingle());

            Add(RspAttribute.BustMinX, br.ReadSingle());
            Add(RspAttribute.BustMinY, br.ReadSingle());
            Add(RspAttribute.BustMinZ, br.ReadSingle());
            Add(RspAttribute.BustMaxX, br.ReadSingle());
            Add(RspAttribute.BustMaxY, br.ReadSingle());
            Add(RspAttribute.BustMaxZ, br.ReadSingle());
        }
        else
        {
            Add(RspAttribute.MaleMinSize, br.ReadSingle());
            Add(RspAttribute.MaleMaxSize, br.ReadSingle());
            Add(RspAttribute.MaleMinTail, br.ReadSingle());
            Add(RspAttribute.MaleMaxTail, br.ReadSingle());
        }

        return ret;
    }
}
