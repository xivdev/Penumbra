using OtterGui.Classes;

namespace Penumbra.Communication;

/// <summary>
/// Triggered before the settings panel is drawn.
/// <list type="number">
///     <item>Parameter is the identifier (directory name) of the currently selected mod. </item>
/// </list>
/// </summary>
public sealed class PreSettingsPanelDraw : EventWrapper<Action<string>, PreSettingsPanelDraw.Priority>
{
    public enum Priority
    {
        /// <seealso cref="Api.PenumbraApi.PreSettingsPanelDraw"/>
        Default = 0,
    }

    public PreSettingsPanelDraw()
        : base(nameof(PreSettingsPanelDraw))
    { }

    public void Invoke(string modDirectory)
        => Invoke(this, modDirectory);
}
