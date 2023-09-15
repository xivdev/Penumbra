using OtterGui.Classes;

namespace Penumbra.Communication;

/// <summary> <list type="number">
///     <item>Parameter is the material resource handle for which the shader package has been loaded. </item>
///     <item>Parameter is the associated game object. </item>
/// </list> </summary>
public sealed class MtrlShpkLoaded : EventWrapper<Action<nint, nint>, MtrlShpkLoaded.Priority>
{
    public enum Priority
    {
        /// <seealso cref="Interop.Services.SkinFixer.OnMtrlShpkLoaded"/>
        SkinFixer = 0,
    }

    public MtrlShpkLoaded()
        : base(nameof(MtrlShpkLoaded))
    { }

    public void Invoke(nint mtrlResourceHandle, nint gameObject)
        => Invoke(this, mtrlResourceHandle, gameObject);
}
