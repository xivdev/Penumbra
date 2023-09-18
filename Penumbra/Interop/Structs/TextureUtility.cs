using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;

namespace Penumbra.Interop.Structs;

public static unsafe class TextureUtility
{
    private static readonly Functions Funcs = new();

    public static Texture* Create2D(Device* device, int* size, byte mipLevel, uint textureFormat, uint flags, uint unk)
        => ((delegate* unmanaged<Device*, int*, byte, uint, uint, uint, Texture*>)Funcs.TextureCreate2D)(device, size, mipLevel, textureFormat,
            flags, unk);

    public static bool InitializeContents(Texture* texture, void* contents)
        => ((delegate* unmanaged<Texture*, void*, bool>)Funcs.TextureInitializeContents)(texture, contents);

    public static void IncRef(Texture* texture)
        => ((delegate* unmanaged<Texture*, void>)(*(void***)texture)[2])(texture);

    public static void DecRef(Texture* texture)
        => ((delegate* unmanaged<Texture*, void>)(*(void***)texture)[3])(texture);

    private sealed class Functions
    {
        [Signature("E8 ?? ?? ?? ?? 8B 0F 48 8D 54 24")]
        public nint TextureCreate2D = nint.Zero;

        [Signature("E9 ?? ?? ?? ?? 8B 02 25")]
        public nint TextureInitializeContents = nint.Zero;

        public Functions()
        {
            SignatureHelper.Initialise(this);
        }
    }
}
