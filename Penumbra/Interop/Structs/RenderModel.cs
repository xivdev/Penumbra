using FFXIVClientStructs.FFXIV.Client.Graphics.Render;

namespace Penumbra.Interop.Structs;

[StructLayout( LayoutKind.Explicit )]
public unsafe struct RenderModel
{
    [FieldOffset( 0x18 )]
    public RenderModel* PreviousModel;

    [FieldOffset( 0x20 )]
    public RenderModel* NextModel;

    [FieldOffset( 0x30 )]
    public ResourceHandle* ResourceHandle;

    [FieldOffset( 0x40 )]
    public Skeleton* Skeleton;

    [FieldOffset( 0x58 )]
    public void** BoneList;

    [FieldOffset( 0x60 )]
    public int BoneListCount;

    [FieldOffset( 0x70 )]
    private void* UnkDXBuffer1;

    [FieldOffset( 0x78 )]
    private void* UnkDXBuffer2;

    [FieldOffset( 0x80 )]
    private void* UnkDXBuffer3;

    [FieldOffset( 0x98 )]
    public void** Materials;

    [FieldOffset( 0xA0 )]
    public int MaterialCount;
}