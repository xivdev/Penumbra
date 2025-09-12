using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Luna;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;

namespace Penumbra.Interop.Hooks.Objects;

public sealed unsafe class WeaponReload : EventBase<WeaponReload.Arguments, WeaponReload.Priority>, IHookService
{
    public enum Priority
    {
        /// <seealso cref="PathResolving.DrawObjectState"/>
        DrawObjectState = 0,
    }

    public WeaponReload(Logger log, HookManager hooks)
        : base("Reload Weapon", log)
    {
        _postEvent = new PostEvent("Created CharacterBase", log);
        _task      = hooks.CreateHook<Delegate>(Name, Address, Detour, !HookOverrides.Instance.Objects.WeaponReload);
    }

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

    private delegate void Delegate(DrawDataContainer* drawData, uint slot, ulong weapon, byte d, byte e, byte f, byte g, byte h);

    private void Detour(DrawDataContainer* drawData, uint slot, ulong weapon, byte d, byte e, byte f, byte g, byte h)
    {
        var gameObject = drawData->OwnerObject;
        Penumbra.Log.Verbose($"[{Name}] Triggered with drawData: 0x{(nint)drawData:X}, {slot}, {weapon}, {d}, {e}, {f}, {g}, {h}.");
        Invoke(new Arguments(ref *drawData, gameObject, ref *(CharacterWeapon*)(&weapon)));
        _task.Result.Original(drawData, slot, weapon, d, e, f, g, h);
        _postEvent.Invoke(new PostEvent.Arguments(ref *drawData, gameObject));
    }

    public void Subscribe(InAction<PostEvent.Arguments> subscriber, PostEvent.Priority priority)
        => _postEvent.Subscribe(subscriber, priority);

    public void Unsubscribe(InAction<PostEvent.Arguments> subscriber)
        => _postEvent.Unsubscribe(subscriber);


    private readonly PostEvent _postEvent;

    protected override void Dispose(bool disposing)
    {
        _postEvent.Dispose();
    }

    public class PostEvent(string name, Logger log) : EventBase<PostEvent.Arguments, PostEvent.Priority>(name, log)
    {
        public enum Priority
        {
            /// <seealso cref="PathResolving.DrawObjectState"/>
            DrawObjectState = 0,
        }

        public readonly ref struct Arguments(ref DrawDataContainer drawData, Actor owner)
        {
            public readonly ref DrawDataContainer DrawDataContainer = ref drawData;
            public readonly     Actor             Owner             = owner;
        }
    }

    public readonly ref struct Arguments(ref DrawDataContainer drawData, Actor owner, ref CharacterWeapon weapon)
    {
        public readonly ref DrawDataContainer DrawDataContainer = ref drawData;
        public readonly     Actor             Owner             = owner;
        public readonly ref CharacterWeapon   Weapon            = ref weapon;
    }
}
