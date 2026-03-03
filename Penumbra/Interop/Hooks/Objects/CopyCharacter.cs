using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Luna;
using Penumbra.GameData.Interop;

namespace Penumbra.Interop.Hooks.Objects;

public sealed unsafe class CopyCharacter : EventBase<CopyCharacter.Arguments, CopyCharacter.Priority>, IHookService
{
    public enum Priority
    {
        /// <seealso cref="PathResolving.CutsceneService.OnCharacterCopy"/>
        CutsceneService = 0,
    }

    public CopyCharacter(Logger log, HookManager hooks)
        : base("Copy Character", log)
        => _task = hooks.CreateHook<Delegate>(Name, Address, Detour, !HookOverrides.Instance.Objects.CopyCharacter);

    private readonly Task<Hook<Delegate>> _task;

    public nint Address
        => (nint)CharacterSetupContainer.MemberFunctionPointers.CopyFromCharacter;

    public void Enable()
        => _task.Result.Enable();

    public void Disable()
        => _task.Result.Disable();

    public Task Awaiter
        => _task;

    public bool Finished
        => _task.IsCompletedSuccessfully;

    private delegate ulong Delegate(CharacterSetupContainer* target, Character* source, uint unk);

    private ulong Detour(CharacterSetupContainer* target, Character* source, uint unk)
    {
        var character = target->OwnerObject;
        Penumbra.Log.Verbose($"[{Name}] Triggered with target: 0x{(nint)target:X}, source : 0x{(nint)source:X} unk: {unk}.");
        Invoke(new Arguments(character, source));
        return _task.Result.Original(target, source, unk);
    }

    /// <summary> The arguments for a copy character event. </summary>
    /// <param name="TargetCharacter"> The character that is being created by a copy. </param>
    /// <param name="SourceCharacter"> The character that is being copied. </param>
    public readonly record struct Arguments(Actor TargetCharacter, Actor SourceCharacter);
}
