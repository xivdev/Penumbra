using OtterGui.Classes;
using Penumbra.Api.Api;
using Penumbra.GameData.Data;

namespace Penumbra.Communication;

/// <summary>
/// Triggered when a Changed Item in Penumbra is hovered.
/// <list type="number">
///     <item>Parameter is the hovered object data if any. </item>
/// </list>
/// </summary>
public sealed class ChangedItemHover() : EventWrapper<IIdentifiedObjectData?, ChangedItemHover.Priority>(nameof(ChangedItemHover))
{
    public enum Priority
    {
        /// <seealso cref="UiApi.OnChangedItemHover"/>
        Default = 0,

        /// <seealso cref="Penumbra.SetupApi"/>
        Link = 1,
    }

    public bool HasTooltip
        => HasSubscribers;
}
