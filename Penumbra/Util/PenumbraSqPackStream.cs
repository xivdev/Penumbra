using Lumina.Data.Structs;
using Lumina.Extensions;

namespace Penumbra.Util;

public class PenumbraSqPackStream : IDisposable
{
    public Stream BaseStream { get; protected set; }

    protected BinaryReader Reader { get; set; }

    public PenumbraSqPackStream(FileInfo file)
        : this(file.OpenRead())
    { }

    public PenumbraSqPackStream(Stream stream)
    {
        BaseStream = stream;
        Reader     = new BinaryReader(BaseStream);
    }

    public SqPackHeader GetSqPackHeader()
    {
        BaseStream.Position = 0;

        return Reader.ReadStructure<SqPackHeader>();
    }

    public SqPackFileInfo GetFileMetadata(long offset)
    {
        BaseStream.Position = offset;

        return Reader.ReadStructure<SqPackFileInfo>();
    }

    public T ReadFile<T>(long offset) where T : PenumbraFileResource
    {
        using var ms = new MemoryStream();

        BaseStream.Position = offset;

        var fileInfo = Reader.ReadStructure<SqPackFileInfo>();
        var file     = Activator.CreateInstance<T>();

        // check if we need to read the extended model header or just default to the standard file header
        if (fileInfo.Type == FileType.Model)
        {
            BaseStream.Position = offset;

            var modelFileInfo = Reader.ReadStructure<ModelBlock>();

            file.FileInfo = new PenumbraFileInfo
            {
                HeaderSize  = modelFileInfo.Size,
                Type        = modelFileInfo.Type,
                BlockCount  = modelFileInfo.UsedNumberOfBlocks,
                RawFileSize = modelFileInfo.RawFileSize,
                Offset      = offset,

                // todo: is this useful?
                ModelBlock = modelFileInfo,
            };
        }
        else
        {
            file.FileInfo = new PenumbraFileInfo
            {
                HeaderSize  = fileInfo.Size,
                Type        = fileInfo.Type,
                BlockCount  = fileInfo.NumberOfBlocks,
                RawFileSize = fileInfo.RawFileSize,
                Offset      = offset,
            };
        }

        switch (fileInfo.Type)
        {
            case FileType.Empty: throw new FileNotFoundException($"The file located at 0x{offset:x} is empty.");

            case FileType.Standard:
                ReadStandardFile(file, ms);
                break;

            case FileType.Model:
                ReadModelFile(file, ms);
                break;

            case FileType.Texture:
                ReadTextureFile(file, ms);
                break;

            default: throw new NotImplementedException($"File Type {(uint)fileInfo.Type} is not implemented.");
        }

        file.Data = ms.ToArray();
        if (file.Data.Length != file.FileInfo.RawFileSize)
            Debug.WriteLine("Read data size does not match file size.");

        file.FileStream          = new MemoryStream(file.Data, false);
        file.Reader              = new BinaryReader(file.FileStream);
        file.FileStream.Position = 0;

        file.LoadFile();

        return file;
    }

    private void ReadStandardFile(PenumbraFileResource resource, MemoryStream ms)
    {
        var blocks = Reader.ReadStructures<DatStdFileBlockInfos>((int)resource.FileInfo!.BlockCount);

        foreach (var block in blocks)
            ReadFileBlock(resource.FileInfo.Offset + resource.FileInfo.HeaderSize + block.Offset, ms);

        // reset position ready for reading
        ms.Position = 0;
    }

