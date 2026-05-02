using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Interop;
using Luna;

namespace Penumbra.Interop.Hooks.Objects;

// TODO ClientStructs-ify (aers/FFXIVClientStructs#1805)
public sealed unsafe class CharacterSetupSlotModel : FastHook<CharacterSetupSlotModel.Delegate>
{
    public CharacterSetupSlotModel(HookManager hooks)
    {
        Task = hooks.CreateHook<Delegate>("CharacterBase.SetupSlotModel", "89 54 24 ?? 55 56 41 56 48 81 EC", Detour,
            !HookOverrides.Instance.Objects.CharacterSetupSlotModel);
    }

    public delegate nint Delegate(CharacterBase* pThis, uint slot);

    public nint Detour(CharacterBase* pThis, uint slot)
    {
        var ret   = Task.Result!.Original.Invoke(pThis, slot);
        var model = pThis->Models[slot];
        if (model is not null)
        {
            var origialCount = model->MaterialCount;
            var materials    = model->MaterialsSpan;
            var actualCount  = materials.LastIndexOfAnyExcept(default(Pointer<Material>)) + 1;
            if (actualCount < origialCount)
            {
                Penumbra.Log.Warning(
                    $"Trimming materials of an instance of model {model->ModelResourceHandle->FileName}: {origialCount} materials are declared, but only {actualCount} were successfully loaded.");
                model->MaterialCount = actualCount;

                materials = materials[..actualCount];
                if (MemoryExtensions.IndexOf(materials, default(Pointer<Material>)) >= 0)
                {
                    // This should never happen. Should it happen on certain slots, it will be a cause of crashes.
                    Penumbra.Log.Fatal(
                        $"Some materials of an instance of model {model->ModelResourceHandle->FileName} failed to load. This may cause further issues.");
                }
            }
        }

        return ret;
    }
}
