using OtterGui.Classes;
using Penumbra.Api;

namespace Penumbra.Communication;

/// <summary>
/// Triggered when the general Enabled state of Penumbra is changed.
/// <list type="number">
///     <item>Parameter is whether Penumbra is now Enabled (true) or Disabled (false). </item>
/// </list>
/// </summary>
public sealed class EnabledChanged : EventWrapper<Action<bool>, EnabledChanged.Priority>
{
    public enum Priority
    {
        /// <seealso cref="Ipc.EnabledChange"/>
        Api = int.MinValue,

        /// <seealso cref="Api.DalamudSubstitutionProvider.OnEnabledChange"/>
        DalamudSubstitutionProvider = 0,
    }

    public EnabledChanged()
        : base(nameof(EnabledChanged))
    { }

    public void Invoke(bool enabled)
        => Invoke(this, enabled);
}
