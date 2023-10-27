using OtterGui.Classes;
using Penumbra.Api.Enums;
using Penumbra.GameData.Enums;

namespace Penumbra.Communication;

/// <summary>
/// Triggered when a Changed Item in Penumbra is clicked.
/// <list type="number">
///     <item>Parameter is the clicked mouse button. </item>
///     <item>Parameter is the clicked object data if any. </item>
/// </list>
/// </summary>
public sealed class ChangedItemClick : EventWrapper<Action<MouseButton, object?>, ChangedItemClick.Priority>
{
    public enum Priority
    {
        /// <seealso cref="Api.PenumbraApi.ChangedItemClicked"/>
        Default = 0,

        /// <seealso cref="Penumbra.SetupApi"/>
        Link = 1,
    }

    public ChangedItemClick()
        : base(nameof(ChangedItemClick))
    { }

    public void Invoke(MouseButton button, object? data)
        => Invoke(this, button, data);
}
