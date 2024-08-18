using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Api.Enums;
using Penumbra.String;
using Penumbra.String.Classes;
using CsHandle = FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

namespace Penumbra.Interop.Structs;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct TextureResourceHandle
{
    [FieldOffset(0x0)]
    public ResourceHandle Handle;

    [FieldOffset(0x0)]
    public CsHandle.TextureResourceHandle CsHandle;

    [FieldOffset(0x104)]
    public byte SomeLodFlag;

    public bool ChangeLod
        => (SomeLodFlag & 1) != 0;
}

public enum LoadState : byte
{
    Success           = 0x07,
    Async             = 0x03,
    Failure           = 0x09,
    FailedSubResource = 0x0A,
    None              = 0xFF,
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct ResourceHandle
{
    [StructLayout(LayoutKind.Explicit)]
    public struct DataIndirection
    {
        [FieldOffset(0x00)]
        public void** VTable;

        [FieldOffset(0x10)]
        public byte* DataPtr;

        [FieldOffset(0x28)]
        public ulong DataLength;
    }

    public readonly CiByteString FileName()
        => CsHandle.FileName.AsByteString();

    public readonly bool GamePath(out Utf8GamePath path)
        => Utf8GamePath.FromSpan(CsHandle.FileName.AsSpan(), MetaDataComputation.All, out path);

    [FieldOffset(0x00)]
    public CsHandle.ResourceHandle CsHandle;

    [FieldOffset(0x00)]
    public void** VTable;

    [FieldOffset(0x08)]
    public ResourceCategory Category;

    [FieldOffset(0x0C)]
    public ResourceType FileType;

    [FieldOffset(0x28)]
    public uint FileSize;

    [FieldOffset(0x48)]
    public byte* FileNameData;

    [FieldOffset(0x58)]
    public int FileNameLength;

    [FieldOffset(0xA9)]
    public LoadState LoadState;

    [FieldOffset(0xAC)]
    public uint RefCount;


    // Only use these if you know what you are doing.
    // Those are actually only sure to be accessible for DefaultResourceHandles.
    [FieldOffset(0xB0)]
    public DataIndirection* Data;

    [FieldOffset(0xB8)]
    public uint DataLength;

    public (nint Data, int Length) GetData()
        => Data != null
            ? ((nint)Data->DataPtr, (int)Data->DataLength)
            : (nint.Zero, 0);

    public bool SetData(nint data, int length)
    {
        if (Data == null)
            return false;

        Data->DataPtr    = length != 0 ? (byte*)data : null;
        Data->DataLength = (ulong)length;
        DataLength       = (uint)length;
        return true;
    }
}
