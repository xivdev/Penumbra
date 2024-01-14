using Dalamud.Hooking;
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

    private delegate void Delegate(nint a, int b, nint c, float* d);

    private void Detour(nint a, int b, nint c, float* d)
    {
        _task!.Result.Original(a,        b, c, d);
        Penumbra.Log.Information($"[Debug Hook] Results with 0x{a:X} {b} {c:X} {d[0]} {d[1]} {d[2]} {d[3]}.");
    }
}
#endif
