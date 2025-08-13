using Dalamud.Game;
using Dalamud.Plugin.Services;
using Penumbra.Api.Enums;
using Penumbra.GameData;
using Penumbra.GameData.Data;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.String;

namespace Penumbra.Interop.Processing;

public sealed class PbdFilePostProcessor : IFilePostProcessor
{
    private readonly IFileAllocator                             _allocator;
    private          byte[]                                     _epbdData;
    private unsafe   delegate* unmanaged<ResourceHandle*, void> _loadEpbdData;

    public ResourceType Type
        => ResourceType.Pbd;

    public unsafe PbdFilePostProcessor(IDataManager dataManager, XivFileAllocator allocator, ISigScanner scanner)
    {
        _allocator    = allocator;
        _epbdData     = SetEpbdData(dataManager);
        _loadEpbdData = (delegate* unmanaged<ResourceHandle*, void>)scanner.ScanText(Sigs.LoadEpbdData);
    }

    public unsafe void PostProcess(ResourceHandle* resource, CiByteString originalGamePath, ReadOnlySpan<byte> additionalData)
    {
        if (_epbdData.Length is 0)
            return;

        if (resource->LoadState is not LoadState.Success)
        {
            Penumbra.Log.Warning($"[ResourceLoader] Requested PBD at {resource->FileName()} failed load ({resource->LoadState}).");
            return;
        }

        var (data, length) = resource->GetData();
        if (length is 0 || data == nint.Zero)
        {
            Penumbra.Log.Warning($"[ResourceLoader] Requested PBD at {resource->FileName()} succeeded load but has no data.");
            return;
        }

        var span   = new ReadOnlySpan<byte>((void*)data, (int)resource->FileSize);
        var reader = new PackReader(span);
        if (reader.HasData)
        {
            Penumbra.Log.Excessive($"[ResourceLoader] Successfully loaded PBD at {resource->FileName()} with EPBD data.");
            return;
        }

        var newData = AppendData(span);
        fixed (byte* ptr = newData)
        {
            // Set the appended data and the actual file size, then re-load the EPBD data via game function call.
            if (resource->SetData((nint)ptr, newData.Length))
            {
                resource->FileSize           = (uint)newData.Length;
                resource->CsHandle.FileSize2 = (uint)newData.Length;
                resource->CsHandle.FileSize3 = (uint)newData.Length;
                _loadEpbdData(resource);
                // Free original data.
                _allocator.Release((void*)data, length);
                Penumbra.Log.Debug($"[ResourceLoader] Loaded {resource->FileName()} from file and appended default EPBD data.");
            }
            else
            {
                Penumbra.Log.Warning(
                    $"[ResourceLoader] Failed to append EPBD data to custom PBD at {resource->FileName()}.");
            }
        }
    }

    /// <summary> Combine the given data with the default PBD data using the game's file allocator. </summary>
    private unsafe ReadOnlySpan<byte> AppendData(ReadOnlySpan<byte> data)
    {
        // offset has to be set, otherwise not called.
        var newLength = data.Length + _epbdData.Length;
        var memory    = _allocator.Allocate(newLength);
        var span      = new Span<byte>(memory, newLength);
        data.CopyTo(span);
        _epbdData.CopyTo(span[data.Length..]);
        return span;
    }

    /// <summary> Fetch the default EPBD data from the .pbd file of the game's installation. </summary>
    private static byte[] SetEpbdData(IDataManager dataManager)
    {
        try
        {
            var file = dataManager.GetFile(GamePaths.Pbd.Path);
            if (file is null || file.Data.Length is 0)
            {
                Penumbra.Log.Warning("Default PBD file has no data.");
                return [];
            }

            ReadOnlySpan<byte> span   = file.Data;
            var                reader = new PackReader(span);
            if (!reader.HasData)
            {
                Penumbra.Log.Warning("Default PBD file has no EPBD section.");
                return [];
            }

            var offset = span.Length - (int)reader.PackLength;
            var ret    = span[offset..];
            Penumbra.Log.Verbose($"Default PBD file has EPBD section of length {ret.Length} at offset {offset}.");
            return ret.ToArray();
        }
        catch (Exception ex)
        {
            Penumbra.Log.Error($"Unknown error getting default EPBD data:\n{ex}");
            return [];
        }
    }
}
