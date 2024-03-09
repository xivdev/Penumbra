using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using Penumbra.GameData;

namespace Penumbra.Interop.Services;

// TODO ClientStructs-ify (https://github.com/aers/FFXIVClientStructs/pull/817)
public unsafe class ModelRenderer : IDisposable
{
    // Will be Manager.Instance()->ModelRenderer.CharacterGlassShaderPackage in CS
    private const nint ModelRendererOffset               = 0x13660;
    private const nint CharacterGlassShaderPackageOffset = 0xD0;

    /// <summary> A static pointer to the Render::Manager address. </summary>
    [Signature(Sigs.RenderManager, ScanType = ScanType.StaticAddress)]
    private readonly nint* _renderManagerAddress = null;

    public bool Ready { get; private set; }

    public ShaderPackageResourceHandle** CharacterGlassShaderPackage
        => *_renderManagerAddress == 0
            ? null
            : (ShaderPackageResourceHandle**)(*_renderManagerAddress + ModelRendererOffset + CharacterGlassShaderPackageOffset).ToPointer();

    public ShaderPackageResourceHandle* DefaultCharacterGlassShaderPackage { get; private set; }

    private readonly IFramework _framework;

    public ModelRenderer(IFramework framework, IGameInteropProvider interop)
    {
        interop.InitializeFromAttributes(this);
        _framework = framework;
        LoadDefaultResources(null!);
        if (!Ready)
            _framework.Update += LoadDefaultResources;
    }

    /// <summary> We store the default data of the resources so we can always restore them. </summary>
    private void LoadDefaultResources(object _)
    {
        if (*_renderManagerAddress == 0)
            return;

        var anyMissing = false;

        if (DefaultCharacterGlassShaderPackage == null)
        {
            DefaultCharacterGlassShaderPackage = *CharacterGlassShaderPackage;
            anyMissing |= DefaultCharacterGlassShaderPackage == null;
        }

        if (anyMissing)
            return;

        Ready = true;
        _framework.Update -= LoadDefaultResources;
    }

    /// <summary> Return all relevant resources to the default resource. </summary>
    public void ResetAll()
    {
        if (!Ready)
            return;

        *CharacterGlassShaderPackage = DefaultCharacterGlassShaderPackage;
    }

    public void Dispose()
        => ResetAll();
}
