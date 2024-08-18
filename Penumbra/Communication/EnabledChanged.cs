using OtterGui.Classes;
using Penumbra.Api;
using Penumbra.Api.IpcSubscribers;

namespace Penumbra.Communication;

/// <summary>
/// Triggered when the general Enabled state of Penumbra is changed.
/// <list type="number">
///     <item>Parameter is whether Penumbra is now Enabled (true) or Disabled (false). </item>
/// </list>
/// </summary>
public sealed class EnabledChanged() : EventWrapper<bool, EnabledChanged.Priority>(nameof(EnabledChanged))
{
    public enum Priority
    {
        /// <seealso cref="Api.IpcSubscribers.Ipc.EnabledChange"/>
        Api = int.MinValue,

        /// <seealso cref="Api.DalamudSubstitutionProvider.OnEnabledChange"/>
        DalamudSubstitutionProvider = 0,
    }
}
