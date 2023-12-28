using Lumina.Extensions;

namespace Penumbra.Import.Models;

// TODO: yeah this goes in gamedata.
public class SklbFile
{
    public byte[] Skeleton;

    public SklbFile(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        var magic = reader.ReadUInt32();
        if (magic != 0x736B6C62)
            throw new InvalidDataException("Invalid sklb magic");

        // todo do this all properly jfc
        var version = reader.ReadUInt32();
        
        var oldHeader = version switch {
            0x31313030 or 0x31313130 or 0x31323030 => true,
            0x31333030 => false,
            _ => throw new InvalidDataException($"Unknown version {version}")
        };

        // Skeleton offset directly follows the layer offset.
        uint skeletonOffset;
        if (oldHeader)
        {
            reader.ReadInt16();
            skeletonOffset = reader.ReadUInt16();
        }
        else
        {
            reader.ReadUInt32();
            skeletonOffset = reader.ReadUInt32();
        }

        reader.Seek(skeletonOffset);
        Skeleton = reader.ReadBytes((int)(reader.BaseStream.Length - skeletonOffset));
    }
}
