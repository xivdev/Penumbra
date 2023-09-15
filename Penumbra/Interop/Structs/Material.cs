using FFXIVClientStructs.FFXIV.Client.Graphics.Render;

namespace Penumbra.Interop.Structs;

[StructLayout( LayoutKind.Explicit, Size = 0x40 )]
public unsafe struct Material
{
    [FieldOffset( 0x10 )]
    public MtrlResource* ResourceHandle;

    [FieldOffset( 0x18 )]
    public uint ShaderPackageFlags;

    [FieldOffset( 0x20 )]
    public uint* ShaderKeys;

    public int ShaderKeyCount
        => (int)((uint*)Textures - ShaderKeys);

    [FieldOffset( 0x28 )]
    public ConstantBuffer* MaterialParameter;

    [FieldOffset( 0x30 )]
    public TextureEntry* Textures;

    [FieldOffset( 0x38 )]
    public ushort TextureCount;

    public Texture* Texture( int index ) => Textures[index].ResourceHandle->KernelTexture;

    [StructLayout( LayoutKind.Explicit, Size = 0x18 )]
    public struct TextureEntry
    {
        [FieldOffset( 0x00 )]
        public uint Id;

        [FieldOffset( 0x08 )]
        public TextureResourceHandle* ResourceHandle;

        [FieldOffset( 0x10 )]
        public uint SamplerFlags;
    }

    public ReadOnlySpan<TextureEntry> TextureSpan
        => new(Textures, TextureCount);
}