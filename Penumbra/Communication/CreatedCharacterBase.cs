using System;
using Penumbra.Util;

namespace Penumbra.Communication;

/// <summary> <list type="number">
///     <item>Parameter is the game object for which a draw object is created. </item>
///     <item>Parameter is the name of the applied collection. </item>
///     <item>Parameter is the created draw object. </item>
/// </list> </summary>
public sealed class CreatedCharacterBase : EventWrapper<Action<nint, string, nint>, CreatedCharacterBase.Priority>
{
    public enum Priority
    {
        /// <seealso cref="Api.PenumbraApi.CreatedCharacterBase"/>
        Api = 0,
    }

    public CreatedCharacterBase()
        : base(nameof(CreatedCharacterBase))
    { }

    public void Invoke(nint gameObject, string appliedCollectionName, nint drawObject)
        => Invoke(this, gameObject, appliedCollectionName, drawObject);
}
