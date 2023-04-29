using System;
using System.Diagnostics;
using System.Threading;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.ResourceLoading;

public unsafe class ResourceLoader : IDisposable
{
    private readonly ResourceService _resources;
    private readonly FileReadService _fileReadService;
    private readonly TexMdlService   _texMdlService;

    public ResourceLoader(ResourceService resources, FileReadService fileReadService, TexMdlService texMdlService,
        CreateFileWHook _)
    {
        _resources       = resources;
        _fileReadService = fileReadService;
        _texMdlService   = texMdlService;
        ResetResolvePath();

        _resources.ResourceRequested    += ResourceHandler;
        _resources.ResourceHandleIncRef += IncRefProtection;
        _resources.ResourceHandleDecRef += DecRefProtection;
        _fileReadService.ReadSqPack     += ReadSqPackDetour;
    }

    /// <summary> The function to use to resolve a given path. </summary>
    public Func<Utf8GamePath, ResourceCategory, ResourceType, (FullPath?, ResolveData)> ResolvePath = null!;

    /// <summary> Reset the ResolvePath function to always return null. </summary>
    public void ResetResolvePath()
        => ResolvePath = (_1, _2, _3) => (null, ResolveData.Invalid);

    public delegate void ResourceLoadedDelegate(ResourceHandle* handle, Utf8GamePath originalPath, FullPath? manipulatedPath,
        ResolveData resolveData);

    /// <summary>
    /// Event fired whenever a resource is returned.
    /// If the path was manipulated by penumbra, manipulatedPath will be the file path of the loaded resource.
    /// resolveData is additional data returned by the current ResolvePath function which can contain the collection and associated game object.
    /// </summary>
    public event ResourceLoadedDelegate? ResourceLoaded;

    public delegate void FileLoadedDelegate(ResourceHandle* resource, ByteString path, bool returnValue, bool custom,
        ByteString additionalData);

    /// <summary>
    /// Event fired whenever a resource is newly loaded.
    /// ReturnValue indicates the return value of the loading function (which does not imply that the resource was actually successfully loaded)
    /// custom is true if the file was loaded from local files instead of the default SqPacks.
    /// AdditionalData is either empty or the part of the path inside the leading pipes.
    /// </summary>
    public event FileLoadedDelegate? FileLoaded;

    public void Dispose()
    {
        _resources.ResourceRequested    -= ResourceHandler;
        _resources.ResourceHandleIncRef -= IncRefProtection;
        _resources.ResourceHandleDecRef -= DecRefProtection;
        _fileReadService.ReadSqPack     -= ReadSqPackDetour;
    }

    private void ResourceHandler(ref ResourceCategory category, ref ResourceType type, ref int hash, ref Utf8GamePath path, Utf8GamePath original,
        GetResourceParameters* parameters, ref bool sync, ref ResourceHandle* returnValue)
    {
        if (returnValue != null)
            return;

        CompareHash(ComputeHash(path.Path, parameters), hash, path);

        // If no replacements are being made, we still want to be able to trigger the event.
        var (resolvedPath, data) = _incMode.Value ? (null, ResolveData.Invalid) : ResolvePath(path, category, type);
        if (resolvedPath == null || !Utf8GamePath.FromByteString(resolvedPath.Value.InternalName, out var p))
        {
            returnValue = _resources.GetOriginalResource(sync, category, type, hash, path.Path, parameters);
            ResourceLoaded?.Invoke(returnValue, path, resolvedPath, data);
            return;
        }

        _texMdlService.AddCrc(type, resolvedPath);
        // Replace the hash and path with the correct one for the replacement.
        hash        = ComputeHash(resolvedPath.Value.InternalName, parameters);
        var oldPath = path;
        path        = p;
        returnValue = _resources.GetOriginalResource(sync, category, type, hash, path.Path, parameters);
        ResourceLoaded?.Invoke(returnValue, oldPath, resolvedPath.Value, data);
    }

    private void ReadSqPackDetour(SeFileDescriptor* fileDescriptor, ref int priority, ref bool isSync, ref byte? returnValue)
    {
        if (fileDescriptor->ResourceHandle == null)
        {
            Penumbra.Log.Error("[ResourceLoader] Failure to load file from SqPack: invalid File Descriptor.");
            return;
        }

        if (!fileDescriptor->ResourceHandle->GamePath(out var gamePath) || gamePath.Length == 0)
        {
            Penumbra.Log.Error("[ResourceLoader] Failure to load file from SqPack: invalid path specified.");
            return;
        }

        // Paths starting with a '|' are handled separately to allow for special treatment.
        // They are expected to also have a closing '|'.
        if (gamePath.Path[0] != (byte)'|')
        {
            returnValue = DefaultLoadResource(gamePath.Path, fileDescriptor, priority, isSync, ByteString.Empty);
            return;
        }

        // Split the path into the special-treatment part (between the first and second '|')
        // and the actual path.
        var split = gamePath.Path.Split((byte)'|', 3, false);
        fileDescriptor->ResourceHandle->FileNameData   = split[2].Path;
        fileDescriptor->ResourceHandle->FileNameLength = split[2].Length;
        MtrlForceSync(fileDescriptor, ref isSync);
        returnValue = DefaultLoadResource(split[2], fileDescriptor, priority, isSync, split[1]);
        // Return original resource handle path so that they can be loaded separately.
        fileDescriptor->ResourceHandle->FileNameData   = gamePath.Path.Path;
        fileDescriptor->ResourceHandle->FileNameLength = gamePath.Path.Length;
    }


