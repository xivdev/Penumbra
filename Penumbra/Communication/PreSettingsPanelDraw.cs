using Luna;
using Penumbra.Api.Api;
using Penumbra.Mods;

namespace Penumbra.Communication;

/// <summary> Triggered before the settings panel is drawn. </summary>
public sealed class PreSettingsPanelDraw(Logger log) : EventBase<PreSettingsPanelDraw.Arguments, PreSettingsPanelDraw.Priority>(nameof(PreSettingsPanelDraw), log)
{
    public enum Priority
    {
        /// <seealso cref="PenumbraApi.PreSettingsPanelDraw"/>
        Default = 0,
    }

    /// <summary> The arguments for a PreSettingsPanelDraw event. </summary>
    /// <param name="Mod"> The mod currently being drawn. </param>
    public readonly record struct Arguments(Mod Mod);
}
