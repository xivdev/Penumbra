using Luna;
using Penumbra.Api.Api;
using Penumbra.Collections;
using Penumbra.GameData.Interop;

namespace Penumbra.Communication;

/// <summary> Invoked whenever a draw object is created for a game object. </summary>
public sealed class CreatedCharacterBase(Logger log)
    : EventBase<CreatedCharacterBase.Arguments, CreatedCharacterBase.Priority>(nameof(CreatedCharacterBase), log)
{
    public enum Priority
    {
        /// <seealso cref="GameStateApi.CreatedCharacterBase"/>
        Api = int.MinValue,
    }

    /// <summary> The arguments for a created CharacterBase event. </summary>
    /// <param name="GameObject"> The address of the game object for which a draw object was created. </param>
    /// <param name="Collection"> The associated collection. </param>
    /// <param name="DrawObject"> The newly created draw object for the game object. </param>
    public readonly record struct Arguments(Actor GameObject, ModCollection Collection, Model DrawObject);
}
