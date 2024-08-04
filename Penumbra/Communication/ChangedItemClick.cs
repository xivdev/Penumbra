using OtterGui.Classes;
using Penumbra.Api.Api;
using Penumbra.Api.Enums;
using Penumbra.GameData.Data;

namespace Penumbra.Communication;

/// <summary>
/// Triggered when a Changed Item in Penumbra is clicked.
/// <list type="number">
///     <item>Parameter is the clicked mouse button. </item>
///     <item>Parameter is the clicked object data if any. </item>
/// </list>
/// </summary>
public sealed class ChangedItemClick() : EventWrapper<MouseButton, IIdentifiedObjectData?, ChangedItemClick.Priority>(nameof(ChangedItemClick))
{
    public enum Priority
    {
        /// <seealso cref="UiApi.OnChangedItemClick"/>
        Default = 0,

        /// <seealso cref="Penumbra.SetupApi"/>
        Link = 1,
    }
}