    private unsafe void ReadModelFile(PenumbraFileResource resource, MemoryStream ms)
    {
        var mdlBlock   = resource.FileInfo!.ModelBlock;
        var baseOffset = resource.FileInfo.Offset + resource.FileInfo.HeaderSize;

        // 1/1/3/3/3 stack/runtime/vertex/egeo/index
        // TODO: consider testing if this is more reliable than the Explorer method
        // of adding mdlBlock.IndexBufferDataBlockIndex[2] + mdlBlock.IndexBufferDataBlockNum[2]
        // i don't want to move this to that method right now, because i know sometimes the index is 0
        // but it seems to work fine in explorer...
        int totalBlocks = mdlBlock.StackBlockNum;
        totalBlocks += mdlBlock.RuntimeBlockNum;
        for (var i = 0; i < 3; i++)
            totalBlocks += mdlBlock.VertexBufferBlockNum[i];

        for (var i = 0; i < 3; i++)
            totalBlocks += mdlBlock.EdgeGeometryVertexBufferBlockNum[i];

        for (var i = 0; i < 3; i++)
            totalBlocks += mdlBlock.IndexBufferBlockNum[i];

        var compressedBlockSizes = Reader.ReadStructures<ushort>(totalBlocks);
        var currentBlock         = 0;
        var vertexDataOffsets    = new int[3];
        var indexDataOffsets     = new int[3];
        var vertexBufferSizes    = new int[3];
        var indexBufferSizes     = new int[3];

        ms.Seek(0x44, SeekOrigin.Begin);

        Reader.Seek(baseOffset + mdlBlock.StackOffset);
        var stackStart = ms.Position;
        for (var i = 0; i < mdlBlock.StackBlockNum; i++)
        {
            var lastPos = Reader.BaseStream.Position;
            ReadFileBlock(ms);
            Reader.Seek(lastPos + compressedBlockSizes[currentBlock]);
            currentBlock++;
        }

        var stackEnd  = ms.Position;
        var stackSize = (int)(stackEnd - stackStart);

        Reader.Seek(baseOffset + mdlBlock.RuntimeOffset);
        var runtimeStart = ms.Position;
        for (var i = 0; i < mdlBlock.RuntimeBlockNum; i++)
        {
            var lastPos = Reader.BaseStream.Position;
            ReadFileBlock(ms);
            Reader.Seek(lastPos + compressedBlockSizes[currentBlock]);
            currentBlock++;
        }

        var runtimeEnd  = ms.Position;
        var runtimeSize = (int)(runtimeEnd - runtimeStart);

        for (var i = 0; i < 3; i++)
        {
            if (mdlBlock.VertexBufferBlockNum[i] != 0)
            {
                var currentVertexOffset = (int)ms.Position;
                if (i == 0 || currentVertexOffset != vertexDataOffsets[i - 1])
                    vertexDataOffsets[i] = currentVertexOffset;
                else
                    vertexDataOffsets[i] = 0;

                Reader.Seek(baseOffset + mdlBlock.VertexBufferOffset[i]);

                for (var j = 0; j < mdlBlock.VertexBufferBlockNum[i]; j++)
                {
                    var lastPos = Reader.BaseStream.Position;
                    vertexBufferSizes[i] += (int)ReadFileBlock(ms);
                    Reader.Seek(lastPos + compressedBlockSizes[currentBlock]);
                    currentBlock++;
                }
            }

            if (mdlBlock.EdgeGeometryVertexBufferBlockNum[i] != 0)
                for (var j = 0; j < mdlBlock.EdgeGeometryVertexBufferBlockNum[i]; j++)
                {
                    var lastPos = Reader.BaseStream.Position;
                    ReadFileBlock(ms);
                    Reader.Seek(lastPos + compressedBlockSizes[currentBlock]);
                    currentBlock++;
                }

            if (mdlBlock.IndexBufferBlockNum[i] != 0)
            {
                var currentIndexOffset = (int)ms.Position;
                if (i == 0 || currentIndexOffset != indexDataOffsets[i - 1])
                    indexDataOffsets[i] = currentIndexOffset;
                else
                    indexDataOffsets[i] = 0;

                // i guess this is only needed in the vertex area, for i = 0
                // Reader.Seek( baseOffset + mdlBlock.IndexBufferOffset[ i ] );

                for (var j = 0; j < mdlBlock.IndexBufferBlockNum[i]; j++)
                {
                    var lastPos = Reader.BaseStream.Position;
                    indexBufferSizes[i] += (int)ReadFileBlock(ms);
                    Reader.Seek(lastPos + compressedBlockSizes[currentBlock]);
                    currentBlock++;
                }
            }
        }

        ms.Seek(0, SeekOrigin.Begin);
        ms.Write(BitConverter.GetBytes(mdlBlock.Version));
        ms.Write(BitConverter.GetBytes(stackSize));
        ms.Write(BitConverter.GetBytes(runtimeSize));
        ms.Write(BitConverter.GetBytes(mdlBlock.VertexDeclarationNum));
        ms.Write(BitConverter.GetBytes(mdlBlock.MaterialNum));
        for (var i = 0; i < 3; i++)
            ms.Write(BitConverter.GetBytes(vertexDataOffsets[i]));

        for (var i = 0; i < 3; i++)
            ms.Write(BitConverter.GetBytes(indexDataOffsets[i]));

        for (var i = 0; i < 3; i++)
            ms.Write(BitConverter.GetBytes(vertexBufferSizes[i]));

        for (var i = 0; i < 3; i++)
            ms.Write(BitConverter.GetBytes(indexBufferSizes[i]));

        ms.Write(new[]
        {
            mdlBlock.NumLods,
        });
        ms.Write(BitConverter.GetBytes(mdlBlock.IndexBufferStreamingEnabled));
        ms.Write(BitConverter.GetBytes(mdlBlock.EdgeGeometryEnabled));
        ms.Write(new byte[]
        {
            0,
        });
    }

