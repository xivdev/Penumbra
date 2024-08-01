using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.GameData.Structs;

namespace Penumbra.Interop.Hooks.Objects;

public sealed unsafe class CreateCharacterBase : EventWrapperPtr<ModelCharaId, CustomizeArray, CharacterArmor, CreateCharacterBase.Priority>, IHookService
{
    public enum Priority
    {
        /// <seealso cref="PathResolving.MetaState.OnCreatingCharacterBase"/>
        MetaState = 0,
    }

    public CreateCharacterBase(HookManager hooks)
        : base("Create CharacterBase")
        => _task = hooks.CreateHook<Delegate>(Name, Address, Detour, !HookOverrides.Instance.Objects.CreateCharacterBase);

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
        Penumbra.Log.Verbose($"[{Name}] Triggered with model: {model.Id}, customize: 0x{(nint)customize:X}, equipment: 0x{(nint)equipment:X}, unk: {unk}.");
        Invoke(&model, customize, equipment);
        var ret = _task.Result.Original(model, customize, equipment, unk);
        _postEvent.Invoke(model, customize, equipment, ret);
        return ret;
    }

    public void Subscribe(ActionPtr234<ModelCharaId, CustomizeArray, CharacterArmor, CharacterBase> subscriber, PostEvent.Priority priority)
        => _postEvent.Subscribe(subscriber, priority);

    public void Unsubscribe(ActionPtr234<ModelCharaId, CustomizeArray, CharacterArmor, CharacterBase> subscriber)
        => _postEvent.Unsubscribe(subscriber);


    private readonly PostEvent _postEvent = new("Created CharacterBase");

    protected override void Dispose(bool disposing)
    {
        _postEvent.Dispose();
    }

    public class PostEvent(string name) : EventWrapperPtr234<ModelCharaId, CustomizeArray, CharacterArmor, CharacterBase, PostEvent.Priority>(name)
    {
        public enum Priority
        {
            /// <seealso cref="PathResolving.DrawObjectState.OnCharacterBaseCreated"/>
            DrawObjectState = 0,

            /// <seealso cref="PathResolving.MetaState.OnCharacterBaseCreated"/>
            MetaState = 0,
        }
    }
}
