using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

namespace Penumbra.Interop.Structs;

public static unsafe class ModelRendererStructs
{
    [StructLayout(LayoutKind.Explicit, Size = 0x28)]
    public struct UnkShaderWrapper
    {
        [FieldOffset(0)]
        public void* Vtbl;

        [FieldOffset(8)]
        public ShaderPackage* ShaderPackage;
    }

    // Unknown size, this is allocated on FUN_1404446c0's stack (E8 ?? ?? ?? ?? FF C3 41 3B DE 72 ?? 48 C7 85)
    [StructLayout(LayoutKind.Explicit)]
    public struct UnkPayload
    {
        [FieldOffset(0)]
        public ModelRenderer.OnRenderModelParams* Params;

        [FieldOffset(8)]
        public ModelResourceHandle* ModelResourceHandle;

        [FieldOffset(0x10)]
        public UnkShaderWrapper* ShaderWrapper;

        [FieldOffset(0x1C)]
        public ushort UnkIndex;
    }
}
