using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;

namespace Penumbra.Interop.MaterialPreview;

public sealed unsafe class LiveMaterialPreviewer : LiveMaterialPreviewerBase
{
    private readonly ShaderPackage* _shaderPackage;

    private readonly uint    _originalShPkFlags;
    private readonly float[] _originalMaterialParameter;
    private readonly uint[]  _originalSamplerFlags;

    public LiveMaterialPreviewer(IObjectTable objects, MaterialInfo materialInfo)
        : base(objects, materialInfo)
    {
        var mtrlHandle = Material->MaterialResourceHandle;
        if (mtrlHandle == null)
            throw new InvalidOperationException("Material doesn't have a resource handle");

        var shpkHandle = ((Structs.MtrlResource*)mtrlHandle)->ShpkResourceHandle;
        if (shpkHandle == null)
            throw new InvalidOperationException("Material doesn't have a ShPk resource handle");

        _shaderPackage = shpkHandle->ShaderPackage;
        if (_shaderPackage == null)
            throw new InvalidOperationException("Material doesn't have a shader package");

        var material = (Structs.Material*)Material;

        _originalShPkFlags = material->ShaderPackageFlags;

        if (material->MaterialParameter->TryGetBuffer(out var materialParameter))
            _originalMaterialParameter = materialParameter.ToArray();
        else
            _originalMaterialParameter = Array.Empty<float>();

        _originalSamplerFlags = new uint[material->TextureCount];
        for (var i = 0; i < _originalSamplerFlags.Length; ++i)
            _originalSamplerFlags[i] = material->Textures[i].SamplerFlags;
    }

    protected override void Clear(bool disposing, bool reset)
    {
        base.Clear(disposing, reset);

        if (reset)
        {
            var material = (Structs.Material*)Material;

            material->ShaderPackageFlags = _originalShPkFlags;

            if (material->MaterialParameter->TryGetBuffer(out var materialParameter))
                _originalMaterialParameter.AsSpan().CopyTo(materialParameter);

            for (var i = 0; i < _originalSamplerFlags.Length; ++i)
                material->Textures[i].SamplerFlags = _originalSamplerFlags[i];
        }
    }

    public void SetShaderPackageFlags(uint shPkFlags)
    {
        if (!CheckValidity())
            return;

        ((Structs.Material*)Material)->ShaderPackageFlags = shPkFlags;
    }

    public void SetMaterialParameter(uint parameterCrc, Index offset, Span<float> value)
    {
        if (!CheckValidity())
            return;

        var constantBuffer = ((Structs.Material*)Material)->MaterialParameter;
        if (constantBuffer == null)
            return;

        if (!constantBuffer->TryGetBuffer(out var buffer))
            return;

        for (var i = 0; i < _shaderPackage->MaterialElementCount; ++i)
        {
            // TODO fix when CS updated
            ref var parameter = ref ((ShaderPackage.MaterialElement*) ((byte*)_shaderPackage + 0x98))[i];
            if (parameter.CRC == parameterCrc)
            {
                if ((parameter.Offset & 0x3) != 0
                 || (parameter.Size & 0x3) != 0
                 || (parameter.Offset + parameter.Size) >> 2 > buffer.Length)
                    return;

                value.TryCopyTo(buffer.Slice(parameter.Offset >> 2, parameter.Size >> 2)[offset..]);
                return;
            }
        }
    }

    public void SetSamplerFlags(uint samplerCrc, uint samplerFlags)
    {
        if (!CheckValidity())
            return;

        var id    = 0u;
        var found = false;

        var samplers = (Structs.ShaderPackageUtility.Sampler*)_shaderPackage->Samplers;
        for (var i = 0; i < _shaderPackage->SamplerCount; ++i)
        {
            if (samplers[i].Crc == samplerCrc)
            {
                id    = samplers[i].Id;
                found = true;
                break;
            }
        }

        if (!found)
            return;

        var material = (Structs.Material*)Material;
        for (var i = 0; i < material->TextureCount; ++i)
        {
            if (material->Textures[i].Id == id)
            {
                material->Textures[i].SamplerFlags = (samplerFlags & 0xFFFFFDFF) | 0x000001C0;
                break;
            }
        }
    }

    protected override bool IsStillValid()
    {
        if (!base.IsStillValid())
            return false;

        var mtrlHandle = Material->MaterialResourceHandle;
        if (mtrlHandle == null)
            return false;

        var shpkHandle = ((Structs.MtrlResource*)mtrlHandle)->ShpkResourceHandle;
        if (shpkHandle == null)
            return false;

        if (_shaderPackage != shpkHandle->ShaderPackage)
            return false;

        return true;
    }
}
