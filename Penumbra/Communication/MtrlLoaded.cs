using Luna;
using Penumbra.GameData.Interop;
using Penumbra.Interop.Hooks.PostProcessing;

namespace Penumbra.Communication;

/// <summary> Invoked whenever a material is loaded. </summary>
public sealed class MtrlLoaded(Logger log) : EventBase<MtrlLoaded.Arguments, MtrlLoaded.Priority>(nameof(MtrlLoaded), log)
{
    public enum Priority
    {
        /// <seealso cref="ShaderReplacementFixer.OnMtrlLoaded"/>
        ShaderReplacementFixer = 0,
    }

    /// <summary> The arguments for a MtrlLoaded event. </summary>
    /// <param name="MaterialResourceHandle"> The material resource handle for which the shader package has been loaded. </param>
    /// <param name="GameObject"> The associated game object </param>
    public readonly record struct Arguments(nint MaterialResourceHandle, Actor GameObject);
}
