using System.Runtime.InteropServices;

namespace Penumbra.Interop.Structs;

[StructLayout( LayoutKind.Explicit )]
public unsafe struct MtrlResource
{
    [FieldOffset( 0x00 )]
    public ResourceHandle Handle;

    [FieldOffset( 0xD0 )]
    public ushort* TexSpace; // Contains the offsets for the tex files inside the string list.

    [FieldOffset( 0xE0 )]
    public byte* StringList;

    [FieldOffset( 0xF8 )]
    public ushort ShpkOffset;

    [FieldOffset( 0xFA )]
    public byte NumTex;

    public byte* ShpkString
        => StringList + ShpkOffset;

    public byte* TexString( int idx )
        => StringList + *( TexSpace + 4 + idx * 8 );

    public bool TexIsDX11( int idx )
        => *(TexSpace + 5 + idx * 8) >= 0x8000;
}