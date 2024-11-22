using Penumbra.Api.Enums;
using Penumbra.Collections.Manager;
using Penumbra.GameData.Enums;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.String;

namespace Penumbra.Interop.Processing;

public sealed class AtchFilePostProcessor(CollectionStorage collections, XivFileAllocator allocator)
    : IFilePostProcessor
{
    private readonly IFileAllocator _allocator = allocator;

    public ResourceType Type
        => ResourceType.Atch;

    public unsafe void PostProcess(ResourceHandle* resource, CiByteString originalGamePath, ReadOnlySpan<byte> additionalData)
    {
        if (!PathDataHandler.Read(additionalData, out var data) || data.Discriminator != PathDataHandler.Discriminator)
            return;

        var collection = collections.ByLocalId(data.Collection);
        if (collection.MetaCache is not { } cache)
            return;

        if (!AtchPathPreProcessor.TryGetAtchGenderRace(originalGamePath, out var gr))
            return;

        if (!collection.MetaCache.Atch.GetFile(gr, out var file))
            return;

        using var bytes  = file.Write();
        var       length = (int)bytes.Position;
        var       alloc  = _allocator.Allocate(length, 1);
        bytes.GetBuffer().AsSpan(0, length).CopyTo(new Span<byte>(alloc, length));
        var (oldData, oldLength) = resource->GetData();
        _allocator.Release((void*)oldData, oldLength);
        resource->SetData((nint)alloc, length);
        Penumbra.Log.Information($"Post-Processed {originalGamePath} on resource 0x{(nint)resource:X} with {collection} for {gr.ToName()}.");
    }
}
