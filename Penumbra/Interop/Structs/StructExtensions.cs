using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.STD;
using Penumbra.String;

namespace Penumbra.Interop.Structs;

internal static class StructExtensions
{
    public static CiByteString AsByteString(in this StdString str)
        => CiByteString.FromSpanUnsafe(str.AsSpan(), true);

    public static CiByteString ResolveEidPathAsByteString(ref this CharacterBase character)
    {
        Span<byte> pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveEidPath(pathBuffer));
    }

    public static CiByteString ResolveImcPathAsByteString(ref this CharacterBase character, uint slotIndex)
    {
        Span<byte> pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveImcPath(pathBuffer, slotIndex));
    }

    public static CiByteString ResolveMdlPathAsByteString(ref this CharacterBase character, uint slotIndex)
    {
        Span<byte> pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveMdlPath(pathBuffer, slotIndex));
    }

    public static unsafe CiByteString ResolveMtrlPathAsByteString(ref this CharacterBase character, uint slotIndex, byte* mtrlFileName)
    {
        var pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveMtrlPath(pathBuffer, CharacterBase.PathBufferSize, slotIndex, mtrlFileName));
    }

    public static CiByteString ResolveSklbPathAsByteString(ref this CharacterBase character, uint partialSkeletonIndex)
    {
        Span<byte> pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveSklbPath(pathBuffer, partialSkeletonIndex));
    }

    public static CiByteString ResolveSkpPathAsByteString(ref this CharacterBase character, uint partialSkeletonIndex)
    {
        Span<byte> pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveSkpPath(pathBuffer, partialSkeletonIndex));
    }

    private static unsafe CiByteString ToOwnedByteString(byte* str)
        => str == null ? CiByteString.Empty : new CiByteString(str).Clone();

    private static CiByteString ToOwnedByteString(ReadOnlySpan<byte> str)
        => str.Length == 0 ? CiByteString.Empty : CiByteString.FromSpanUnsafe(str, true).Clone();
}
