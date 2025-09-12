using Luna;
using Penumbra.Api.Api;
using Penumbra.Mods;

namespace Penumbra.Communication;

/// <summary> Triggered after the settings panel is drawn. </summary>
public sealed class PostSettingsPanelDraw(Logger log) : EventBase<PostSettingsPanelDraw.Arguments, PostSettingsPanelDraw.Priority>(nameof(PostSettingsPanelDraw), log)
{
    public enum Priority
    {
        /// <seealso cref="PenumbraApi.PostSettingsPanelDraw"/>
        Default = 0,
    }

    /// <summary> The arguments for a PostSettingsPanelDraw event. </summary>
    /// <param name="Mod"> The mod currently being drawn. </param>
    public readonly record struct Arguments(Mod Mod);
}
