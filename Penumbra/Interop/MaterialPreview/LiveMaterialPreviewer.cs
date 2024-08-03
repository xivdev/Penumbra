using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using Penumbra.GameData.Interop;

namespace Penumbra.Interop.MaterialPreview;

public sealed unsafe class LiveMaterialPreviewer : LiveMaterialPreviewerBase
{
    private readonly ShaderPackage* _shaderPackage;

    private readonly uint   _originalShPkFlags;
    private readonly byte[] _originalMaterialParameter;
    private readonly uint[] _originalSamplerFlags;

    public LiveMaterialPreviewer(ObjectManager objects, MaterialInfo materialInfo)
        : base(objects, materialInfo)
    {
        var mtrlHandle = Material->MaterialResourceHandle;
        if (mtrlHandle == null)
            throw new InvalidOperationException("Material doesn't have a resource handle");

        var shpkHandle = mtrlHandle->ShaderPackageResourceHandle;
        if (shpkHandle == null)
            throw new InvalidOperationException("Material doesn't have a ShPk resource handle");

        _shaderPackage = shpkHandle->ShaderPackage;
        if (_shaderPackage == null)
            throw new InvalidOperationException("Material doesn't have a shader package");

        _originalShPkFlags = Material->ShaderFlags;

        _originalMaterialParameter = Material->MaterialParameterCBuffer->TryGetBuffer<byte>().ToArray();

        _originalSamplerFlags = new uint[Material->TextureCount];
        for (var i = 0; i < _originalSamplerFlags.Length; ++i)
            _originalSamplerFlags[i] = Material->Textures[i].SamplerFlags;
    }

    protected override void Clear(bool disposing, bool reset)
    {
        base.Clear(disposing, reset);

        if (!reset)
            return;

        Material->ShaderFlags = _originalShPkFlags;
        var materialParameter = Material->MaterialParameterCBuffer->TryGetBuffer<byte>();
        if (!materialParameter.IsEmpty)
            _originalMaterialParameter.AsSpan().CopyTo(materialParameter);

        for (var i = 0; i < _originalSamplerFlags.Length; ++i)
            Material->Textures[i].SamplerFlags = _originalSamplerFlags[i];
    }

    public void SetShaderPackageFlags(uint shPkFlags)
    {
        if (!CheckValidity())
            return;

        Material->ShaderFlags = shPkFlags;
    }

    public void SetMaterialParameter(uint parameterCrc, Index offset, ReadOnlySpan<byte> value)
    {
        if (!CheckValidity())
            return;

        var constantBuffer = Material->MaterialParameterCBuffer;
        if (constantBuffer == null)
            return;

        var buffer = constantBuffer->TryGetBuffer<byte>();
        if (buffer.IsEmpty)
            return;

        for (var i = 0; i < _shaderPackage->MaterialElementCount; ++i)
        {
            ref var parameter = ref _shaderPackage->MaterialElementsSpan[i];
            if (parameter.CRC != parameterCrc)
                continue;

            if (parameter.Offset + parameter.Size > buffer.Length)
                return;

            value.TryCopyTo(buffer.Slice(parameter.Offset, parameter.Size)[offset..]);
            return;
        }
    }

    public void SetSamplerFlags(uint samplerCrc, uint samplerFlags)
    {
        if (!CheckValidity())
            return;

        var id    = 0u;
        var found = false;

        var samplers = _shaderPackage->Samplers;
        for (var i = 0; i < _shaderPackage->SamplerCount; ++i)
        {
            if (samplers[i].CRC != samplerCrc)
                continue;

            id    = samplers[i].Id;
            found = true;
            break;
        }

        if (!found)
            return;

        for (var i = 0; i < Material->TextureCount; ++i)
        {
            if (Material->Textures[i].Id != id)
                continue;

            Material->Textures[i].SamplerFlags = (samplerFlags & 0xFFFFFDFF) | 0x000001C0;
            break;
        }
    }

    protected override bool IsStillValid()
    {
        if (!base.IsStillValid())
            return false;

        var mtrlHandle = Material->MaterialResourceHandle;
        if (mtrlHandle == null)
            return false;

        var shpkHandle = mtrlHandle->ShaderPackageResourceHandle;
        if (shpkHandle == null)
            return false;

        return _shaderPackage == shpkHandle->ShaderPackage;
    }
}
