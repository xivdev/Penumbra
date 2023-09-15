using Dalamud.Memory;
using Penumbra.Interop.Structs;
using Penumbra.String.Functions;
using CharacterUtility = Penumbra.Interop.Services.CharacterUtility;

namespace Penumbra.Meta.Files;

public unsafe class MetaBaseFile : IDisposable
{
    protected readonly MetaFileManager Manager;

    public byte*                          Data   { get; private set; }
    public int                            Length { get; private set; }
    public CharacterUtility.InternalIndex Index  { get; }

    public MetaBaseFile(MetaFileManager manager, MetaIndex idx)
    {
        Manager = manager;
        Index   = CharacterUtility.ReverseIndices[(int)idx];
    }

    protected (IntPtr Data, int Length) DefaultData
        => Manager.CharacterUtility.DefaultResource(Index);

    /// <summary> Reset to default values. </summary>
    public virtual void Reset()
    { }

    /// <summary> Obtain memory. </summary>
    protected void AllocateData(int length)
    {
        Length = length;
        Data   = (byte*)Manager.AllocateFileMemory(length);
        if (length > 0)
            GC.AddMemoryPressure(length);
    }

    /// <summary> Free memory. </summary>
    protected void ReleaseUnmanagedResources()
    {
        var ptr = (IntPtr)Data;
        MemoryHelper.GameFree(ref ptr, (ulong)Length);
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

        var data = (byte*)Manager.AllocateFileMemory((ulong)newLength);
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
