using Dalamud.Hooking;
using OtterGui.Services;
using Penumbra.Interop.Structs;

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

    private delegate nint Delegate(ResourceHandle* a, int b, int c);

    private nint Detour(ResourceHandle* a, int b, int c)
    {
        var ret = _task!.Result.Original(a, b, c);
        Penumbra.Log.Information($"[Debug Hook] Results with 0x{(nint)a:X}, {b}, {c} -> 0x{ret:X}.");
        return ret;
    }
}
#endif
