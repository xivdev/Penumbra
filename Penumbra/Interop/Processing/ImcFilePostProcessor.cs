using Penumbra.Api.Enums;
using Penumbra.Collections.Manager;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Structs;
using Penumbra.String;

namespace Penumbra.Interop.Processing;

public sealed class ImcFilePostProcessor(CollectionStorage collections) : IFilePostProcessor
{
    public ResourceType Type
        => ResourceType.Imc;

    public unsafe void PostProcess(ResourceHandle* resource, CiByteString originalGamePath, ReadOnlySpan<byte> additionalData)
    {
        if (!PathDataHandler.Read(additionalData, out var data) || data.Discriminator != PathDataHandler.Discriminator)
            return;

        var collection = collections.ByLocalId(data.Collection);
        if (collection.MetaCache is not { } cache)
            return;

        if (!cache.Imc.GetFile(originalGamePath, out var file))
            return;

        file.Replace(resource);
        Penumbra.Log.Verbose(
            $"[ResourceLoader] Loaded {originalGamePath} from file and replaced with IMC from collection {collection.Identity.AnonymizedName}.");
    }
}
