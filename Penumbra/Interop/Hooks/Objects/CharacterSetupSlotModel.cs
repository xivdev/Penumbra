// Unused for the moment as ModelSafetyCheck.cs should supersede its function.
#if false
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Interop;
using Luna;

namespace Penumbra.Interop.Hooks.Objects;

public sealed unsafe class CharacterSetupSlotModel : FastHook<CharacterBase.Delegates.SetupSlotModel>
{
    public CharacterSetupSlotModel(HookManager hooks)
    {
        Task = hooks.CreateHook<CharacterBase.Delegates.SetupSlotModel>("CharacterBase.SetupSlotModel",
            CharacterBase.Addresses.SetupSlotModel.Value, Detour, !HookOverrides.Instance.Objects.CharacterSetupSlotModel);
    }

    public nint Detour(CharacterBase* pThis, uint slot)
    {
        var ret   = Task.Result!.Original.Invoke(pThis, slot);
        var model = pThis->Models[slot];
        if (model is not null)
        {
            var originalCount = model->MaterialCount;
            var materials     = model->MaterialsSpan;
            var actualCount   = materials.LastIndexOfAnyExcept(default(Pointer<Material>)) + 1;
            if (actualCount < originalCount)
            {
                Penumbra.Log.Warning(
                    $"Trimming materials of an instance of model {model->ModelResourceHandle->FileName}: {originalCount} materials are declared, but only {actualCount} were successfully loaded.");
                model->MaterialCount = actualCount;

                materials = materials[..actualCount];
                if (MemoryExtensions.IndexOf(materials, default(Pointer<Material>)) >= 0)
                    // This should never happen. Should it happen on certain slots, it will be a cause of crashes.
                    Penumbra.Log.Fatal(
                        $"Some materials of an instance of model {model->ModelResourceHandle->FileName} failed to load. This may cause further issues.");
            }
        }

        return ret;
    }
}
#endif
