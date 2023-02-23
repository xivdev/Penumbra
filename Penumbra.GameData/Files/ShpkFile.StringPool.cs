using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Penumbra.GameData.Files;

public partial class ShpkFile
{
    public class StringPool
    {
        public MemoryStream Data;
        public List<int>    StartingOffsets;

        public StringPool(ReadOnlySpan<byte> bytes)
        {
            Data = new MemoryStream();
            Data.Write(bytes);
            StartingOffsets = new List<int>
            {
                0,
            };
            for (var i = 0; i < bytes.Length; ++i)
            {
                if (bytes[i] == 0)
                    StartingOffsets.Add(i + 1);
            }

            if (StartingOffsets[^1] == bytes.Length)
                StartingOffsets.RemoveAt(StartingOffsets.Count - 1);
            else
                Data.WriteByte(0);
        }

        public string GetString(int offset, int size)
            => Encoding.UTF8.GetString(Data.GetBuffer().AsSpan().Slice(offset, size));

        public string GetNullTerminatedString(int offset)
        {
            var str  = Data.GetBuffer().AsSpan()[offset..];
            var size = str.IndexOf((byte)0);
            if (size >= 0)
                str = str[..size];
            return Encoding.UTF8.GetString(str);
        }

        public (int, int) FindOrAddString(string str)
        {
            var dataSpan = Data.GetBuffer().AsSpan();
            var bytes    = Encoding.UTF8.GetBytes(str);
            foreach (var offset in StartingOffsets)
            {
                if (offset + bytes.Length > Data.Length)
                    break;

                var strSpan = dataSpan[offset..];
                var match   = true;
                for (var i = 0; i < bytes.Length; ++i)
                {
                    if (strSpan[i] != bytes[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (match && strSpan[bytes.Length] == 0)
                    return (offset, bytes.Length);
            }

            Data.Seek(0L, SeekOrigin.End);
            var newOffset = (int)Data.Position;
            StartingOffsets.Add(newOffset);
            Data.Write(bytes);
            Data.WriteByte(0);
            return (newOffset, bytes.Length);
        }
    }
}
