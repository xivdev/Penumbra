using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.Interop.PathResolving;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Processing;

public sealed class ImcPathPreProcessor : IPathPreProcessor
{
    public ResourceType Type
        => ResourceType.Imc;

    public FullPath? PreProcess(ResolveData resolveData, CiByteString path, Utf8GamePath originalGamePath, bool _, FullPath? resolved)
        => resolveData.ModCollection.MetaCache?.Imc.HasFile(originalGamePath.Path) ?? false
            ? PathDataHandler.CreateImc(path, resolveData.ModCollection)
            : resolved;
}
