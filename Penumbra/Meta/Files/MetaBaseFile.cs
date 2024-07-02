using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Interop.Structs;
using Penumbra.String.Functions;
using CharacterUtility = Penumbra.Interop.Services.CharacterUtility;

namespace Penumbra.Meta.Files;

public unsafe interface IFileAllocator
{
    public T*   Allocate<T>(int length, int alignment = 1) where T : unmanaged;
    public void Release<T>(ref T* pointer, int length) where T : unmanaged;

    public void Release(void* pointer, int length)
    {
        var tmp = (byte*)pointer;
        Release(ref tmp, length);
    }

    public byte* Allocate(int length, int alignment = 1)
        => Allocate<byte>(length, alignment);
}

public sealed class MarshalAllocator : IFileAllocator
{
    public unsafe T* Allocate<T>(int length, int alignment = 1) where T : unmanaged
        => (T*)Marshal.AllocHGlobal(length * sizeof(T));

    public unsafe void Release<T>(ref T* pointer, int length) where T : unmanaged
    {
        Marshal.FreeHGlobal((nint)pointer);
        pointer = null;
    }
}

public sealed unsafe class XivFileAllocator : IFileAllocator, IService
{
    /// <summary>
    /// Allocate in the games space for file storage.
    /// We only need this if using any meta file.
    /// </summary>
    [Signature(Sigs.GetFileSpace)]
    private readonly nint _getFileSpaceAddress = nint.Zero;

    public XivFileAllocator(IGameInteropProvider provider)
        => provider.InitializeFromAttributes(this);

    public IMemorySpace* GetFileSpace()
        => ((delegate* unmanaged<IMemorySpace*>)_getFileSpaceAddress)();

    public T* Allocate<T>(int length, int alignment = 1) where T : unmanaged
        => (T*)GetFileSpace()->Malloc((ulong)(length * sizeof(T)), (ulong)alignment);

    public void Release<T>(ref T* pointer, int length) where T : unmanaged
    {
        IMemorySpace.Free(pointer, (ulong)(length * sizeof(T)));
        pointer = null;
    }
}

public unsafe class MetaBaseFile(MetaFileManager manager, IFileAllocator alloc, MetaIndex idx) : IDisposable
{
    protected readonly MetaFileManager Manager   = manager;
    protected readonly IFileAllocator  Allocator = alloc;

    public byte*                          Data   { get; private set; }
    public int                            Length { get; private set; }
    public CharacterUtility.InternalIndex Index  { get; } = CharacterUtility.ReverseIndices[(int)idx];

    protected (nint Data, int Length) DefaultData
        => Manager.CharacterUtility.DefaultResource(Index);

    /// <summary> Reset to default values. </summary>
    public virtual void Reset()
    { }

    /// <summary> Obtain memory. </summary>
    protected void AllocateData(int length)
    {
        Length = length;
        Data   = Allocator.Allocate(length);
        if (length > 0)
            GC.AddMemoryPressure(length);
    }

    /// <summary> Free memory. </summary>
    protected void ReleaseUnmanagedResources()
    {
        Allocator.Release(Data, Length);
        if (Length > 0)
            GC.RemoveMemoryPressure(Length);

        Length = 0;
        Data   = null;
    }

    /// <summary> Resize memory while retaining data. </summary>
    protected void ResizeResources(int newLength)
    {
        if (newLength == Length)
            return;

        var data = Allocator.Allocate(newLength);
        if (newLength > Length)
        {
            MemoryUtility.MemCpyUnchecked(data, Data, Length);
            MemoryUtility.MemSet(data + Length, 0, newLength - Length);
        }
        else
        {
            MemoryUtility.MemCpyUnchecked(data, Data, newLength);
        }

        ReleaseUnmanagedResources();
        GC.AddMemoryPressure(newLength);
        Data   = data;
        Length = newLength;
    }

    /// <summary> Manually free memory.  </summary>
    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~MetaBaseFile()
    {
        ReleaseUnmanagedResources();
    }
}
