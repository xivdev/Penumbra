using Luna;
using Penumbra.Mods;

namespace Penumbra.Communication;

/// <summary> Triggered before the settings tab bar for a mod is drawn, after the title group is drawn. </summary>
public sealed class PreSettingsTabBarDraw(Logger log)
    : EventBase<PreSettingsTabBarDraw.Arguments, PreSettingsTabBarDraw.Priority>(nameof(PreSettingsTabBarDraw), log)
{
    public enum Priority
    {
        /// <seealso cref="Api.IpcSubscribers.PreSettingsTabBarDraw"/>
        Default = 0,
    }

    /// <summary> The arguments for a PreSettingsTabBarDraw event. </summary>
    /// <param name="Mod"> The mod currently being drawn. </param>
    /// <param name="HeaderWidth"> The total width of the header group. </param>
    /// <param name="TitleBoxWidth"> The width of the title box. </param>
    public readonly record struct Arguments(Mod Mod, float HeaderWidth, float TitleBoxWidth);
}
