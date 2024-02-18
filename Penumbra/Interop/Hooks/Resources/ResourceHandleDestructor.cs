using Dalamud.Hooking;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop.Hooks.Resources;

public sealed unsafe class ResourceHandleDestructor : EventWrapperPtr<ResourceHandle, ResourceHandleDestructor.Priority>, IHookService
{
    public enum Priority
    {
        /// <seealso cref="PathResolving.SubfileHelper"/>
        SubfileHelper,

        /// <seealso cref="SkinFixer"/>
        SkinFixer,
    }

    public ResourceHandleDestructor(HookManager hooks)
        : base("Destroy ResourceHandle")
        => _task = hooks.CreateHook<Delegate>(Name, Sigs.ResourceHandleDestructor, Detour, true);

    private readonly Task<Hook<Delegate>> _task;

    public nint Address
        => _task.Result.Address;

    public void Enable()
        => _task.Result.Enable();

    public void Disable()
        => _task.Result.Disable();

    public Task Awaiter
        => _task;

    public bool Finished
        => _task.IsCompletedSuccessfully;

    private delegate nint Delegate(ResourceHandle* resourceHandle);

    private nint Detour(ResourceHandle* resourceHandle)
    {
        Penumbra.Log.Excessive($"[{Name}] Triggered with 0x{(nint)resourceHandle:X}.");
        Invoke(resourceHandle);
        return _task.Result.Original(resourceHandle);
    }
}
