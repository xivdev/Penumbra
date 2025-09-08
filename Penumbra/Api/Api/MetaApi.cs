using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterGui;
using Penumbra.Collections;
using Penumbra.Collections.Cache;
using Penumbra.GameData.Files.AtchStructs;
using Penumbra.GameData.Files.Utility;
using Penumbra.GameData.Structs;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta.Manipulations;

namespace Penumbra.Api.Api;

public class MetaApi(IFramework framework, CollectionResolver collectionResolver, ApiHelpers helpers)
    : IPenumbraApiMeta, Luna.IApiService
{
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

    public Task<string> GetPlayerMetaManipulationsAsync()
    {
        return Task.Run(async () =>
        {
            var playerCollection = await framework.RunOnFrameworkThread(collectionResolver.PlayerCollection).ConfigureAwait(false);
            return CompressMetaManipulations(playerCollection);
        });
    }

    public Task<string> GetMetaManipulationsAsync(int gameObjectIdx)
    {
        return Task.Run(async () =>
        {
            var playerCollection = await framework.RunOnFrameworkThread(() =>
            {
                helpers.AssociatedCollection(gameObjectIdx, out var collection);
                return collection;
            }).ConfigureAwait(false);
            return CompressMetaManipulations(playerCollection);
        });
    }

    internal static string CompressMetaManipulations(ModCollection collection)
        => CompressMetaManipulationsV1(collection);

    private static string CompressMetaManipulationsV0(ModCollection collection)
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
            MetaDictionary.SerializeTo(array, cache.Atch.Select(kvp => new KeyValuePair<AtchIdentifier, AtchEntry>(kvp.Key, kvp.Value.Entry)));
            MetaDictionary.SerializeTo(array, cache.Shp.Select(kvp => new KeyValuePair<ShpIdentifier, ShpEntry>(kvp.Key, kvp.Value.Entry)));
            MetaDictionary.SerializeTo(array, cache.Atr.Select(kvp => new KeyValuePair<AtrIdentifier, AtrEntry>(kvp.Key, kvp.Value.Entry)));
        }

        return Functions.ToCompressedBase64(array, 0);
    }

    private static unsafe string CompressMetaManipulationsV1(ModCollection? collection)
    {
        using var ms = new MemoryStream();
        ms.Capacity = 1024;
        using (var zipStream = new GZipStream(ms, CompressionMode.Compress, true))
        {
            zipStream.Write((byte)1);
            zipStream.Write("META0001"u8);
            if (collection?.MetaCache is not { } cache)
            {
                zipStream.Write(0);
                zipStream.Write(0);
                zipStream.Write(0);
                zipStream.Write(0);
                zipStream.Write(0);
                zipStream.Write(0);
                zipStream.Write(0);
            }
            else
            {
                WriteCache(zipStream, cache.Imc);
                WriteCache(zipStream, cache.Eqp);
                WriteCache(zipStream, cache.Eqdp);
                WriteCache(zipStream, cache.Est);
                WriteCache(zipStream, cache.Rsp);
                WriteCache(zipStream, cache.Gmp);
                cache.GlobalEqp.EnterReadLock();

                try
                {
                    zipStream.Write(cache.GlobalEqp.Count);
                    foreach (var (globalEqp, _) in cache.GlobalEqp)
                        zipStream.Write(new ReadOnlySpan<byte>(&globalEqp, sizeof(GlobalEqpManipulation)));
                }
                finally
                {
                    cache.GlobalEqp.ExitReadLock();
                }

                WriteCache(zipStream, cache.Atch);
                WriteCache(zipStream, cache.Shp);
                WriteCache(zipStream, cache.Atr);
            }
        }

        ms.Flush();
        ms.Position = 0;
        var data = ms.GetBuffer().AsSpan(0, (int)ms.Length);
        return Convert.ToBase64String(data);

        void WriteCache<TKey, TValue>(Stream stream, MetaCacheBase<TKey, TValue> metaCache)
            where TKey : unmanaged, IMetaIdentifier
            where TValue : unmanaged
        {
            metaCache.EnterReadLock();
            try
            {
                stream.Write(metaCache.Count);
                foreach (var (identifier, (_, value)) in metaCache)
                {
                    stream.Write(identifier);
                    stream.Write(value);
                }
            }
            finally
            {
                metaCache.ExitReadLock();
            }
        }
    }

    public const uint ImcKey  = ((uint)'I' << 24) | ((uint)'M' << 16) | ((uint)'C' << 8);
    public const uint EqpKey  = ((uint)'E' << 24) | ((uint)'Q' << 16) | ((uint)'P' << 8);
    public const uint EqdpKey = ((uint)'E' << 24) | ((uint)'Q' << 16) | ((uint)'D' << 8) | 'P';
    public const uint EstKey  = ((uint)'E' << 24) | ((uint)'S' << 16) | ((uint)'T' << 8);
    public const uint RspKey  = ((uint)'R' << 24) | ((uint)'S' << 16) | ((uint)'P' << 8);
    public const uint GmpKey  = ((uint)'G' << 24) | ((uint)'M' << 16) | ((uint)'P' << 8);
    public const uint GeqpKey = ((uint)'G' << 24) | ((uint)'E' << 16) | ((uint)'Q' << 8) | 'P';
    public const uint AtchKey = ((uint)'A' << 24) | ((uint)'T' << 16) | ((uint)'C' << 8) | 'H';
    public const uint ShpKey  = ((uint)'S' << 24) | ((uint)'H' << 16) | ((uint)'P' << 8);
    public const uint AtrKey  = ((uint)'A' << 24) | ((uint)'T' << 16) | ((uint)'R' << 8);

    private static unsafe string CompressMetaManipulationsV2(ModCollection? collection)
    {
        using var ms = new MemoryStream();
        ms.Capacity = 1024;
        using (var zipStream = new GZipStream(ms, CompressionMode.Compress, true))
        {
            zipStream.Write((byte)2);
            zipStream.Write("META0002"u8);
            if (collection?.MetaCache is { } cache)
            {
                WriteCache(zipStream, cache.Imc,  ImcKey);
                WriteCache(zipStream, cache.Eqp,  EqpKey);
                WriteCache(zipStream, cache.Eqdp, EqdpKey);
                WriteCache(zipStream, cache.Est,  EstKey);
                WriteCache(zipStream, cache.Rsp,  RspKey);
                WriteCache(zipStream, cache.Gmp,  GmpKey);
                cache.GlobalEqp.EnterReadLock();

                try
                {
                    if (cache.GlobalEqp.Count > 0)
                    {
                        zipStream.Write(GeqpKey);
                        zipStream.Write(cache.GlobalEqp.Count);
                        foreach (var (globalEqp, _) in cache.GlobalEqp)
                            zipStream.Write(new ReadOnlySpan<byte>(&globalEqp, sizeof(GlobalEqpManipulation)));
                    }
                }
                finally
                {
                    cache.GlobalEqp.ExitReadLock();
                }

                WriteCache(zipStream, cache.Atch, AtchKey);
                WriteCache(zipStream, cache.Shp,  ShpKey);
                WriteCache(zipStream, cache.Atr,  AtrKey);
            }
        }

        ms.Flush();
        ms.Position = 0;
        var data = ms.GetBuffer().AsSpan(0, (int)ms.Length);
        return Convert.ToBase64String(data);

        void WriteCache<TKey, TValue>(Stream stream, MetaCacheBase<TKey, TValue> metaCache, uint label)
            where TKey : unmanaged, IMetaIdentifier
            where TValue : unmanaged
        {
            metaCache.EnterReadLock();
            try
            {
                if (metaCache.Count <= 0)
                    return;

                stream.Write(label);
                stream.Write(metaCache.Count);
                foreach (var (identifier, (_, value)) in metaCache)
                {
                    stream.Write(identifier);
                    stream.Write(value);
                }
            }
            finally
            {
                metaCache.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Convert manipulations from a transmitted base64 string to actual manipulations.
    /// The empty string is treated as an empty set.
    /// Only returns true if all conversions are successful and distinct. 
    /// </summary>
    internal static bool ConvertManips(string manipString, [NotNullWhen(true)] out MetaDictionary? manips, out byte version)
    {
        if (manipString.Length == 0)
        {
            manips  = new MetaDictionary();
            version = byte.MaxValue;
            return true;
        }

        try
        {
            var       bytes            = Convert.FromBase64String(manipString);
            using var compressedStream = new MemoryStream(bytes);
            using var zipStream        = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var resultStream     = new MemoryStream();
            zipStream.CopyTo(resultStream);
            resultStream.Flush();
            resultStream.Position = 0;
            var data = resultStream.GetBuffer().AsSpan(0, (int)resultStream.Length);
            version = data[0];
            data    = data[1..];
            switch (version)
            {
                case 0: return ConvertManipsV0(data, out manips);
                case 1: return ConvertManipsV1(data, out manips);
                case 2: return ConvertManipsV2(data, out manips);
                default:
                    Penumbra.Log.Debug($"Invalid version for manipulations: {version}.");
                    manips = null;
                    return false;
            }
        }
        catch (Exception ex)
        {
            Penumbra.Log.Debug($"Error decompressing manipulations:\n{ex}");
            manips  = null;
            version = byte.MaxValue;
            return false;
        }
    }

    private static bool ConvertManipsV2(ReadOnlySpan<byte> data, [NotNullWhen(true)] out MetaDictionary? manips)
    {
        if (!data.StartsWith("META0002"u8))
        {
            Penumbra.Log.Debug("Invalid manipulations of version 2, does not start with valid prefix.");
            manips = null;
            return false;
        }

        manips = new MetaDictionary();
        var r = new SpanBinaryReader(data[8..]);
        while (r.Remaining > 4)
        {
            var prefix = r.ReadUInt32();
            var count  = r.Remaining > 4 ? r.ReadInt32() : 0;
            if (count is 0)
                continue;

            switch (prefix)
            {
                case ImcKey:
                    for (var i = 0; i < count; ++i)
                    {
                        var identifier = r.Read<ImcIdentifier>();
                        var value      = r.Read<ImcEntry>();
                        if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                            return false;
                    }

                    break;
                case EqpKey:
                    for (var i = 0; i < count; ++i)
                    {
                        var identifier = r.Read<EqpIdentifier>();
                        var value      = r.Read<EqpEntry>();
                        if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                            return false;
                    }

                    break;
                case EqdpKey:
                    for (var i = 0; i < count; ++i)
                    {
                        var identifier = r.Read<EqdpIdentifier>();
                        var value      = r.Read<EqdpEntry>();
                        if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                            return false;
                    }

                    break;
                case EstKey:
                    for (var i = 0; i < count; ++i)
                    {
                        var identifier = r.Read<EstIdentifier>();
                        var value      = r.Read<EstEntry>();
                        if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                            return false;
                    }

                    break;
                case RspKey:
                    for (var i = 0; i < count; ++i)
                    {
                        var identifier = r.Read<RspIdentifier>();
                        var value      = r.Read<RspEntry>();
                        if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                            return false;
                    }

                    break;
                case GmpKey:
                    for (var i = 0; i < count; ++i)
                    {
                        var identifier = r.Read<GmpIdentifier>();
                        var value      = r.Read<GmpEntry>();
                        if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                            return false;
                    }

                    break;
                case GeqpKey:
                    for (var i = 0; i < count; ++i)
                    {
                        var identifier = r.Read<GlobalEqpManipulation>();
                        if (!identifier.Validate() || !manips.TryAdd(identifier))
                            return false;
                    }

                    break;
                case AtchKey:
                    for (var i = 0; i < count; ++i)
                    {
                        var identifier = r.Read<AtchIdentifier>();
                        var value      = r.Read<AtchEntry>();
                        if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                            return false;
                    }

                    break;
                case ShpKey:
                    for (var i = 0; i < count; ++i)
                    {
                        var identifier = r.Read<ShpIdentifier>();
                        var value      = r.Read<ShpEntry>();
                        if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                            return false;
                    }

                    break;
                case AtrKey:
                    for (var i = 0; i < count; ++i)
                    {
                        var identifier = r.Read<AtrIdentifier>();
                        var value      = r.Read<AtrEntry>();
                        if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                            return false;
                    }

                    break;
            }
        }

        return true;
    }

    private static bool ConvertManipsV1(ReadOnlySpan<byte> data, [NotNullWhen(true)] out MetaDictionary? manips)
    {
        if (!data.StartsWith("META0001"u8))
        {
            Penumbra.Log.Debug($"Invalid manipulations of version 1, does not start with valid prefix.");
            manips = null;
            return false;
        }

        manips = new MetaDictionary();
        var r        = new SpanBinaryReader(data[8..]);
        var imcCount = r.ReadInt32();
        for (var i = 0; i < imcCount; ++i)
        {
            var identifier = r.Read<ImcIdentifier>();
            var value      = r.Read<ImcEntry>();
            if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                return false;
        }

        var eqpCount = r.ReadInt32();
        for (var i = 0; i < eqpCount; ++i)
        {
            var identifier = r.Read<EqpIdentifier>();
            var value      = r.Read<EqpEntry>();
            if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                return false;
        }

        var eqdpCount = r.ReadInt32();
        for (var i = 0; i < eqdpCount; ++i)
        {
            var identifier = r.Read<EqdpIdentifier>();
            var value      = r.Read<EqdpEntry>();
            if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                return false;
        }

        var estCount = r.ReadInt32();
        for (var i = 0; i < estCount; ++i)
        {
            var identifier = r.Read<EstIdentifier>();
            var value      = r.Read<EstEntry>();
            if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                return false;
        }

        var rspCount = r.ReadInt32();
        for (var i = 0; i < rspCount; ++i)
        {
            var identifier = r.Read<RspIdentifier>();
            var value      = r.Read<RspEntry>();
            if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                return false;
        }

        var gmpCount = r.ReadInt32();
        for (var i = 0; i < gmpCount; ++i)
        {
            var identifier = r.Read<GmpIdentifier>();
            var value      = r.Read<GmpEntry>();
            if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                return false;
        }

        var globalEqpCount = r.ReadInt32();
        for (var i = 0; i < globalEqpCount; ++i)
        {
            var manip = r.Read<GlobalEqpManipulation>();
            if (!manip.Validate() || !manips.TryAdd(manip))
                return false;
        }

        // Atch was added after there were already some V1 around, so check for size here.
        if (r.Position < r.Count)
        {
            var atchCount = r.ReadInt32();
            for (var i = 0; i < atchCount; ++i)
            {
                var identifier = r.Read<AtchIdentifier>();
                var value      = r.Read<AtchEntry>();
                if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                    return false;
            }

            // Shp and Atr was added later
            if (r.Position < r.Count)
            {
                var shpCount = r.ReadInt32();
                for (var i = 0; i < shpCount; ++i)
                {
                    var identifier = r.Read<ShpIdentifier>();
                    var value      = r.Read<ShpEntry>();
                    if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                        return false;
                }

                var atrCount = r.ReadInt32();
                for (var i = 0; i < atrCount; ++i)
                {
                    var identifier = r.Read<AtrIdentifier>();
                    var value      = r.Read<AtrEntry>();
                    if (!identifier.Validate() || !manips.TryAdd(identifier, value))
                        return false;
                }
            }
        }

        return true;
    }

    private static bool ConvertManipsV0(ReadOnlySpan<byte> data, [NotNullWhen(true)] out MetaDictionary? manips)
    {
        var json = Encoding.UTF8.GetString(data);
        manips = JsonConvert.DeserializeObject<MetaDictionary>(json);
        return manips != null;
    }

    internal void TestMetaManipulations()
    {
        var collection = collectionResolver.PlayerCollection();
        var dict       = new MetaDictionary(collection.MetaCache);
        var count      = dict.Count;

        var watch  = Stopwatch.StartNew();
        var v0     = CompressMetaManipulationsV0(collection);
        var v0Time = watch.ElapsedMilliseconds;

        watch.Restart();
        var v1     = CompressMetaManipulationsV1(collection);
        var v1Time = watch.ElapsedMilliseconds;

        watch.Restart();
        var v1Success       = ConvertManips(v1, out var v1Roundtrip, out _);
        var v1RoundtripTime = watch.ElapsedMilliseconds;

        watch.Restart();
        var v0Success       = ConvertManips(v0, out var v0Roundtrip, out _);
        var v0RoundtripTime = watch.ElapsedMilliseconds;

        Penumbra.Log.Information($"Version | Count | Time | Length | Success | ReCount | ReTime | Equal");
        Penumbra.Log.Information(
            $"0       | {count} | {v0Time} | {v0.Length} | {v0Success} | {v0Roundtrip?.Count} | {v0RoundtripTime} | {v0Roundtrip?.Equals(dict)}");
        Penumbra.Log.Information(
            $"1       | {count} | {v1Time} | {v1.Length} | {v1Success} | {v1Roundtrip?.Count} | {v1RoundtripTime} | {v0Roundtrip?.Equals(dict)}");
    }
}
