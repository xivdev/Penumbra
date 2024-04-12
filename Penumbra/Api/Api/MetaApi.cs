using OtterGui;
using OtterGui.Services;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Api.Api;

public class MetaApi(CollectionResolver collectionResolver, ApiHelpers helpers) : IPenumbraApiMeta, IApiService
{
    public string GetPlayerMetaManipulations()
    {
        var collection = collectionResolver.PlayerCollection();
        var set        = collection.MetaCache?.Manipulations.ToArray() ?? [];
        return Functions.ToCompressedBase64(set, MetaManipulation.CurrentVersion);
    }

    public string GetMetaManipulations(int gameObjectIdx)
    {
        helpers.AssociatedCollection(gameObjectIdx, out var collection);
        var set = collection.MetaCache?.Manipulations.ToArray() ?? [];
        return Functions.ToCompressedBase64(set, MetaManipulation.CurrentVersion);
    }
}
