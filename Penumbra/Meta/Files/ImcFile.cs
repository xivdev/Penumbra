using OtterGui;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Manipulations;
using Penumbra.String.Classes;
using Penumbra.String.Functions;

namespace Penumbra.Meta.Files;

public class ImcException(ImcIdentifier identifier, Utf8GamePath path) : Exception
{
    public readonly ImcIdentifier Identifier = identifier;
    public readonly string        GamePath   = path.ToString();

    public override string Message
        => "Could not obtain default Imc File.\n"
          + "        Either the default file does not exist (possibly for offhand files from TexTools) or the installation is corrupted.\n"
          + $"        Game Path:  {GamePath}\n"
          + $"        Manipulation: {Identifier}";
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

    public ImcEntry GetEntry(EquipSlot slot, Variant variantIdx)
        => GetEntry(PartIndex(slot), variantIdx);

    public ImcEntry GetEntry(int partIdx, Variant variantIdx, out bool exists)
    {
        var ptr = VariantPtr(Data, partIdx, variantIdx);
        exists = ptr != null;
        return exists ? *ptr : new ImcEntry();
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

    public ImcFile(MetaFileManager manager, ImcIdentifier identifier)
        : this(manager, manager.MarshalAllocator, identifier)
    { }

    public ImcFile(MetaFileManager manager, IFileAllocator alloc, ImcIdentifier identifier)
        : base(manager, alloc, 0)
    {
        var path = identifier.GamePathString();
        Path = Utf8GamePath.FromString(path, out var p) ? p : Utf8GamePath.Empty;
        var file = manager.GameData.GetFile(path);
        if (file == null)
            throw new ImcException(identifier, Path);

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

        return GetEntry(file.Data, slot, variantIdx, out exists);
    }

    public static ImcEntry GetEntry(ReadOnlySpan<byte> imcFileData, EquipSlot slot, Variant variantIdx, out bool exists)
    {
        fixed (byte* ptr = imcFileData)
        {
            var entry = VariantPtr(ptr, PartIndex(slot), variantIdx);
            if (entry == null)
            {
                exists = false;
                return new ImcEntry();
            }

            exists = true;
            return *entry;
        }
    }

    public void Replace(ResourceHandle* resource)
    {
        var (data, length) = resource->GetData();
        var actualLength = ActualLength;

        if (DebugConfiguration.WriteImcBytesToLog)
        {
            Penumbra.Log.Information($"Default IMC file -> Modified IMC File for {Path}, current handle state {resource->LoadState}:");
            Penumbra.Log.Information(new Span<byte>((void*)data, length).WriteHexBytes());
            Penumbra.Log.Information(new Span<byte>(Data,        actualLength).WriteHexBytes());
            Penumbra.Log.Information(new Span<byte>(Data,        actualLength).WriteHexByteDiff(new Span<byte>((void*)data, length)));
        }

        if (length >= actualLength)
        {
            MemoryUtility.MemCpyUnchecked((byte*)data, Data, actualLength);
            if (length > actualLength)
                MemoryUtility.MemSet((byte*)(data + actualLength), 0, length - actualLength);
            if (DebugConfiguration.WriteImcBytesToLog)
            {
                Penumbra.Log.Information(
                    $"Copied {actualLength} bytes from local IMC file into {length} available bytes.{(length > actualLength ? $" Filled remaining {length - actualLength} bytes with 0." : string.Empty)}");
                Penumbra.Log.Information("Result IMC Resource Data:");
                Penumbra.Log.Information(new Span<byte>((void*)data, length).WriteHexBytes());
            }

            return;
        }

        var paddedLength = actualLength.PadToMultiple(128);
        var newData      = Manager.XivFileAllocator.Allocate(paddedLength, 8);
        if (newData == null)
        {
            Penumbra.Log.Error($"Could not replace loaded IMC data at 0x{(ulong)resource:X}, allocation failed.");
            return;
        }

        MemoryUtility.MemCpyUnchecked(newData, Data, actualLength);
        if (paddedLength > actualLength)
            MemoryUtility.MemSet(newData + actualLength, 0, paddedLength - actualLength);
        if (DebugConfiguration.WriteImcBytesToLog)
        {
            Penumbra.Log.Information(
                $"Allocated {paddedLength} bytes for IMC file, copied {actualLength} bytes from local IMC file. {(length > actualLength ? $" Filled remaining {length - actualLength} bytes with 0." : string.Empty)}");
            Penumbra.Log.Information("Result IMC Resource Data:");
            Penumbra.Log.Information(new Span<byte>(newData, paddedLength).WriteHexBytes());
        }

        Manager.XivFileAllocator.Release((void*)data, length);
        resource->SetData((nint)newData, paddedLength);
    }
}
