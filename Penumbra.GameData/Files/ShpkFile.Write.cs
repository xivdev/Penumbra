using System;
using System.IO;

namespace Penumbra.GameData.Files;

public partial class ShpkFile
{
    public byte[] Write()
    {
        if (SubViewKeys.Length != 2)
            throw new InvalidDataException();

        using var stream  = new MemoryStream();
        using var blobs   = new MemoryStream();
        var       strings = new StringPool(ReadOnlySpan<byte>.Empty);
        using (var w = new BinaryWriter(stream))
        {
            w.Write(ShPkMagic);
            w.Write(Version);
            w.Write(DirectXVersion switch
            {
                DxVersion.DirectX9  => Dx9Magic,
                DxVersion.DirectX11 => Dx11Magic,
                _                   => throw new NotImplementedException(),
            });
            var offsetsPosition = stream.Position;
            w.Write(0u); // Placeholder for file size
            w.Write(0u); // Placeholder for blobs offset
            w.Write(0u); // Placeholder for strings offset
            w.Write((uint)VertexShaders.Length);
            w.Write((uint)PixelShaders.Length);
            w.Write(MaterialParamsSize);
            w.Write((uint)MaterialParams.Length);
            w.Write((uint)Constants.Length);
            w.Write((uint)Samplers.Length);
            w.Write((uint)Uavs.Length);
            w.Write((uint)SystemKeys.Length);
            w.Write((uint)SceneKeys.Length);
            w.Write((uint)MaterialKeys.Length);
            w.Write((uint)Nodes.Length);
            w.Write((uint)Items.Length);

            WriteShaderArray(w, VertexShaders, blobs, strings);
            WriteShaderArray(w, PixelShaders,  blobs, strings);

            foreach (var materialParam in MaterialParams)
            {
                w.Write(materialParam.Id);
                w.Write(materialParam.ByteOffset);
                w.Write(materialParam.ByteSize);
            }

            WriteResourceArray(w, Constants, strings);
            WriteResourceArray(w, Samplers,  strings);
            WriteResourceArray(w, Uavs,      strings);

            foreach (var key in SystemKeys)
            {
                w.Write(key.Id);
                w.Write(key.DefaultValue);
            }

            foreach (var key in SceneKeys)
            {
                w.Write(key.Id);
                w.Write(key.DefaultValue);
            }

            foreach (var key in MaterialKeys)
            {
                w.Write(key.Id);
                w.Write(key.DefaultValue);
            }

            foreach (var key in SubViewKeys)
                w.Write(key.DefaultValue);

            foreach (var node in Nodes)
            {
                if (node.PassIndices.Length != 16
                 || node.SystemKeys.Length != SystemKeys.Length
                 || node.SceneKeys.Length != SceneKeys.Length
                 || node.MaterialKeys.Length != MaterialKeys.Length
                 || node.SubViewKeys.Length != SubViewKeys.Length)
                    throw new InvalidDataException();

                w.Write(node.Id);
                w.Write(node.Passes.Length);
                w.Write(node.PassIndices);
                foreach (var key in node.SystemKeys)
                    w.Write(key);
                foreach (var key in node.SceneKeys)
                    w.Write(key);
                foreach (var key in node.MaterialKeys)
                    w.Write(key);
                foreach (var key in node.SubViewKeys)
                    w.Write(key);
                foreach (var pass in node.Passes)
                {
                    w.Write(pass.Id);
                    w.Write(pass.VertexShader);
                    w.Write(pass.PixelShader);
                }
            }

            foreach (var item in Items)
            {
                w.Write(item.Id);
                w.Write(item.Node);
            }

            w.Write(AdditionalData);

            var blobsOffset = (int)stream.Position;
            blobs.WriteTo(stream);

            var stringsOffset = (int)stream.Position;
            strings.Data.WriteTo(stream);

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
            w.Write((ushort)shader.Uavs.Length);
            w.Write((ushort)0);

            WriteResourceArray(w, shader.Constants, strings);
            WriteResourceArray(w, shader.Samplers,  strings);
            WriteResourceArray(w, shader.Uavs,      strings);
        }
    }
}
