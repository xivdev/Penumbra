using OtterGui.Classes;

namespace Penumbra.Communication;

/// <summary> <list type="number">
///     <item>Parameter is the material resource handle for which the shader package has been loaded. </item>
///     <item>Parameter is the associated game object. </item>
/// </list> </summary>
public sealed class MtrlShpkLoaded() : EventWrapper<nint, nint, MtrlShpkLoaded.Priority>(nameof(MtrlShpkLoaded))
{
    public enum Priority
    {
        /// <seealso cref="Interop.Services.ShaderReplacementFixer.OnMtrlShpkLoaded"/>
        ShaderReplacementFixer = 0,
    }
}
