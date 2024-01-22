using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.STD;
using Penumbra.String;

namespace Penumbra.Interop.Structs;

internal static class StructExtensions
{
    public static unsafe ByteString AsByteString(in this StdString str)
        => ByteString.FromSpanUnsafe(str.AsSpan(), true);

    public static ByteString ResolveEidPathAsByteString(in this CharacterBase character)
    {
        Span<byte> pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveEidPath(pathBuffer));
    }

    public static ByteString ResolveImcPathAsByteString(in this CharacterBase character, uint slotIndex)
    {
        Span<byte> pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveImcPath(pathBuffer, slotIndex));
    }

    public static ByteString ResolveMdlPathAsByteString(in this CharacterBase character, uint slotIndex)
    {
        Span<byte> pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveMdlPath(pathBuffer, slotIndex));
    }

    public static unsafe ByteString ResolveMtrlPathAsByteString(in this CharacterBase character, uint slotIndex, byte* mtrlFileName)
    {
        var pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveMtrlPath(pathBuffer, CharacterBase.PathBufferSize, slotIndex, mtrlFileName));
    }

    public static ByteString ResolveSklbPathAsByteString(in this CharacterBase character, uint partialSkeletonIndex)
    {
        Span<byte> pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveSklbPath(pathBuffer, partialSkeletonIndex));
    }

    public static ByteString ResolveSkpPathAsByteString(in this CharacterBase character, uint partialSkeletonIndex)
    {
        Span<byte> pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveSkpPath(pathBuffer, partialSkeletonIndex));
    }

    private static unsafe ByteString ToOwnedByteString(byte* str)
        => str == null ? ByteString.Empty : new ByteString(str).Clone();

    private static ByteString ToOwnedByteString(ReadOnlySpan<byte> str)
        => str.Length == 0 ? ByteString.Empty : ByteString.FromSpanUnsafe(str, true).Clone();
}
