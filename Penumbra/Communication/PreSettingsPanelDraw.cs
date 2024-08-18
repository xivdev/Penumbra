using OtterGui.Classes;
using Penumbra.Api.Api;

namespace Penumbra.Communication;

/// <summary>
/// Triggered before the settings panel is drawn.
/// <list type="number">
///     <item>Parameter is the identifier (directory name) of the currently selected mod. </item>
/// </list>
/// </summary>
public sealed class PreSettingsPanelDraw() : EventWrapper<string, PreSettingsPanelDraw.Priority>(nameof(PreSettingsPanelDraw))
{
    public enum Priority
    {
        /// <seealso cref="PenumbraApi.PreSettingsPanelDraw"/>
        Default = 0,
    }
}