    private void ReadTextureFile(PenumbraFileResource resource, MemoryStream ms)
    {
        if (resource.FileInfo!.BlockCount == 0)
            return;

        var blocks = Reader.ReadStructures<LodBlock>((int)resource.FileInfo!.BlockCount);

        // if there is a mipmap header, the comp_offset
        // will not be 0
        var mipMapSize = blocks[0].CompressedOffset;
        if (mipMapSize != 0)
        {
            var originalPos = BaseStream.Position;

            BaseStream.Position = resource.FileInfo.Offset + resource.FileInfo.HeaderSize;
            ms.Write(Reader.ReadBytes((int)mipMapSize));

            BaseStream.Position = originalPos;
        }

        // i is for texture blocks, j is 'data blocks'...
        for (byte i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].CompressedSize == 0)
                continue;

            // start from comp_offset
            var runningBlockTotal = blocks[i].CompressedOffset + resource.FileInfo.Offset + resource.FileInfo.HeaderSize;
            ReadFileBlock(runningBlockTotal, ms, true);

            for (var j = 1; j < blocks[i].BlockCount; j++)
            {
                runningBlockTotal += (uint)Reader.ReadInt16();
                ReadFileBlock(runningBlockTotal, ms, true);
            }

            // unknown
            Reader.ReadInt16();
        }
    }

    protected uint ReadFileBlock(MemoryStream dest, bool resetPosition = false)
        => ReadFileBlock(Reader.BaseStream.Position, dest, resetPosition);

    protected uint ReadFileBlock(long offset, MemoryStream dest, bool resetPosition = false)
    {
        var originalPosition = BaseStream.Position;
        BaseStream.Position = offset;

        var blockHeader = Reader.ReadStructure<DatBlockHeader>();

        // uncompressed block
        if (blockHeader.CompressedSize == 32000)
        {
            dest.Write(Reader.ReadBytes((int)blockHeader.UncompressedSize));
        }
        else
        {
            var data = Reader.ReadBytes((int)blockHeader.CompressedSize);

            using var compressedStream = new MemoryStream(data);
            using var zlibStream       = new DeflateStream(compressedStream, CompressionMode.Decompress);
            zlibStream.CopyTo(dest);
        }

        if (resetPosition)
            BaseStream.Position = originalPosition;

        return blockHeader.UncompressedSize;
    }

    public void Dispose()
    {
        Reader.Dispose();
        Dispose(true);
    }

    protected virtual void Dispose(bool _)
    { }

    public class PenumbraFileInfo
    {
        public uint     HeaderSize;
        public FileType Type;
        public uint     RawFileSize;
        public uint     BlockCount;

        public long Offset { get; internal set; }

        public ModelBlock ModelBlock { get; internal set; }
    }

    public class PenumbraFileResource
    {
        public PenumbraFileResource()
        { }

        public PenumbraFileInfo? FileInfo { get; internal set; }

        public byte[] Data { get; internal set; } = new byte[0];

        public MemoryStream? FileStream { get; internal set; }

        public BinaryReader? Reader { get; internal set; }

        /// <summary>
        /// Called once the files are read out from the dats. Used to further parse the file into usable data structures.
        /// </summary>
        public virtual void LoadFile()
        {
            // this function is intentionally left blank
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DatBlockHeader
    {
        public uint Size;
        public uint unknown1;
        public uint CompressedSize;
        public uint UncompressedSize;
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct LodBlock
    {
        public uint CompressedOffset;
        public uint CompressedSize;
        public uint DecompressedSize;
        public uint BlockOffset;
        public uint BlockCount;
    }
}
