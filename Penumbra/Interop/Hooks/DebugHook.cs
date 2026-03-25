using Dalamud.Hooking;
using Luna;
using Penumbra.Collections.Manager;

namespace Penumbra.Interop.Hooks;

#if DEBUG

public sealed unsafe class DebugHook() : IHookService
{
    public const string Signature = "";

    public DebugHook(CollectionStorage collections, HookManager hooks)
        : this()
    {
        if (Signature.Length > 0)
            _task = hooks.CreateHook<Delegate>("Debug Hook", Signature, Detour, true);
    }

    private readonly Task<Hook<Delegate>?>? _task;

    public nint Address
        => _task?.Result?.Address ?? nint.Zero;

    public void Enable()
        => _task?.Result?.Enable();

    public void Disable()
        => _task?.Result?.Disable();

    public Task Awaiter
        => _task ?? Task.CompletedTask;

    public bool Finished
        => _task?.IsCompletedSuccessfully ?? true;

    private delegate byte Delegate(nint a, nint b, int c, uint d);

    private byte Detour(nint a, nint b, int c, uint d)
    {
        var ret  = _task!.Result!.Original(a, b, c, d);
        Penumbra.Log.Information($"[Debug Hook] Results with 0x{a:X}, 0x{b:X}, {c}, {d} -> {ret}.");
        return ret;
    }
}
#endif
