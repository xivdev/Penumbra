using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.GameData.Structs;

namespace Penumbra.Interop.Hooks.Objects;

public sealed unsafe class WeaponReload : EventWrapperPtr<DrawDataContainer, Character, CharacterWeapon, WeaponReload.Priority>, IHookService
{
    public enum Priority
    {
        /// <seealso cref="PathResolving.DrawObjectState"/>
        DrawObjectState = 0,
    }

    public WeaponReload(HookManager hooks)
        : base("Reload Weapon")
        => _task = hooks.CreateHook<Delegate>(Name, Address, Detour, !HookOverrides.Instance.Objects.WeaponReload);

    private readonly Task<Hook<Delegate>> _task;

    public nint Address
        => (nint)DrawDataContainer.MemberFunctionPointers.LoadWeapon;

    public void Enable()
        => _task.Result.Enable();

    public void Disable()
        => _task.Result.Disable();

    public Task Awaiter
        => _task;

    public bool Finished
        => _task.IsCompletedSuccessfully;

    private delegate void Delegate(DrawDataContainer* drawData, uint slot, ulong weapon, byte d, byte e, byte f, byte g);

    private void Detour(DrawDataContainer* drawData, uint slot, ulong weapon, byte d, byte e, byte f, byte g)
    {
        var gameObject = drawData->OwnerObject;
        Penumbra.Log.Verbose($"[{Name}] Triggered with drawData: 0x{(nint)drawData:X}, {slot}, {weapon}, {d}, {e}, {f}, {g}.");
        Invoke(drawData, gameObject, (CharacterWeapon*)(&weapon));
        _task.Result.Original(drawData, slot, weapon, d, e, f, g);
        _postEvent.Invoke(drawData, gameObject);
    }

    public void Subscribe(ActionPtr<DrawDataContainer, Character> subscriber, PostEvent.Priority priority)
        => _postEvent.Subscribe(subscriber, priority);

    public void Unsubscribe(ActionPtr<DrawDataContainer, Character> subscriber)
        => _postEvent.Unsubscribe(subscriber);


    private readonly PostEvent _postEvent = new("Created CharacterBase");

    protected override void Dispose(bool disposing)
    {
        _postEvent.Dispose();
    }

    public class PostEvent(string name) : EventWrapperPtr<DrawDataContainer, Character, PostEvent.Priority>(name)
    {
        public enum Priority
        {
            /// <seealso cref="PathResolving.DrawObjectState"/>
            DrawObjectState = 0,
        }
    }
}
