using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Interop.PathResolving;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Processing;

public sealed class TmbPathPreProcessor : IPathPreProcessor
{
    public ResourceType Type
        => ResourceType.Tmb;

    public FullPath? PreProcess(ResolveData resolveData, CiByteString path, Utf8GamePath _, bool nonDefault, FullPath? resolved)
        => nonDefault ? PathDataHandler.CreateTmb(path, resolveData.ModCollection) : resolved;
}