    /// <summary> Load a resource by its path. If it is rooted, it will be loaded from the drive, otherwise from the SqPack. </summary>
    private byte DefaultLoadResource(ByteString gamePath, SeFileDescriptor* fileDescriptor, int priority,
        bool isSync, ByteString additionalData)
    {
        if (Utf8GamePath.IsRooted(gamePath))
        {
            // Specify that we are loading unpacked files from the drive.
            // We need to obtain the actual file path in UTF16 (Windows-Unicode) on two locations,
            // but we write a pointer to the given string instead and use the CreateFileW hook to handle it,
            // because otherwise we are limited to 260 characters.
            fileDescriptor->FileMode = FileMode.LoadUnpackedResource;

            // Ensure that the file descriptor has its wchar_t array on aligned boundary even if it has to be odd.
            var fd = stackalloc char[0x11 + 0x0B + 14];
            fileDescriptor->FileDescriptor = (byte*)fd + 1;
            CreateFileWHook.WritePtr(fd + 0x11,                      gamePath.Path, gamePath.Length);
            CreateFileWHook.WritePtr(&fileDescriptor->Utf16FileName, gamePath.Path, gamePath.Length);

            // Use the SE ReadFile function.
            var ret = _fileReadService.ReadFile(fileDescriptor, priority, isSync);
            FileLoaded?.Invoke(fileDescriptor->ResourceHandle, gamePath, ret != 0, true, additionalData);
            return ret;
        }
        else
        {
            var ret = _fileReadService.ReadDefaultSqPack(fileDescriptor, priority, isSync);
            FileLoaded?.Invoke(fileDescriptor->ResourceHandle, gamePath, ret != 0, false, additionalData);
            return ret;
        }
    }

    /// <summary> Special handling for materials. </summary>
    private static void MtrlForceSync(SeFileDescriptor* fileDescriptor, ref bool isSync)
    {
        // Force isSync = true for Materials. I don't really understand why,
        // or where the difference even comes from.
        // Was called with True on my client and with false on other peoples clients,
        // which caused problems.
        isSync |= fileDescriptor->ResourceHandle->FileType is ResourceType.Mtrl;
    }

    /// <summary>
    /// A resource with ref count 0 that gets incremented goes through GetResourceAsync again.
    /// This means, that if the path determined from that is different than the resources path,
    /// a different resource gets loaded or incremented, while the IncRef'd resource stays at 0.
    /// This causes some problems and is hopefully prevented with this.
    /// </summary>
    private readonly ThreadLocal<bool> _incMode = new(() => false, true);

    /// <inheritdoc cref="_incMode"/>
    private void IncRefProtection(ResourceHandle* handle, ref nint? returnValue)
    {
        if (handle->RefCount != 0)
            return;

        _incMode.Value = true;
        returnValue    = _resources.IncRef(handle);
        _incMode.Value = false;
    }

    /// <summary>
    /// Catch weird errors with invalid decrements of the reference count.
    /// </summary>
    private void DecRefProtection(ResourceHandle* handle, ref byte? returnValue)
    {
        if (handle->RefCount != 0)
            return;

        Penumbra.Log.Error(
            $"[ResourceLoader] Caught decrease of Reference Counter for {handle->FileName()} at 0x{(ulong)handle} below 0.");
        returnValue = 1;
    }

    /// <summary> Compute the CRC32 hash for a given path together with potential resource parameters. </summary>
    private static int ComputeHash(ByteString path, GetResourceParameters* pGetResParams)
    {
        if (pGetResParams == null || !pGetResParams->IsPartialRead)
            return path.Crc32;

        // When the game requests file only partially, crc32 includes that information, in format of:
        // path/to/file.ext.hex_offset.hex_size
        // ex) music/ex4/BGM_EX4_System_Title.scd.381adc.30000
        return ByteString.Join(
            (byte)'.',
            path,
            ByteString.FromStringUnsafe(pGetResParams->SegmentOffset.ToString("x"), true),
            ByteString.FromStringUnsafe(pGetResParams->SegmentLength.ToString("x"), true)
        ).Crc32;
    }

    /// <summary>
    /// In Debug build, compare the hashes the game computes with those Penumbra computes to notice potential changes in the CRC32 algorithm or resource parameters.
    /// </summary>
    [Conditional("DEBUG")]
    private static void CompareHash(int local, int game, Utf8GamePath path)
    {
        if (local != game)
            Penumbra.Log.Warning($"[ResourceLoader] Hash function appears to have changed. Computed {local:X8} vs Game {game:X8} for {path}.");
    }
}
