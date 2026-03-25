using Luna;
using Penumbra.Api.Api;
using Penumbra.GameData.Data;

namespace Penumbra.Communication;

/// <summary> Triggered when a Changed Item in Penumbra is hovered. </summary>
public sealed class ChangedItemHover(Logger log)
    : EventBase<ChangedItemHover.Arguments, ChangedItemHover.Priority>(nameof(ChangedItemHover), log)
{
    public enum Priority
    {
        /// <seealso cref="UiApi.OnChangedItemHover"/>
        Default = 0,

        /// <seealso cref="Penumbra.SetupApi"/>
        Link = 1,
    }

    /// <summary> Whether this event has any subscribers. </summary>
    public bool HasTooltip
        => HasSubscribers;

    /// <summary> The arguments for a changed item hover event. </summary>
    /// <param name="Data"> The associated data for the hovered object, if any. </param>
    public readonly record struct Arguments(IIdentifiedObjectData Data);
}
