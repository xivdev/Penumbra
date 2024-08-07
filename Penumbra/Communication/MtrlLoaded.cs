using OtterGui.Classes;

namespace Penumbra.Communication;

/// <summary> <list type="number">
///     <item>Parameter is the material resource handle for which the shader package has been loaded. </item>
///     <item>Parameter is the associated game object. </item>
/// </list> </summary>
public sealed class MtrlLoaded() : EventWrapper<nint, nint, MtrlLoaded.Priority>(nameof(MtrlLoaded))
{
    public enum Priority
    {
        /// <seealso cref="Interop.Hooks.PostProcessing.ShaderReplacementFixer.OnMtrlLoaded"/>
        ShaderReplacementFixer = 0,
    }
}
