using Penumbra.GameData.Structs;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.String.Functions;

namespace Penumbra.Meta.Files;

/// <summary>
/// EQP/GMP Structure:
/// 64 x [Block collapsed or not bit]
/// 159 x [EquipmentParameter:ulong]
/// (CountSetBits(Block Collapsed or not) - 1) x 160 x [EquipmentParameter:ulong]
/// Item 0 does not exist and is sent to Item 1 instead.
/// </summary>
public unsafe class ExpandedEqpGmpBase : MetaBaseFile
{
    protected const int BlockSize = 160;
    protected const int NumBlocks = 64;
    protected const int EntrySize = 8;
    protected const int MaxSize   = BlockSize * NumBlocks * EntrySize;

    public const int Count = BlockSize * NumBlocks;

    public ulong ControlBlock
        => *(ulong*)Data;

    protected ulong GetInternal(SetId idx)
    {
        return idx.Id switch
        {
            >= Count => throw new IndexOutOfRangeException(),
            <= 1     => *((ulong*)Data + 1),
            _        => *((ulong*)Data + idx.Id),
        };
    }

    protected void SetInternal(SetId idx, ulong value)
    {
        idx = idx.Id switch
        {
            >= Count => throw new IndexOutOfRangeException(),
            <= 0     => 1,
            _        => idx,
        };

        *((ulong*)Data + idx.Id) = value;
    }

    protected virtual void SetEmptyBlock(int idx)
    {
        MemoryUtility.MemSet(Data + idx * BlockSize * EntrySize, 0, BlockSize * EntrySize);
    }

    public sealed override void Reset()
    {
        var ptr            = (byte*)DefaultData.Data;
        var controlBlock   = *(ulong*)ptr;
        var expandedBlocks = 0;
        for (var i = 0; i < NumBlocks; ++i)
        {
            var collapsed = ((controlBlock >> i) & 1) == 0;
            if (!collapsed)
            {
                MemoryUtility.MemCpyUnchecked(Data + i * BlockSize * EntrySize, ptr + expandedBlocks * BlockSize * EntrySize,
                    BlockSize * EntrySize);
                expandedBlocks++;
            }
            else
            {
                SetEmptyBlock(i);
            }
        }

        *(ulong*)Data = ulong.MaxValue;
    }

    public ExpandedEqpGmpBase(MetaFileManager manager, bool gmp)
        : base(manager, gmp ? MetaIndex.Gmp : MetaIndex.Eqp)
    {
        AllocateData(MaxSize);
        Reset();
    }

    protected static ulong GetDefaultInternal(MetaFileManager manager, CharacterUtility.InternalIndex fileIndex, SetId setId, ulong def)
    {
        var data = (byte*)manager.CharacterUtility.DefaultResource(fileIndex).Address;
        if (setId == 0)
            setId = 1;

        var blockIdx = setId.Id / BlockSize;
        if (blockIdx >= NumBlocks)
            return def;

        var control  = *(ulong*)data;
        var blockBit = 1ul << blockIdx;
        if ((control & blockBit) == 0)
            return def;

        var count = BitOperations.PopCount(control & (blockBit - 1));
        var idx   = setId.Id % BlockSize;
        var ptr   = (ulong*)data + BlockSize * count + idx;
        return *ptr;
    }
}

public sealed class ExpandedEqpFile : ExpandedEqpGmpBase, IEnumerable<EqpEntry>
{
    public static readonly CharacterUtility.InternalIndex InternalIndex =
        CharacterUtility.ReverseIndices[(int)MetaIndex.Eqp];

    public ExpandedEqpFile(MetaFileManager manager)
        : base(manager, false)
    { }

    public EqpEntry this[SetId idx]
    {
        get => (EqpEntry)GetInternal(idx);
        set => SetInternal(idx, (ulong)value);
    }


    public static EqpEntry GetDefault(MetaFileManager manager, SetId setIdx)
        => (EqpEntry)GetDefaultInternal(manager, InternalIndex, setIdx, (ulong)Eqp.DefaultEntry);

    protected override unsafe void SetEmptyBlock(int idx)
    {
        var blockPtr = (ulong*)(Data + idx * BlockSize * EntrySize);
        var endPtr   = blockPtr + BlockSize;
        for (var ptr = blockPtr; ptr < endPtr; ++ptr)
            *ptr = (ulong)Eqp.DefaultEntry;
    }

    public void Reset(IEnumerable<SetId> entries)
    {
        foreach (var entry in entries)
            this[entry] = GetDefault(Manager, entry);
    }

    public IEnumerator<EqpEntry> GetEnumerator()
    {
        for (ushort idx = 1; idx < Count; ++idx)
            yield return this[idx];
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}

public sealed class ExpandedGmpFile : ExpandedEqpGmpBase, IEnumerable<GmpEntry>
{
    public static readonly CharacterUtility.InternalIndex InternalIndex =
        CharacterUtility.ReverseIndices[(int)MetaIndex.Gmp];

    public ExpandedGmpFile(MetaFileManager manager)
        : base(manager, true)
    { }

    public GmpEntry this[SetId idx]
    {
        get => (GmpEntry)GetInternal(idx);
        set => SetInternal(idx, (ulong)value);
    }

    public static GmpEntry GetDefault(MetaFileManager manager, SetId setIdx)
        => (GmpEntry)GetDefaultInternal(manager, InternalIndex, setIdx, (ulong)GmpEntry.Default);

    public void Reset(IEnumerable<SetId> entries)
    {
        foreach (var entry in entries)
            this[entry] = GetDefault(Manager, entry);
    }

    public IEnumerator<GmpEntry> GetEnumerator()
    {
        for (ushort idx = 1; idx < Count; ++idx)
            yield return this[idx];
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
