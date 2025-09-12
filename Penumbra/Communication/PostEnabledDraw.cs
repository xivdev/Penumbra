using Luna;
using Penumbra.Api.Api;
using Penumbra.Mods;

namespace Penumbra.Communication;

/// <summary> Triggered after the Enabled Checkbox line in settings is drawn, but before options are drawn. </summary>
public sealed class PostEnabledDraw(Logger log) : EventBase<PostEnabledDraw.Arguments, PostEnabledDraw.Priority>(nameof(PostEnabledDraw), log)
{
    public enum Priority
    {
        /// <seealso cref="PenumbraApi.PostEnabledDraw"/>
        Default = 0,
    }

    /// <summary> The arguments for a PostEnabledDraw event. </summary>
    /// <param name="Mod"> The mod currently being drawn. </param>
    public readonly record struct Arguments(Mod Mod);
}
