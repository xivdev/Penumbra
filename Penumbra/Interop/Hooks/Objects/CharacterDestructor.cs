using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Luna;
using Penumbra.GameData;
using Penumbra.GameData.Interop;

namespace Penumbra.Interop.Hooks.Objects;

public sealed unsafe class CharacterDestructor : EventBase<CharacterDestructor.Arguments, CharacterDestructor.Priority>, IHookService
{
    public enum Priority
    {
        /// <seealso cref="PathResolving.CutsceneService"/>
        CutsceneService = 0,

        /// <seealso cref="PathResolving.IdentifiedCollectionCache"/>
        IdentifiedCollectionCache = 0,

        /// <seealso cref="PathResolving.DrawObjectState.OnCharacterDestructor"/>
        DrawObjectState = 0,
    }

    public CharacterDestructor(Logger log, HookManager hooks)
        : base("Character Destructor", log)
        => _task = hooks.CreateHook<Delegate>(Name, Sigs.CharacterDestructor, Detour, !HookOverrides.Instance.Objects.CharacterDestructor);

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

    private delegate void Delegate(Character* character);

    private void Detour(Character* character)
    {
        Penumbra.Log.Excessive($"[{Name}] Triggered with 0x{(nint)character:X}.");
        Invoke(new Arguments(character));
        _task.Result.Original(character);
    }

    /// <summary> The arguments for a character destructor event. </summary>
    /// <param name="Character"> The game object that is being destroyed. </param>
    public readonly record struct Arguments(Actor Character);
}
