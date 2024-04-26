using OtterGui.Classes;
using Penumbra.Api.Api;
using Penumbra.Api.IpcSubscribers;

namespace Penumbra.Communication;

/// <summary>
/// Triggered before the settings tab bar for a mod is drawn, after the title group is drawn.
/// <list type="number">
///     <item>Parameter is the identifier (directory name) of the currently selected mod. </item>
///     <item>is the total width of the header group. </item>
///     <item>is the width of the title box. </item>
/// </list>
/// </summary>
public sealed class PreSettingsTabBarDraw() : EventWrapper<string, float, float, PreSettingsTabBarDraw.Priority>(nameof(PreSettingsTabBarDraw))
{
    public enum Priority
    {
        /// <seealso cref="Api.IpcSubscribers.PreSettingsTabBarDraw"/>
        Default = 0,
    }
}
