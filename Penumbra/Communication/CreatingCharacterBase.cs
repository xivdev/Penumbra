using Luna;
using Penumbra.Api.Api;
using Penumbra.Collections;
using Penumbra.GameData.Interop;
using Penumbra.Services;

namespace Penumbra.Communication;

/// <summary> Triggered whenever a character base draw object is being created by the game. </summary>
public sealed class CreatingCharacterBase(Logger log)
    : EventBase<CreatingCharacterBase.Arguments, CreatingCharacterBase.Priority>(nameof(CreatingCharacterBase), log)
{
    public enum Priority
    {
        /// <seealso cref="GameStateApi.CreatingCharacterBase"/>
        Api = 0,

        /// <seealso cref="CrashHandlerService.OnCreatingCharacterBase"/>
        CrashHandler = 0,
    }

    /// <summary> The arguments for a created CharacterBase event. </summary>
    /// <param name="GameObject"> The address of the game object for which a draw object is being created. </param>
    /// <param name="Collection"> The associated collection. </param>
    /// <param name="ModelCharaId"> The address of the model ID that is being used. </param>
    /// <param name="Customize"> The address of the customize array that is being used. </param>
    /// <param name="EquipData"> The address of the equip data array that is being used. </param>
    public readonly record struct Arguments(Actor GameObject, ModCollection Collection, nint ModelCharaId, nint Customize, nint EquipData);
}
