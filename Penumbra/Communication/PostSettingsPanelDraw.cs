using OtterGui.Classes;

namespace Penumbra.Communication;

/// <summary>
/// Triggered after the settings panel is drawn.
/// <list type="number">
///     <item>Parameter is the identifier (directory name) of the currently selected mod. </item>
/// </list>
/// </summary>
public sealed class PostSettingsPanelDraw : EventWrapper<Action<string>, PostSettingsPanelDraw.Priority>
{
    public enum Priority
    {
        /// <seealso cref="Api.PenumbraApi.PostSettingsPanelDraw"/>
        Default = 0,
    }

    public PostSettingsPanelDraw()
        : base(nameof(PostSettingsPanelDraw))
    { }

    public void Invoke(string modDirectory)
        => Invoke(this, modDirectory);
}
