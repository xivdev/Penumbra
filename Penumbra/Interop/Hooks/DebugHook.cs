using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using OtterGui.Services;

namespace Penumbra.Interop.Hooks;

#if DEBUG
public sealed unsafe class DebugHook : IHookService
{
    public const string Signature = "";

    public DebugHook(HookManager hooks)
    {
        if (Signature.Length > 0)
            _task = hooks.CreateHook<Delegate>("Debug Hook", Signature, Detour, true);
    }

    private readonly Task<Hook<Delegate>>? _task;

    public nint Address
        => _task?.Result.Address ?? nint.Zero;

    public void Enable()
        => _task?.Result.Enable();

    public void Disable()
        => _task?.Result.Disable();

    public Task Awaiter
        => _task ?? Task.CompletedTask;

    public bool Finished
        => _task?.IsCompletedSuccessfully ?? true;

    private delegate nint Delegate(ResourceHandle* resourceHandle);

    private nint Detour(ResourceHandle* resourceHandle)
    {
        Penumbra.Log.Information($"[Debug Hook] Triggered with 0x{(nint)resourceHandle:X}.");
        return _task!.Result.Original(resourceHandle);
    }
}
#endif
