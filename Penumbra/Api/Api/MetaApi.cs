using Newtonsoft.Json.Linq;
using OtterGui;
using OtterGui.Services;
using Penumbra.Collections;
using Penumbra.GameData.Structs;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Api.Api;

public class MetaApi(CollectionResolver collectionResolver, ApiHelpers helpers) : IPenumbraApiMeta, IApiService
{
    public const int CurrentVersion = 0;

    public string GetPlayerMetaManipulations()
    {
        var collection = collectionResolver.PlayerCollection();
        return CompressMetaManipulations(collection);
    }

    public string GetMetaManipulations(int gameObjectIdx)
    {
        helpers.AssociatedCollection(gameObjectIdx, out var collection);
        return CompressMetaManipulations(collection);
    }

    internal static string CompressMetaManipulations(ModCollection collection)
    {
        var array = new JArray();
        if (collection.MetaCache is { } cache)
        {
            MetaDictionary.SerializeTo(array, cache.GlobalEqp.Select(kvp => kvp.Key));
            MetaDictionary.SerializeTo(array, cache.Imc.Select(kvp => new KeyValuePair<ImcIdentifier, ImcEntry>(kvp.Key, kvp.Value.Entry)));
            MetaDictionary.SerializeTo(array, cache.Eqp.Select(kvp => new KeyValuePair<EqpIdentifier, EqpEntry>(kvp.Key, kvp.Value.Entry)));
            MetaDictionary.SerializeTo(array, cache.Eqdp.Select(kvp => new KeyValuePair<EqdpIdentifier, EqdpEntry>(kvp.Key, kvp.Value.Entry)));
            MetaDictionary.SerializeTo(array, cache.Est.Select(kvp => new KeyValuePair<EstIdentifier, EstEntry>(kvp.Key, kvp.Value.Entry)));
            MetaDictionary.SerializeTo(array, cache.Rsp.Select(kvp => new KeyValuePair<RspIdentifier, RspEntry>(kvp.Key, kvp.Value.Entry)));
            MetaDictionary.SerializeTo(array, cache.Gmp.Select(kvp => new KeyValuePair<GmpIdentifier, GmpEntry>(kvp.Key, kvp.Value.Entry)));
        }

        return Functions.ToCompressedBase64(array, CurrentVersion);
    }
}
