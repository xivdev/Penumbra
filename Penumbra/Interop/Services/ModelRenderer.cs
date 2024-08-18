using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using OtterGui.Services;
using ModelRendererData = FFXIVClientStructs.FFXIV.Client.Graphics.Render.ModelRenderer;

namespace Penumbra.Interop.Services;

public unsafe class ModelRenderer : IDisposable, IRequiredService
{
    public bool Ready { get; private set; }

    public ModelRendererData* Address
        => Manager.Instance() switch
        {
            null              => null,
            var renderManager => &renderManager->ModelRenderer,
        };

    public ShaderPackageResourceHandle** IrisShaderPackage
        => Address switch
        {
            null     => null,
            var data => &data->IrisShaderPackage,
        };

    public ShaderPackageResourceHandle** CharacterGlassShaderPackage
        => Address switch
        {
            null     => null,
            var data => &data->CharacterGlassShaderPackage,
        };

    public ShaderPackageResourceHandle** CharacterTransparencyShaderPackage
        => Address switch
        {
            null     => null,
            var data => &data->CharacterTransparencyShaderPackage,
        };

    public ShaderPackageResourceHandle** CharacterTattooShaderPackage
        => Address switch
        {
            null     => null,
            var data => &data->CharacterTattooShaderPackage,
        };

    public ShaderPackageResourceHandle** CharacterOcclusionShaderPackage
        => Address switch
        {
            null     => null,
            var data => &data->CharacterOcclusionShaderPackage,
        };

    public ShaderPackageResourceHandle** HairMaskShaderPackage
        => Address switch
        {
            null     => null,
            var data => &data->HairMaskShaderPackage,
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
            anyMissing                                |= DefaultCharacterTransparencyShaderPackage == null;
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
