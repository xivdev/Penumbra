using OtterGui.Classes;
using Penumbra.Api;

namespace Penumbra.Communication;

/// <summary>
/// Triggered whenever a character base draw object is being created by the game.
/// <list type="number">
///     <item>Parameter is the game object for which a draw object is created. </item>
///     <item>Parameter is the name of the applied collection. </item>
///     <item>Parameter is a pointer to the model id (an uint). </item>
///     <item>Parameter is a pointer to the customize array. </item>
///     <item>Parameter is a pointer to the equip data array. </item>
/// </list> </summary>
public sealed class CreatingCharacterBase : EventWrapper<Action<nint, string, nint, nint, nint>, CreatingCharacterBase.Priority>
{
    public enum Priority
    {
        /// <seealso cref="PenumbraApi.CreatingCharacterBase"/>
        Api = 0,
    }

    public CreatingCharacterBase()
        : base(nameof(CreatingCharacterBase))
    { }

    public void Invoke(nint gameObject, string appliedCollectionName, nint modelIdAddress, nint customizeArrayAddress, nint equipDataAddress)
        => Invoke(this, gameObject, appliedCollectionName, modelIdAddress, customizeArrayAddress, equipDataAddress);
}
