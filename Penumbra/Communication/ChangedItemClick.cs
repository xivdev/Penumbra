using Luna;
using Penumbra.Api.Api;
using Penumbra.Api.Enums;
using Penumbra.GameData.Data;

namespace Penumbra.Communication;

/// <summary> Triggered when a Changed Item in Penumbra is clicked. </summary>
public sealed class ChangedItemClick(Logger log)
    : EventBase<ChangedItemClick.Arguments, ChangedItemClick.Priority>(nameof(ChangedItemClick), log)
{
    public enum Priority
    {
        /// <seealso cref="UiApi.OnChangedItemClick"/>
        Default = 0,

        /// <seealso cref="Penumbra.SetupApi"/>
        Link = 1,
    }

    /// <summary> The arguments for a changed item click event. </summary>
    /// <param name="Button"> The clicked mouse button. </param>
    /// <param name="Data"> The associated data for the clicked object, if any. </param>
    public readonly record struct Arguments(MouseButton Button, IIdentifiedObjectData Data);
}
