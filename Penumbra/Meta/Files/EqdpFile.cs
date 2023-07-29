using System;
using System.Collections.Generic;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.String.Functions;

namespace Penumbra.Meta.Files;

/// <summary>
/// EQDP file structure:
/// [Identifier][BlockSize:ushort][BlockCount:ushort]
///   BlockCount x [BlockHeader:ushort]
///   Containing offsets for blocks, ushort.Max means collapsed.
///   Offsets are based on the end of the header, so 0 means IdentifierSize + 4 + BlockCount x 2.
///     ExpandedBlockCount x [Entry]
/// Expanded Eqdp File just expands all blocks for easy read and write access to single entries and to keep the same memory for it.
/// </summary>
public sealed unsafe class ExpandedEqdpFile : MetaBaseFile
{
    private const ushort BlockHeaderSize = 2;
    private const ushort PreambleSize    = 4;
    private const ushort CollapsedBlock  = ushort.MaxValue;
    private const ushort IdentifierSize  = 2;
    private const ushort EqdpEntrySize   = 2;
    private const int    FileAlignment   = 1 << 9;

    public readonly int DataOffset;

    public ushort Identifier
        => *(ushort*)Data;

    public ushort BlockSize
        => *(ushort*)(Data + 2);

    public ushort BlockCount
        => *(ushort*)(Data + 4);

    public int Count
        => (Length - DataOffset) / EqdpEntrySize;

    public EqdpEntry this[SetId id]
    {
        get
        {
            if (id.Id >= Count)
                throw new IndexOutOfRangeException();

            return (EqdpEntry)(*(ushort*)(Data + DataOffset + EqdpEntrySize * id.Id));
        }
        set
        {
            if (id.Id >= Count)
                throw new IndexOutOfRangeException();

            *(ushort*)(Data + DataOffset + EqdpEntrySize * id.Id) = (ushort)value;
        }
    }

    public override void Reset()
    {
        var def = (byte*)DefaultData.Data;
        MemoryUtility.MemCpyUnchecked(Data, def, IdentifierSize + PreambleSize);

        var controlPtr   = (ushort*)(def + IdentifierSize + PreambleSize);
        var dataBasePtr  = controlPtr + BlockCount;
        var myDataPtr    = (ushort*)(Data + IdentifierSize + PreambleSize + 2 * BlockCount);
        var myControlPtr = (ushort*)(Data + IdentifierSize + PreambleSize);
        for (var i = 0; i < BlockCount; ++i)
        {
            if (controlPtr[i] == CollapsedBlock)
                MemoryUtility.MemSet(myDataPtr, 0, BlockSize * EqdpEntrySize);
            else
                MemoryUtility.MemCpyUnchecked(myDataPtr, dataBasePtr + controlPtr[i], BlockSize * EqdpEntrySize);

            myControlPtr[i] =  (ushort)(i * BlockSize);
            myDataPtr       += BlockSize;
        }

        MemoryUtility.MemSet(myDataPtr, 0, Length - (int)((byte*)myDataPtr - Data));
    }

    public void Reset(IEnumerable<SetId> entries)
    {
        foreach (var entry in entries)
            this[entry] = GetDefault(entry);
    }

    public ExpandedEqdpFile(MetaFileManager manager, GenderRace raceCode, bool accessory)
        : base(manager, CharacterUtilityData.EqdpIdx(raceCode, accessory))
    {
        var def             = (byte*)DefaultData.Data;
        var blockSize       = *(ushort*)(def + IdentifierSize);
        var totalBlockCount = *(ushort*)(def + IdentifierSize + 2);
        var totalBlockSize  = blockSize * EqdpEntrySize;

        DataOffset = IdentifierSize + PreambleSize + totalBlockCount * BlockHeaderSize;

        var fullLength = DataOffset + totalBlockCount * totalBlockSize;
        fullLength += (FileAlignment - (fullLength & (FileAlignment - 1))) & (FileAlignment - 1);
        AllocateData(fullLength);
        Reset();
    }

    public EqdpEntry GetDefault(SetId setId)
        => GetDefault(Manager, Index, setId);

    public static EqdpEntry GetDefault(MetaFileManager manager, CharacterUtility.InternalIndex idx, SetId setId)
        => GetDefault((byte*)manager.CharacterUtility.DefaultResource(idx).Address, setId);

    public static EqdpEntry GetDefault(byte* data, SetId setId)
    {
        var blockSize       = *(ushort*)(data + IdentifierSize);
        var totalBlockCount = *(ushort*)(data + IdentifierSize + 2);

        var blockIdx = setId.Id / blockSize;
        if (blockIdx >= totalBlockCount)
            return 0;

        var block = ((ushort*)(data + IdentifierSize + PreambleSize))[blockIdx];
        if (block == CollapsedBlock)
            return 0;

        var blockData = (ushort*)(data + IdentifierSize + PreambleSize + totalBlockCount * 2 + block * 2);
        return (EqdpEntry)(*(blockData + setId.Id % blockSize));
    }

    public static EqdpEntry GetDefault(MetaFileManager manager, GenderRace raceCode, bool accessory, SetId setId)
        => GetDefault(manager, CharacterUtility.ReverseIndices[(int)CharacterUtilityData.EqdpIdx(raceCode, accessory)], setId);
}
