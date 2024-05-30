using Penumbra.Collections;
using Penumbra.Collections.Manager;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.PathResolving;

public static class PathDataHandler
{
    public static readonly  ushort Discriminator       = (ushort)(Environment.TickCount >> 12);
    private static readonly string DiscriminatorString = $"{Discriminator:X4}";
    private const           int    MinimumLength       = 8;

    /// <summary> Additional Data encoded in a path. </summary>
    /// <param name="Collection"> The local ID of the collection. </param>
    /// <param name="ChangeCounter"> The change counter of that collection when this file was loaded. </param>
    /// <param name="OriginalPathCrc32"> The CRC32 of the originally requested path, only used for materials. </param>
    /// <param name="Discriminator"> A discriminator to differ between multiple loads of Penumbra. </param>
    public readonly record struct AdditionalPathData(
        LocalCollectionId Collection,
        int ChangeCounter,
        int OriginalPathCrc32,
        ushort Discriminator)
    {
        public static readonly AdditionalPathData Invalid = new(LocalCollectionId.Zero, 0, 0, PathDataHandler.Discriminator);

        /// <summary> Any collection but the empty collection can appear. In particular, they can be negative for temporary collections. </summary>
        public bool Valid
            => Collection.Id != 0;
    }

    /// <summary> Create the encoding path for an IMC file. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FullPath CreateImc(ByteString path, ModCollection collection)
        => CreateBase(path, collection);

    /// <summary> Create the encoding path for a TMB file. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FullPath CreateTmb(ByteString path, ModCollection collection)
        => CreateBase(path, collection);

    /// <summary> Create the encoding path for an AVFX file. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FullPath CreateAvfx(ByteString path, ModCollection collection)
        => CreateBase(path, collection);

    /// <summary> Create the encoding path for a MTRL file. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FullPath CreateMtrl(ByteString path, ModCollection collection, Utf8GamePath originalPath)
        => new($"|{collection.LocalId.Id}_{collection.ChangeCounter}_{originalPath.Path.Crc32:X8}_{DiscriminatorString}|{path}");

    /// <summary> The base function shared by most file types. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FullPath CreateBase(ByteString path, ModCollection collection)
        => new($"|{collection.LocalId.Id}_{collection.ChangeCounter}_{DiscriminatorString}|{path}");

    /// <summary> Read an additional data blurb and parse it into usable data for all file types but Materials. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Read(ReadOnlySpan<byte> additionalData, out AdditionalPathData data)
        => ReadBase(additionalData, out data, out _);

    /// <summary> Read an additional data blurb and parse it into usable data for Materials. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ReadMtrl(ReadOnlySpan<byte> additionalData, out AdditionalPathData data)
    {
        if (!ReadBase(additionalData, out data, out var remaining))
            return false;

        if (!int.TryParse(remaining, out var crc32))
            return false;

        data = data with { OriginalPathCrc32 = crc32 };
        return true;
    }

    /// <summary> Parse the common attributes of an additional data blurb and return remaining data if there is any. </summary>
    private static bool ReadBase(ReadOnlySpan<byte> additionalData, out AdditionalPathData data, out ReadOnlySpan<byte> remainingData)
    {
        data          = AdditionalPathData.Invalid;
        remainingData = [];

        // At least (\d_\d_\x\x\x\x)
        if (additionalData.Length < MinimumLength)
            return false;

        // Fetch discriminator, constant length.
        var discriminatorSpan = additionalData[^4..];
        if (!ushort.TryParse(discriminatorSpan, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var discriminator))
            return false;

        additionalData = additionalData[..^5];
        var collectionSplit = additionalData.IndexOf((byte)'_');
        if (collectionSplit == -1)
            return false;

        var collectionSpan = additionalData[..collectionSplit];
        additionalData = additionalData[(collectionSplit + 1)..];

        if (!int.TryParse(collectionSpan, out var id))
            return false;

        var changeCounterSpan  = additionalData;
        var changeCounterSplit = additionalData.IndexOf((byte)'_');
        if (changeCounterSplit != -1)
        {
            changeCounterSpan = additionalData[..changeCounterSplit];
            remainingData     = additionalData[(changeCounterSplit + 1)..];
        }

        if (!int.TryParse(changeCounterSpan, out var changeCounter))
            return false;

        data = new AdditionalPathData(new LocalCollectionId(id), changeCounter, 0, discriminator);
        return true;
    }

    /// <summary> Split a given span into the actual path and the additional data blurb. Returns true if a blurb exists. </summary>
    public static bool Split(ReadOnlySpan<byte> text, out ReadOnlySpan<byte> path, out ReadOnlySpan<byte> data)
    {
        if (text.IsEmpty || text[0] is not (byte)'|')
        {
            path = text;
            data = [];
            return false;
        }

        var endIdx = text[1..].IndexOf((byte)'|');
        if (endIdx++ < 0)
        {
            path = text;
            data = [];
            return false;
        }

        data = text.Slice(1, endIdx - 1);
        path = ++endIdx == text.Length ? [] : text[endIdx..];
        return true;
    }

    /// <inheritdoc cref="Split(ReadOnlySpan{byte},out ReadOnlySpan{byte},out ReadOnlySpan{byte})("/>
    public static bool Split(ReadOnlySpan<char> text, out ReadOnlySpan<char> path, out ReadOnlySpan<char> data)
    {
        if (text.Length == 0 || text[0] is not '|')
        {
            path = text;
            data = [];
            return false;
        }

        var endIdx = text[1..].IndexOf('|');
        if (endIdx++ < 0)
        {
            path = text;
            data = [];
            return false;
        }

        data = text.Slice(1, endIdx - 1);
        path = ++endIdx >= text.Length ? [] : text[endIdx..];
        return true;
    }
}
