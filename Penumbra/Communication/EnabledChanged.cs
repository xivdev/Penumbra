using Luna;

namespace Penumbra.Communication;

/// <summary> Triggered when the general Enabled state of Penumbra is changed. </summary>
public sealed class EnabledChanged(Logger log) : EventBase<EnabledChanged.Arguments, EnabledChanged.Priority>(nameof(EnabledChanged), log)
{
    public enum Priority
    {
        /// <seealso cref="Api.Api.PluginStateApi.EnabledChange"/>
        Api = int.MinValue,

        /// <seealso cref="Api.DalamudSubstitutionProvider.OnEnabledChange"/>
        DalamudSubstitutionProvider = 0,
    }

    /// <summary> The arguments for a EnabledChanged event. </summary>
    /// <param name="Enabled"> Whether Penumbra is now Enabled (true) or Disabled (false). </param>
    public readonly record struct Arguments(bool Enabled);
}
