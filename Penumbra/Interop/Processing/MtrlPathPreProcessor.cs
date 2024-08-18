using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Interop.PathResolving;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Processing;

public sealed class MtrlPathPreProcessor : IPathPreProcessor
{
    public ResourceType Type
        => ResourceType.Mtrl;

    public FullPath? PreProcess(ResolveData resolveData, CiByteString path, Utf8GamePath originalGamePath, bool nonDefault, FullPath? resolved)
        => nonDefault ? PathDataHandler.CreateMtrl(path, resolveData.ModCollection, originalGamePath) : resolved;
}
