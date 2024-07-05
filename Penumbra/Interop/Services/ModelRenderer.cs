using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using OtterGui.Services;

namespace Penumbra.Interop.Services;

public unsafe class ModelRenderer : IDisposable, IRequiredService
{
    public bool Ready { get; private set; }

    public ShaderPackageResourceHandle** IrisShaderPackage
        => Manager.Instance() switch
        {
            null              => null,
            var renderManager => &renderManager->ModelRenderer.IrisShaderPackage,
        };

    public ShaderPackageResourceHandle** CharacterGlassShaderPackage
        => Manager.Instance() switch
        {
            null              => null,
            var renderManager => &renderManager->ModelRenderer.CharacterGlassShaderPackage,
        };

    public ShaderPackageResourceHandle** CharacterTransparencyShaderPackage
        => Manager.Instance() switch
        {
            null              => null,
            var renderManager => &renderManager->ModelRenderer.CharacterTransparencyShaderPackage,
        };

    public ShaderPackageResourceHandle** CharacterTattooShaderPackage
        => Manager.Instance() switch
        {
            null              => null,
            var renderManager => &renderManager->ModelRenderer.CharacterTattooShaderPackage,
        };

    public ShaderPackageResourceHandle** CharacterOcclusionShaderPackage
        => Manager.Instance() switch
        {
            null              => null,
            var renderManager => &renderManager->ModelRenderer.CharacterOcclusionShaderPackage,
        };

    public ShaderPackageResourceHandle** HairMaskShaderPackage
        => Manager.Instance() switch
        {
            null              => null,
            var renderManager => &renderManager->ModelRenderer.HairMaskShaderPackage,
        };

    public ShaderPackageResourceHandle* DefaultIrisShaderPackage { get; private set; }

    public ShaderPackageResourceHandle* DefaultCharacterGlassShaderPackage { get; private set; }

    public ShaderPackageResourceHandle* DefaultCharacterTransparencyShaderPackage { get; private set; }

    public ShaderPackageResourceHandle* DefaultCharacterTattooShaderPackage { get; private set; }

    public ShaderPackageResourceHandle* DefaultCharacterOcclusionShaderPackage { get; private set; }

    public ShaderPackageResourceHandle* DefaultHairMaskShaderPackage { get; private set; }

    private readonly IFramework _framework;

    public ModelRenderer(IFramework framework)
    {
        _framework = framework;
        LoadDefaultResources(null!);
        if (!Ready)
            _framework.Update += LoadDefaultResources;
    }

    /// <summary> We store the default data of the resources so we can always restore them. </summary>
    private void LoadDefaultResources(object _)
    {
        if (Manager.Instance() == null)
            return;

        var anyMissing = false;

        if (DefaultIrisShaderPackage == null)
        {
            DefaultIrisShaderPackage =  *IrisShaderPackage;
            anyMissing               |= DefaultIrisShaderPackage == null;
        }

        if (DefaultCharacterGlassShaderPackage == null)
        {
            DefaultCharacterGlassShaderPackage =  *CharacterGlassShaderPackage;
            anyMissing                         |= DefaultCharacterGlassShaderPackage == null;
        }

        if (DefaultCharacterTransparencyShaderPackage == null)
        {
            DefaultCharacterTransparencyShaderPackage =  *CharacterTransparencyShaderPackage;
            anyMissing                               |= DefaultCharacterTransparencyShaderPackage == null;
        }

        if (DefaultCharacterTattooShaderPackage == null)
        {
            DefaultCharacterTattooShaderPackage =  *CharacterTattooShaderPackage;
            anyMissing                          |= DefaultCharacterTattooShaderPackage == null;
        }

        if (DefaultCharacterOcclusionShaderPackage == null)
        {
            DefaultCharacterOcclusionShaderPackage =  *CharacterOcclusionShaderPackage;
            anyMissing                             |= DefaultCharacterOcclusionShaderPackage == null;
        }

        if (DefaultHairMaskShaderPackage == null)
        {
            DefaultHairMaskShaderPackage =  *HairMaskShaderPackage;
            anyMissing                   |= DefaultHairMaskShaderPackage == null;
        }

        if (anyMissing)
            return;

        Ready             =  true;
        _framework.Update -= LoadDefaultResources;
    }

    /// <summary> Return all relevant resources to the default resource. </summary>
    public void ResetAll()
    {
        if (!Ready)
            return;

        *HairMaskShaderPackage              = DefaultHairMaskShaderPackage;
        *CharacterOcclusionShaderPackage    = DefaultCharacterOcclusionShaderPackage;
        *CharacterTattooShaderPackage       = DefaultCharacterTattooShaderPackage;
        *CharacterTransparencyShaderPackage = DefaultCharacterTransparencyShaderPackage;
        *CharacterGlassShaderPackage        = DefaultCharacterGlassShaderPackage;
        *IrisShaderPackage                  = DefaultIrisShaderPackage;
    }

    public void Dispose()
        => ResetAll();
}
