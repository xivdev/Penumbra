using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;

namespace Penumbra.Interop.Structs;

[StructLayout( LayoutKind.Explicit )]
public unsafe struct Material
{
    [FieldOffset( 0x10 )]
    public ResourceHandle* ResourceHandle;

    [FieldOffset( 0x28 )]
    public void* MaterialData;

    [FieldOffset( 0x48 )]
    public Texture* Tex1;

    [FieldOffset( 0x60 )]
    public Texture* Tex2;

    [FieldOffset( 0x78 )]
    public Texture* Tex3;
}