using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

namespace Penumbra.Interop.Services;

public unsafe class ModelRenderer : IDisposable
{
    public bool Ready { get; private set; }

    public ShaderPackageResourceHandle** CharacterGlassShaderPackage
        => Manager.Instance() switch
        {
            null              => null,
            var renderManager => &renderManager->ModelRenderer.CharacterGlassShaderPackage,
        };

    public ShaderPackageResourceHandle* DefaultCharacterGlassShaderPackage { get; private set; }

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
