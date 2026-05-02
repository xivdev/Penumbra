using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Interop;
using Luna;
using Penumbra.GameData;

namespace Penumbra.Interop.Hooks.Objects;

// TODO ClientStructs-ify (aers/FFXIVClientStructs#1805)
public sealed unsafe class CharacterSetupSlotModel : FastHook<CharacterSetupSlotModel.Delegate>
{
    public CharacterSetupSlotModel(HookManager hooks)
    {
        Task = hooks.CreateHook<Delegate>("CharacterBase.SetupSlotModel", Sigs.CharacterSetupSlotModel, Detour,
            !HookOverrides.Instance.Objects.CharacterSetupSlotModel);
    }

    public delegate nint Delegate(CharacterBase* pThis, uint slot);

    public nint Detour(CharacterBase* pThis, uint slot)
    {
        var ret = Task.Result!.Original.Invoke(pThis, slot);
        MitigateMaterialLoadErrors(pThis->Models[slot]);

        return ret;
    }

    public static void MitigateMaterialLoadErrors(Model* model)
    {
        if (model is null)
            return;

        var originalCount = model->MaterialCount;
        var materials     = model->MaterialsSpan;
        var actualCount   = materials.LastIndexOfAnyExcept(default(Pointer<Material>)) + 1;
        if (actualCount >= originalCount)
            return;

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
