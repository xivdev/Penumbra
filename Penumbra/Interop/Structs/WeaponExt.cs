using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace Penumbra.Interop.Structs;

[StructLayout( LayoutKind.Explicit )]
public unsafe struct WeaponExt
{
    [FieldOffset( 0x0 )]
    public Weapon Weapon;

    [FieldOffset( 0xA8 )]
    public RenderModel** WeaponRenderModel;
}