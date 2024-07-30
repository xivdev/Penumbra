using Penumbra.Api.Enums;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Structs;
using Penumbra.String;

namespace Penumbra.Interop.Processing;

public sealed class MaterialFilePostProcessor //: IFilePostProcessor
{
    public ResourceType Type
        => ResourceType.Mtrl;

    public unsafe void PostProcess(ResourceHandle* resource, CiByteString originalGamePath, ReadOnlySpan<byte> additionalData)
    {
        if (!PathDataHandler.ReadMtrl(additionalData, out var data))
            return;
    }
}
