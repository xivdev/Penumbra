using FFXIVClientStructs.FFXIV.Client.Game.Character;
using OtterGui.Services;
using Penumbra.GameData;

namespace Penumbra.Interop.Hooks.Objects;

public sealed unsafe class SetupPlayerNpc : FastHook<SetupPlayerNpc.Delegate>
{
    private readonly GameState _gameState;

    public SetupPlayerNpc(GameState gameState, HookManager hooks)
    {
        _gameState = gameState;
        Task = hooks.CreateHook<Delegate>("SetupPlayerNPC", Sigs.SetupPlayerNpc, Detour,
            !HookOverrides.Instance.Objects.SetupPlayerNpc);
    }

    public delegate SchedulerStruct* Delegate(byte* npcType, nint unk, NpcSetupData* setupData);

    public SchedulerStruct* Detour(byte* npcType, nint unk, NpcSetupData* setupData)
    {
        // This function actually seems to generate all NPC.

        // If an ENPC is being created, check the creation parameters.
        // If CopyPlayerCustomize is true, the event NPC gets a timeline that copies its customize and glasses from the local player.
        // Keep track of this, so we can associate the actor to be created for this with the player character, see ConstructCutsceneCharacter.
        if (setupData->CopyPlayerCustomize && npcType != null && *npcType is 8)
            _gameState.CharacterAssociated.Value = true;

        var ret = Task.Result.Original.Invoke(npcType, unk, setupData);
        Penumbra.Log.Excessive(
            $"[Setup Player NPC] Invoked for type {*npcType} with 0x{unk:X} and Copy Player Customize: {setupData->CopyPlayerCustomize}.");
        return ret;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct NpcSetupData
    {
        [FieldOffset(0x0B)]
        private byte _copyPlayerCustomize;

        public bool CopyPlayerCustomize
        {
            get => _copyPlayerCustomize != 0;
            set => _copyPlayerCustomize = value ? (byte)1 : (byte)0;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct SchedulerStruct
    {
        public static Character* GetCharacter(SchedulerStruct* s)
            => ((delegate* unmanaged<SchedulerStruct*, Character*>**)s)[0][19](s);
    }
}
