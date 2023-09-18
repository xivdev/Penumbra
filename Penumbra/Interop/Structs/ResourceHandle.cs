using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Api.Enums;
using Penumbra.GameData;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Structs;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct TextureResourceHandle
{
    [FieldOffset(0x0)]
    public ResourceHandle Handle;

    [FieldOffset(0x38)]
    public IntPtr Unk;

    [FieldOffset(0x118)]
    public Texture* KernelTexture;

    [FieldOffset(0x20)]
    public IntPtr NewKernelTexture;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct ShaderPackageResourceHandle
{
    [FieldOffset(0x0)]
    public ResourceHandle Handle;

    [FieldOffset(0xB0)]
    public ShaderPackage* ShaderPackage;
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

    public const int SsoSize = 15;

    public byte* FileNamePtr()
    {
        if (FileNameLength > SsoSize)
            return FileNameData;

        fixed (byte** name = &FileNameData)
        {
            return (byte*)name;
        }
    }

    public ByteString FileName()
        => ByteString.FromByteStringUnsafe(FileNamePtr(), FileNameLength, true);

    public ReadOnlySpan<byte> FileNameAsSpan()
        => new(FileNamePtr(), FileNameLength);

    public bool GamePath(out Utf8GamePath path)
        => Utf8GamePath.FromSpan(FileNameAsSpan(), out path);

    [FieldOffset(0x00)]
    public void** VTable;

    [FieldOffset(0x08)]
    public ResourceCategory Category;

    [FieldOffset(0x0C)]
    public ResourceType FileType;

    [FieldOffset(0x10)]
    public uint Id;

    [FieldOffset(0x28)]
    public uint FileSize;

    [FieldOffset(0x2C)]
    public uint FileSize2;

    [FieldOffset(0x34)]
    public uint FileSize3;

    [FieldOffset(0x48)]
    public byte* FileNameData;

    [FieldOffset(0x58)]
    public int FileNameLength;

    [FieldOffset(0xA9)]
    public LoadState LoadState;

    [FieldOffset(0xAC)]
    public uint RefCount;

    // May return null.
    public static byte* GetData(ResourceHandle* handle)
        => ((delegate* unmanaged< ResourceHandle*, byte* >)handle->VTable[Offsets.ResourceHandleGetDataVfunc])(handle);

    public static ulong GetLength(ResourceHandle* handle)
        => ((delegate* unmanaged< ResourceHandle*, ulong >)handle->VTable[Offsets.ResourceHandleGetLengthVfunc])(handle);


    // Only use these if you know what you are doing.
    // Those are actually only sure to be accessible for DefaultResourceHandles.
    [FieldOffset(0xB0)]
    public DataIndirection* Data;

    [FieldOffset(0xB8)]
    public uint DataLength;

    public (IntPtr Data, int Length) GetData()
        => Data != null
            ? ((IntPtr)Data->DataPtr, (int)Data->DataLength)
            : (IntPtr.Zero, 0);

    public bool SetData(IntPtr data, int length)
    {
        if (Data == null)
            return false;

        Data->DataPtr    = length != 0 ? (byte*)data : null;
        Data->DataLength = (ulong)length;
        DataLength       = (uint)length;
        return true;
    }
}
