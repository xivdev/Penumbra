using System;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.GameData.Enums;

namespace Penumbra.Interop.Structs;

[StructLayout( LayoutKind.Explicit )]
public unsafe struct ResourceHandle
{
    [StructLayout( LayoutKind.Explicit )]
    public struct DataIndirection
    {
        [FieldOffset( 0x00 )]
        public void** VTable;

        [FieldOffset( 0x10 )]
        public byte* DataPtr;

        [FieldOffset( 0x28 )]
        public ulong DataLength;
    }

    public const int SsoSize = 15;

    public byte* FileName()
    {
        if( FileNameLength > SsoSize )
        {
            return FileNameData;
        }

        fixed( byte** name = &FileNameData )
        {
            return ( byte* )name;
        }
    }

    public ReadOnlySpan< byte > FileNameSpan()
        => new(FileName(), FileNameLength);

    [FieldOffset( 0x00 )]
    public void** VTable;

    [FieldOffset( 0x08 )]
    public ResourceCategory Category;

    [FieldOffset( 0x0C )]
    public ResourceType FileType;

    [FieldOffset( 0x10 )]
    public uint Id;

    [FieldOffset( 0x48 )]
    public byte* FileNameData;

    [FieldOffset( 0x58 )]
    public int FileNameLength;

    [FieldOffset( 0xAC )]
    public uint RefCount;

    // May return null.
    public static byte* GetData( ResourceHandle* handle )
        => ( ( delegate* unmanaged< ResourceHandle*, byte* > )handle->VTable[ 23 ] )( handle );

    public static ulong GetLength( ResourceHandle* handle )
        => ( ( delegate* unmanaged< ResourceHandle*, ulong > )handle->VTable[ 17 ] )( handle );


    // Only use these if you know what you are doing.
    // Those are actually only sure to be accessible for DefaultResourceHandles.
    [FieldOffset( 0xB0 )]
    public DataIndirection* Data;

    [FieldOffset( 0xB8 )]
    public uint DataLength;

    public (IntPtr Data, int Length) GetData()
        => Data != null
            ? ( ( IntPtr )Data->DataPtr, ( int )Data->DataLength )
            : ( IntPtr.Zero, 0 );

    public bool SetData( IntPtr data, int length )
    {
        if( Data == null )
        {
            return false;
        }

        Data->DataPtr    = length != 0 ? ( byte* )data : null;
        Data->DataLength = ( ulong )length;
        DataLength       = ( uint )length;
        return true;
    }
}