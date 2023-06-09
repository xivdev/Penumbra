using System;
using OtterGui.Classes;

namespace Penumbra.Communication;

/// <summary>
/// Triggered when a Changed Item in Penumbra is hovered.
/// <list type="number">
///     <item>Parameter is the hovered object data if any. </item>
/// </list>
/// </summary>
public sealed class ChangedItemHover : EventWrapper<Action<object?>, ChangedItemHover.Priority>
{
    public enum Priority
    {
        /// <seealso cref="Api.PenumbraApi.ChangedItemTooltip"/>
        Default = 0,

        /// <seealso cref="Penumbra.SetupApi"/>
        Link = 1,
    }

    public ChangedItemHover()
        : base(nameof(ChangedItemHover))
    { }

    public void Invoke(object? data)
        => Invoke(this, data);

    public bool HasTooltip
        => HasSubscribers;
}
