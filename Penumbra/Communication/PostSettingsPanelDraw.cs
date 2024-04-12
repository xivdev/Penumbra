using OtterGui.Classes;
using Penumbra.Api.Api;

namespace Penumbra.Communication;

/// <summary>
/// Triggered after the settings panel is drawn.
/// <list type="number">
///     <item>Parameter is the identifier (directory name) of the currently selected mod. </item>
/// </list>
/// </summary>
public sealed class PostSettingsPanelDraw() : EventWrapper<string, PostSettingsPanelDraw.Priority>(nameof(PostSettingsPanelDraw))
{
    public enum Priority
    {
        /// <seealso cref="PenumbraApi.PostSettingsPanelDraw"/>
        Default = 0,
    }
}
