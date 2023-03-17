using System;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using Penumbra.GameData;

namespace Penumbra.Interop.Services;

public unsafe class MetaFileManager
{
    public MetaFileManager()
    {
        SignatureHelper.Initialise(this);
    }

    // Allocate in the games space for file storage.
    // We only need this if using any meta file.
    [Signature(Sigs.GetFileSpace)]
    private readonly IntPtr _getFileSpaceAddress = IntPtr.Zero;

    public IMemorySpace* GetFileSpace()
        => ((delegate* unmanaged<IMemorySpace*>)_getFileSpaceAddress)();

    public void* AllocateFileMemory(ulong length, ulong alignment = 0)
        => GetFileSpace()->Malloc(length, alignment);

    public void* AllocateFileMemory(int length, int alignment = 0)
        => AllocateFileMemory((ulong)length, (ulong)alignment);

    public void* AllocateDefaultMemory(ulong length, ulong alignment = 0)
        => GetFileSpace()->Malloc(length, alignment);

    public void* AllocateDefaultMemory(int length, int alignment = 0)
        => IMemorySpace.GetDefaultSpace()->Malloc((ulong)length, (ulong)alignment);

    public void Free(IntPtr ptr, int length)
        => IMemorySpace.Free((void*)ptr, (ulong)length);
}