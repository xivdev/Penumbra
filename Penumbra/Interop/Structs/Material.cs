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

    [FieldOffset( 0x30 )]
    public void** Textures;

    public Texture* Texture( int index ) => ( Texture* )Textures[3 * index + 1];
}