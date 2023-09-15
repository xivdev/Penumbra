using OtterGui.Classes;
using Penumbra.Api;
using Penumbra.Collections;

namespace Penumbra.Communication;

/// <summary> <list type="number">
///     <item>Parameter is the game object for which a draw object is created. </item>
///     <item>Parameter is the applied collection. </item>
///     <item>Parameter is the created draw object. </item>
/// </list> </summary>
public sealed class CreatedCharacterBase : EventWrapper<Action<nint, ModCollection, nint>, CreatedCharacterBase.Priority>
{
    public enum Priority
    {
        /// <seealso cref="PenumbraApi.CreatedCharacterBase"/>
        Api = int.MinValue,
    }

    public CreatedCharacterBase()
        : base(nameof(CreatedCharacterBase))
    { }

    public void Invoke(nint gameObject, ModCollection appliedCollection, nint drawObject)
        => Invoke(this, gameObject, appliedCollection, drawObject);
}
