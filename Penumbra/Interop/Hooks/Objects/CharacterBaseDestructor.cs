using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Classes;

namespace Penumbra.Interop.Hooks.Objects;

public sealed unsafe class CharacterBaseDestructor : EventWrapperPtr<CharacterBase, CharacterBaseDestructor.Priority>, Luna.IHookService
{
    public enum Priority
    {
        /// <seealso cref="PathResolving.DrawObjectState.OnCharacterBaseDestructor"/>
        DrawObjectState = 0,

        /// <seealso cref="UI.AdvancedWindow.Materials.MtrlTab.UnbindFromDrawObjectMaterialInstances"/>
        MtrlTab = -1000,
    }

    public CharacterBaseDestructor(Luna.HookManager hooks)
        : base("Destroy CharacterBase")
        => _task = hooks.CreateHook<Delegate>(Name, Address, Detour, !HookOverrides.Instance.Objects.CharacterBaseDestructor);

    private readonly Task<Hook<Delegate>> _task;

    public nint Address
        => (nint)CharacterBase.MemberFunctionPointers.Destroy;

    public void Enable()
        => _task.Result.Enable();

    public void Disable()
        => _task.Result.Disable();

    public Task Awaiter
        => _task;

    public bool Finished
        => _task.IsCompletedSuccessfully;

    private delegate nint Delegate(CharacterBase* characterBase);

    private nint Detour(CharacterBase* characterBase)
    {
        Penumbra.Log.Excessive($"[{Name}] Triggered with 0x{(nint)characterBase:X}.");
        Invoke(characterBase);
        return _task.Result.Original(characterBase);
    }
}
