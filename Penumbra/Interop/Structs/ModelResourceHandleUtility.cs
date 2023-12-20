using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Penumbra.GameData;

namespace Penumbra.Interop.Structs;

// TODO submit this to ClientStructs
public class ModelResourceHandleUtility
{
    public ModelResourceHandleUtility(IGameInteropProvider interop)
        => interop.InitializeFromAttributes(this);

    [Signature(Sigs.GetMaterialFileNameBySlot)]
    private static nint _getMaterialFileNameBySlot = nint.Zero;

    public static unsafe byte* GetMaterialFileNameBySlot(ModelResourceHandle* handle, uint slot)
        => ((delegate* unmanaged<ModelResourceHandle*, uint, byte*>)_getMaterialFileNameBySlot)(handle, slot);
}
