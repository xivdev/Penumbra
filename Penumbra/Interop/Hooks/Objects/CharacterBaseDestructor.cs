using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Luna;
using Penumbra.GameData.Interop;

namespace Penumbra.Interop.Hooks.Objects;

public sealed unsafe class CharacterBaseDestructor : EventBase<CharacterBaseDestructor.Arguments, CharacterBaseDestructor.Priority>,
    IHookService
{
    public enum Priority
    {
        /// <seealso cref="PathResolving.DrawObjectState.OnCharacterBaseDestructor"/>
        DrawObjectState = 0,

        /// <seealso cref="UI.AdvancedWindow.Materials.MtrlTab.UnbindFromDrawObjectMaterialInstances"/>
        MtrlTab = -1000,
    }

    public CharacterBaseDestructor(Logger log, HookManager hooks)
        : base("Destroy CharacterBase", log)
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
        Invoke(new Arguments(characterBase));
        return _task.Result.Original(characterBase);
    }

    /// <summary> The arguments for a character base destructor event. </summary>
    /// <param name="CharacterBase"> The model that is being destroyed. </param>
    public readonly record struct Arguments(Model CharacterBase);
}
