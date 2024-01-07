using OtterGui.Classes;
using Penumbra.Api.Enums;

namespace Penumbra.Communication;

/// <summary>
/// Triggered when a Changed Item in Penumbra is clicked.
/// <list type="number">
///     <item>Parameter is the clicked mouse button. </item>
///     <item>Parameter is the clicked object data if any. </item>
/// </list>
/// </summary>
public sealed class ChangedItemClick() : EventWrapper<MouseButton, object?, ChangedItemClick.Priority>(nameof(ChangedItemClick))
{
    public enum Priority
    {
        /// <seealso cref="Api.PenumbraApi.ChangedItemClicked"/>
        Default = 0,

        /// <seealso cref="Penumbra.SetupApi"/>
        Link = 1,
    }
}
