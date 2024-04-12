using OtterGui.Classes;
using Penumbra.Api.Api;

namespace Penumbra.Communication;

/// <summary>
/// Triggered after the Enabled Checkbox line in settings is drawn, but before options are drawn.
/// <list type="number">
///     <item>Parameter is the identifier (directory name) of the currently selected mod. </item>
/// </list>
/// </summary>
public sealed class PostEnabledDraw() : EventWrapper<string, PostEnabledDraw.Priority>(nameof(PostEnabledDraw))
{
    public enum Priority
    {
        /// <seealso cref="PenumbraApi.PostEnabledDraw"/>
        Default = 0,
    }
}
