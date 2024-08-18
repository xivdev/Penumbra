using Penumbra.GameData.Structs;
using Penumbra.Interop.Services;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Manipulations;
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
    public const int BlockSize = 160;
    public const int NumBlocks = 64;
    public const int EntrySize = 8;
    public const int MaxSize   = BlockSize * NumBlocks * EntrySize;

    public const int Count = BlockSize * NumBlocks;

    public ulong ControlBlock
        => *(ulong*)Data;

    protected ulong GetInternal(PrimaryId idx)
    {
        return idx.Id switch
        {
            >= Count => throw new IndexOutOfRangeException(),
            <= 1     => *((ulong*)Data + 1),
            _        => *((ulong*)Data + idx.Id),
        };
    }

    protected void SetInternal(PrimaryId idx, ulong value)
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
        : base(manager, manager.MarshalAllocator, gmp ? MetaIndex.Gmp : MetaIndex.Eqp)
    {
        AllocateData(MaxSize);
        Reset();
    }

    protected static ulong GetDefaultInternal(MetaFileManager manager, CharacterUtility.InternalIndex fileIndex, PrimaryId primaryId, ulong def)
    {
        var data = (byte*)manager.CharacterUtility.DefaultResource(fileIndex).Address;
        if (primaryId == 0)
            primaryId = 1;

        var blockIdx = primaryId.Id / BlockSize;
        if (blockIdx >= NumBlocks)
            return def;

        var control  = *(ulong*)data;
        var blockBit = 1ul << blockIdx;
        if ((control & blockBit) == 0)
            return def;

        var count = BitOperations.PopCount(control & (blockBit - 1));
        var idx   = primaryId.Id % BlockSize;
        var ptr   = (ulong*)data + BlockSize * count + idx;
        return *ptr;
    }
}

public sealed class ExpandedEqpFile(MetaFileManager manager) : ExpandedEqpGmpBase(manager, false), IEnumerable<EqpEntry>
{
    public static readonly CharacterUtility.InternalIndex InternalIndex =
        CharacterUtility.ReverseIndices[(int)MetaIndex.Eqp];

    public EqpEntry this[PrimaryId idx]
    {
        get => (EqpEntry)GetInternal(idx);
        set => SetInternal(idx, (ulong)value);
    }


    public static EqpEntry GetDefault(MetaFileManager manager, PrimaryId primaryIdx)
        => (EqpEntry)GetDefaultInternal(manager, InternalIndex, primaryIdx, (ulong)Eqp.DefaultEntry);

    protected override unsafe void SetEmptyBlock(int idx)
    {
        var blockPtr = (ulong*)(Data + idx * BlockSize * EntrySize);
        var endPtr   = blockPtr + BlockSize;
        for (var ptr = blockPtr; ptr < endPtr; ++ptr)
            *ptr = (ulong)Eqp.DefaultEntry;
    }

    public void Reset(IEnumerable<PrimaryId> entries)
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

public sealed class ExpandedGmpFile(MetaFileManager manager) : ExpandedEqpGmpBase(manager, true), IEnumerable<GmpEntry>
{
    public static readonly CharacterUtility.InternalIndex InternalIndex =
        CharacterUtility.ReverseIndices[(int)MetaIndex.Gmp];

    public GmpEntry this[PrimaryId idx]
    {
        get => new() { Value = GetInternal(idx) };
        set => SetInternal(idx, value.Value);
    }

    public static GmpEntry GetDefault(MetaFileManager manager, PrimaryId primaryIdx)
        => new() { Value = GetDefaultInternal(manager, InternalIndex, primaryIdx, GmpEntry.Default.Value) };

    public static GmpEntry GetDefault(MetaFileManager manager, GmpIdentifier identifier)
        => new() { Value = GetDefaultInternal(manager, InternalIndex, identifier.SetId, GmpEntry.Default.Value) };

    public void Reset(IEnumerable<PrimaryId> entries)
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
