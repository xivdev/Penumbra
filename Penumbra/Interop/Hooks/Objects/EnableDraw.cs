using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using OtterGui.Services;
using Penumbra.GameData;

namespace Penumbra.Interop.Hooks.Objects;

/// <summary>
/// EnableDraw is what creates DrawObjects for gameObjects,
/// so we always keep track of the current GameObject to be able to link it to the DrawObject.
/// </summary>
public sealed unsafe class EnableDraw : IHookService
{
    private readonly Task<Hook<Delegate>> _task;
    private readonly GameState _state;

    public EnableDraw(HookManager hooks, GameState state)
    {
        _state = state;
        _task  = hooks.CreateHook<Delegate>("Enable Draw", Sigs.EnableDraw, Detour, !HookOverrides.Instance.Objects.EnableDraw);
    }

    private delegate void Delegate(GameObject* gameObject);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Detour(GameObject* gameObject)
    {
        _state.QueueGameObject(gameObject);
        Penumbra.Log.Excessive($"[Enable Draw] Invoked on 0x{(nint)gameObject:X} at {gameObject->ObjectIndex}.");
        _task.Result.Original.Invoke(gameObject);
        _state.DequeueGameObject();
    }

    public Task Awaiter
        => _task;

    public bool Finished
        => _task.IsCompletedSuccessfully;

    public nint Address
        => _task.Result.Address;

    public void Enable()
        => _task.Result.Enable();

    public void Disable()
        => _task.Result.Disable();
}
