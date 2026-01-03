using Dalamud.Hooking;
using Luna;
using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.Interop.PathResolving;
using Penumbra.String;

namespace Penumbra.Interop.Hooks;

#if DEBUG

public sealed unsafe class DebugHook(CollectionStorage collections) : Luna.IHookService
{
    public const string Signature = "";

    public DebugHook(CollectionStorage collections, HookManager hooks)
        : this(collections)
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

    private delegate nint Delegate(nint a, nint b, nint c, nint d, uint e, nint f, nint g);

    private nint Detour(nint a, nint b, nint c, nint d, uint e, nint f, nint g)
    {
        var path = new CiByteString((byte*)c);
        var collection = PathDataHandler.Split(path.Span, out _, out var additionalData) && PathDataHandler.Read(additionalData, out var data)
            ? collections.ByLocalId(data.Collection)
            : ModCollection.Empty;

        var ret  = _task!.Result.Original(a, b, c, d, e, f, g);

        
        Penumbra.Log.Information($"[Debug Hook] Results with 0x{a:X}, 0x{b:X}, 0x{c:X} ({path}, {collection}) 0x{d:X} {e} 0x{f:X} {g} -> 0x{ret:X}.");
        return ret;
    }
}
#endif
