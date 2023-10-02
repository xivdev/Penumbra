using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace Penumbra.Interop.Structs;

public unsafe class TextureUtility
{
    public TextureUtility(IGameInteropProvider interop)
        => interop.InitializeFromAttributes(this);


    [Signature("E8 ?? ?? ?? ?? 8B 0F 48 8D 54 24")]
    private static nint _textureCreate2D = nint.Zero;

    [Signature("E9 ?? ?? ?? ?? 8B 02 25")]
    private static nint _textureInitializeContents = nint.Zero;

    public static Texture* Create2D(Device* device, int* size, byte mipLevel, uint textureFormat, uint flags, uint unk)
        => ((delegate* unmanaged<Device*, int*, byte, uint, uint, uint, Texture*>)_textureCreate2D)(device, size, mipLevel, textureFormat,
            flags, unk);

    public static bool InitializeContents(Texture* texture, void* contents)
        => ((delegate* unmanaged<Texture*, void*, bool>)_textureInitializeContents)(texture, contents);

    public static void IncRef(Texture* texture)
        => ((delegate* unmanaged<Texture*, void>)(*(void***)texture)[2])(texture);

    public static void DecRef(Texture* texture)
        => ((delegate* unmanaged<Texture*, void>)(*(void***)texture)[3])(texture);
}
