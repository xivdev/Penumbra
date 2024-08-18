using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.GameData;

namespace Penumbra.Interop.Hooks.Objects;

public sealed unsafe class CharacterDestructor : EventWrapperPtr<Character, CharacterDestructor.Priority>, IHookService
{
    public enum Priority
    {
        /// <seealso cref="PathResolving.CutsceneService"/>
        CutsceneService = 0,

        /// <seealso cref="PathResolving.IdentifiedCollectionCache"/>
        IdentifiedCollectionCache = 0,
    }

    public CharacterDestructor(HookManager hooks)
        : base("Character Destructor")
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
        Penumbra.Log.Verbose($"[{Name}] Triggered with 0x{(nint)character:X}.");
        Invoke(character);
        _task.Result.Original(character);
    }
}
