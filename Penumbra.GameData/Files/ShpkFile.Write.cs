using System;
using System.IO;

namespace Penumbra.GameData.Files;

public partial class ShpkFile
{
    public byte[] Write()
    {
        using var stream = new MemoryStream();
        using var blobs  = new MemoryStream();
        using (var w = new BinaryWriter(stream))
        {
            w.Write(ShPkMagic);
            w.Write(Unknown1);
            w.Write(DirectXVersion switch
            {
                DXVersion.DirectX9  => DX9Magic,
                DXVersion.DirectX11 => DX11Magic,
                _                   => throw new NotImplementedException(),
            });
            long offsetsPosition = stream.Position;
            w.Write(0u); // Placeholder for file size
            w.Write(0u); // Placeholder for blobs offset
            w.Write(0u); // Placeholder for strings offset
            w.Write((uint)VertexShaders.Length);
            w.Write((uint)PixelShaders.Length);
            w.Write(MaterialParamsSize);
            w.Write((uint)MaterialParams.Length);
            w.Write((uint)Constants.Length);
            w.Write((uint)Samplers.Length);
            w.Write((uint)UnknownA.Length);
            w.Write((uint)UnknownB.Length);
            w.Write((uint)UnknownC.Length);
            w.Write(Unknown2);
            w.Write(Unknown3);
            w.Write(Unknown4);

            WriteShaderArray(w, VertexShaders, blobs, Strings);
            WriteShaderArray(w, PixelShaders, blobs, Strings);

            foreach (var materialParam in MaterialParams)
            {
                w.Write(materialParam.Id);
                w.Write(materialParam.ByteOffset);
                w.Write(materialParam.ByteSize);
            }

            WriteResourceArray(w, Constants, Strings);
            WriteResourceArray(w, Samplers, Strings);

            w.Write(Unknowns.Item1);
            w.Write(Unknowns.Item2);
            w.Write(Unknowns.Item3);

            WriteUInt32PairArray(w, UnknownA);
            WriteUInt32PairArray(w, UnknownB);
            WriteUInt32PairArray(w, UnknownC);

            w.Write(AdditionalData);

            var blobsOffset = (int)stream.Position;
            blobs.WriteTo(stream);

            var stringsOffset = (int)stream.Position;
            Strings.Data.WriteTo(stream);

            var fileSize = (int)stream.Position;

            stream.Seek(offsetsPosition, SeekOrigin.Begin);
            w.Write(fileSize);
            w.Write(blobsOffset);
            w.Write(stringsOffset);
        }

        return stream.ToArray();
    }

    private static void WriteResourceArray(BinaryWriter w, Resource[] array, StringPool strings)
    {
        foreach (var buf in array)
        {
            var (strOffset, strSize) = strings.FindOrAddString(buf.Name);
            w.Write(buf.Id);
            w.Write(strOffset);
            w.Write(strSize);
            w.Write(buf.Slot);
            w.Write(buf.Size);
        }
    }

    private static void WriteShaderArray(BinaryWriter w, Shader[] array, MemoryStream blobs, StringPool strings)
    {
        foreach (var shader in array)
        {
            var blobOffset = (int)blobs.Position;
            blobs.Write(shader.AdditionalHeader);
            blobs.Write(shader.Blob);
            var blobSize = (int)blobs.Position - blobOffset;

            w.Write(blobOffset);
            w.Write(blobSize);
            w.Write((ushort)shader.Constants.Length);
            w.Write((ushort)shader.Samplers.Length);
            w.Write((ushort)shader.UnknownX.Length);
            w.Write((ushort)shader.UnknownY.Length);

            WriteResourceArray(w, shader.Constants, strings);
            WriteResourceArray(w, shader.Samplers, strings);
            WriteResourceArray(w, shader.UnknownX, strings);
            WriteResourceArray(w, shader.UnknownY, strings);
        }
    }

    private static void WriteUInt32PairArray(BinaryWriter w, (uint, uint)[] array)
    {
        foreach (var (first, second) in array)
        {
            w.Write(first);
            w.Write(second);
        }
    }
}
