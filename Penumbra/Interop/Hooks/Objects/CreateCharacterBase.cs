using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Luna;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;

namespace Penumbra.Interop.Hooks.Objects;

public sealed unsafe class CreateCharacterBase : EventBase<CreateCharacterBase.Arguments, CreateCharacterBase.Priority>, IHookService
{
    public enum Priority
    {
        /// <seealso cref="PathResolving.MetaState.OnCreatingCharacterBase"/>
        MetaState = 0,
    }

    public CreateCharacterBase(Logger log, HookManager hooks)
        : base("Create CharacterBase", log)
    {
        _postEvent = new PostEvent("Created CharacterBase", log);
        _task      = hooks.CreateHook<Delegate>(Name, Address, Detour, !HookOverrides.Instance.Objects.CreateCharacterBase);
    }

    private readonly Task<Hook<Delegate>> _task;

    public nint Address
        => (nint)CharacterBase.MemberFunctionPointers.Create;

    public void Enable()
        => _task.Result.Enable();

    public void Disable()
        => _task.Result.Disable();

    public Task Awaiter
        => _task;

    public bool Finished
        => _task.IsCompletedSuccessfully;

    private delegate CharacterBase* Delegate(ModelCharaId model, CustomizeArray* customize, CharacterArmor* equipment, byte unk);

    private CharacterBase* Detour(ModelCharaId model, CustomizeArray* customize, CharacterArmor* equipment, byte unk)
    {
        Penumbra.Log.Verbose(
            $"[{Name}] Triggered with model: {model.Id}, customize: 0x{(nint)customize:X}, equipment: 0x{(nint)equipment:X}, unk: {unk}.");
        Invoke(new Arguments(ref model, customize, equipment));
        var ret = _task.Result.Original(model, customize, equipment, unk);
        _postEvent.Invoke(new PostEvent.Arguments(model, customize, equipment, ret));
        return ret;
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
            /// <seealso cref="PathResolving.DrawObjectState.OnCharacterBaseCreated"/>
            DrawObjectState = 0,

            /// <seealso cref="PathResolving.MetaState.OnCharacterBaseCreated"/>
            MetaState = 0,
        }

        public readonly struct Arguments(ModelCharaId modelCharaId, CustomizeArray* customize, CharacterArmor* equipment, Model characterBase)
        {
            public readonly ModelCharaId    ModelCharaId  = modelCharaId;
            public readonly CustomizeArray* Customize     = customize;
            public readonly CharacterArmor* Equipment     = equipment;
            public readonly Model           CharacterBase = characterBase;
        }
    }

    public readonly ref struct Arguments(ref ModelCharaId modelCharaId, CustomizeArray* customize, CharacterArmor* equipment)
    {
        public readonly ref ModelCharaId    ModelCharaId = ref modelCharaId;
        public readonly     CustomizeArray* Customize    = customize;
        public readonly     CharacterArmor* Equipment    = equipment;
    }
}
