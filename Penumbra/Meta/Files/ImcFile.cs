using System;
using System.Numerics;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;
using Penumbra.String.Functions;

namespace Penumbra.Meta.Files;

public class ImcException : Exception
{
    public readonly ImcManipulation Manipulation;
    public readonly string          GamePath;

    public ImcException(ImcManipulation manip, Utf8GamePath path)
    {
        Manipulation = manip;
        GamePath     = path.ToString();
    }

    public override string Message
        => "Could not obtain default Imc File.\n"
          + "        Either the default file does not exist (possibly for offhand files from TexTools) or the installation is corrupted.\n"
          + $"        Game Path:  {GamePath}\n"
          + $"        Manipulation: {Manipulation}";
}

public unsafe class ImcFile : MetaBaseFile
{
    private const int PreambleSize = 4;

    public int ActualLength
        => NumParts * sizeof(ImcEntry) * (Count + 1) + PreambleSize;

    public int Count
        => CountInternal(Data);

    public readonly Utf8GamePath Path;
    public readonly int          NumParts;

    public ReadOnlySpan<ImcEntry> Span
        => new((ImcEntry*)(Data + PreambleSize), (Length - PreambleSize) / sizeof(ImcEntry));

    private static int CountInternal(byte* data)
        => *(ushort*)data;

    private static ushort PartMask(byte* data)
        => *(ushort*)(data + 2);

    private static ImcEntry* VariantPtr(byte* data, int partIdx, Variant variantIdx)
    {
        var flag = 1 << partIdx;
        if ((PartMask(data) & flag) == 0 || variantIdx.Id > CountInternal(data))
            return null;

        var numParts = BitOperations.PopCount(PartMask(data));
        var ptr      = (ImcEntry*)(data + PreambleSize);
        ptr += variantIdx.Id * numParts + partIdx;
        return ptr;
    }

    public ImcEntry GetEntry(int partIdx, Variant variantIdx)
    {
        var ptr = VariantPtr(Data, partIdx, variantIdx);
        return ptr == null ? new ImcEntry() : *ptr;
    }

    public static int PartIndex(EquipSlot slot)
        => slot switch
        {
            EquipSlot.Head    => 0,
            EquipSlot.Ears    => 0,
            EquipSlot.Body    => 1,
            EquipSlot.Neck    => 1,
            EquipSlot.Hands   => 2,
            EquipSlot.Wrists  => 2,
            EquipSlot.Legs    => 3,
            EquipSlot.RFinger => 3,
            EquipSlot.Feet    => 4,
            EquipSlot.LFinger => 4,
            _                 => 0,
        };

    public bool EnsureVariantCount(int numVariants)
    {
        if (numVariants <= Count)
            return true;

        var oldCount = Count;
        *(ushort*)Data = (ushort)numVariants;
        if (ActualLength > Length)
        {
            var newLength = (((ActualLength - 1) >> 7) + 1) << 7;
            Penumbra.Log.Verbose($"Resized IMC {Path} from {Length} to {newLength}.");
            ResizeResources(newLength);
        }

        var defaultPtr = (ImcEntry*)(Data + PreambleSize);
        for (var i = oldCount + 1; i < numVariants + 1; ++i)
            MemoryUtility.MemCpyUnchecked(defaultPtr + i * NumParts, defaultPtr, NumParts * sizeof(ImcEntry));

        Penumbra.Log.Verbose($"Expanded IMC {Path} from {oldCount} to {numVariants} variants.");
        return true;
    }

    public bool SetEntry(int partIdx, Variant variantIdx, ImcEntry entry)
    {
        if (partIdx >= NumParts)
            return false;

        EnsureVariantCount(variantIdx.Id);

        var variantPtr = VariantPtr(Data, partIdx, variantIdx);
        if (variantPtr == null)
        {
            Penumbra.Log.Error("Error during expansion of imc file.");
            return false;
        }

        if (variantPtr->Equals(entry))
            return false;

        *variantPtr = entry;
        return true;
    }


    public override void Reset()
    {
        var file = Manager.GameData.GetFile(Path.ToString());
        fixed (byte* ptr = file!.Data)
        {
            MemoryUtility.MemCpyUnchecked(Data, ptr, file.Data.Length);
            MemoryUtility.MemSet(Data + file.Data.Length, 0, Length - file.Data.Length);
        }
    }

    public ImcFile(MetaFileManager manager, ImcManipulation manip)
        : base(manager, 0)
    {
        Path = manip.GamePath();
        var file = manager.GameData.GetFile(Path.ToString());
        if (file == null)
            throw new ImcException(manip, Path);

        fixed (byte* ptr = file.Data)
        {
            NumParts = BitOperations.PopCount(*(ushort*)(ptr + 2));
            AllocateData(file.Data.Length);
            MemoryUtility.MemCpyUnchecked(Data, ptr, file.Data.Length);
        }
    }

    public static ImcEntry GetDefault(MetaFileManager manager, Utf8GamePath path, EquipSlot slot, Variant variantIdx, out bool exists)
        => GetDefault(manager, path.ToString(), slot, variantIdx, out exists);

    public static ImcEntry GetDefault(MetaFileManager manager, string path, EquipSlot slot, Variant variantIdx, out bool exists)
    {
        var file = manager.GameData.GetFile(path);
        exists = false;
        if (file == null)
            throw new Exception();

        fixed (byte* ptr = file.Data)
        {
            var entry = VariantPtr(ptr, PartIndex(slot), variantIdx);
            if (entry == null)
                return new ImcEntry();

            exists = true;
            return *entry;
        }
    }

    public void Replace(ResourceHandle* resource)
    {
        var (data, length) = resource->GetData();
        var newData = Manager.AllocateDefaultMemory(ActualLength, 8);
        if (newData == null)
        {
            Penumbra.Log.Error($"Could not replace loaded IMC data at 0x{(ulong)resource:X}, allocation failed.");
            return;
        }

        MemoryUtility.MemCpyUnchecked(newData, Data, ActualLength);

        Manager.Free(data, length);
        resource->SetData((IntPtr)newData, ActualLength);
    }
}
