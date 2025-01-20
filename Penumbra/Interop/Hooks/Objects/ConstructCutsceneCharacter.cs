using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using OtterGui.Classes;
using OtterGui.Services;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;

namespace Penumbra.Interop.Hooks.Objects;

public sealed unsafe class ConstructCutsceneCharacter : EventWrapperPtr<Character, ConstructCutsceneCharacter.Priority>, IHookService
{
    private readonly GameState     _gameState;
    private readonly ObjectManager _objects;

    public enum Priority
    {
        /// <seealso cref="PathResolving.CutsceneService.OnSetupPlayerNpc"/>
        CutsceneService = 0,
    }

    public ConstructCutsceneCharacter(GameState gameState, HookManager hooks, ObjectManager objects)
        : base("ConstructCutsceneCharacter")
    {
        _gameState = gameState;
        _objects   = objects;
        _task      = hooks.CreateHook<Delegate>(Name, Sigs.ConstructCutsceneCharacter, Detour, !HookOverrides.Instance.Objects.ConstructCutsceneCharacter);
    }

    private readonly Task<Hook<Delegate>> _task;

    public delegate int Delegate(SetupPlayerNpc.SchedulerStruct* scheduler);

    public int Detour(SetupPlayerNpc.SchedulerStruct* scheduler)
    {
        // This is the function that actually creates the new game object
        // and fills it into the object table at a free index etc.
        var ret       = _task.Result.Original(scheduler);
        // Check for the copy state from SetupPlayerNpc.
        if (_gameState.CharacterAssociated.Value)
        {
            // If the newly created character exists, invoke the event.
            var character = _objects[ret + (int)ScreenActor.CutsceneStart].AsCharacter;
            if (character != null)
            {
                Invoke(character);
                Penumbra.Log.Verbose(
                    $"[{Name}] Created indirect copy of player character at 0x{(nint)character}, index {character->ObjectIndex}.");
            }
            _gameState.CharacterAssociated.Value = false;
        }

        return ret;
    }

    public IntPtr Address
        => _task.Result.Address;

    public void Enable()
        => _task.Result.Enable();

    public void Disable()
        => _task.Result.Disable();

    public Task Awaiter
        => _task;

    public bool Finished
        => _task.IsCompletedSuccessfully;
}
