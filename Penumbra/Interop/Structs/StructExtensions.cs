using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.STD;
using InteropGenerator.Runtime;
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

    public static unsafe CiByteString ResolveSkinMtrlPathAsByteString(ref this CharacterBase character, uint slotIndex)
    {
        var pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveSkinMtrlPath(pathBuffer, CharacterBase.PathBufferSize, slotIndex));
    }

    public static CiByteString ResolveMaterialPapPathAsByteString(ref this CharacterBase character, uint slotIndex, uint unkSId)
    {
        Span<byte> pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveMaterialPapPath(pathBuffer, slotIndex, unkSId));
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

    public static CiByteString ResolvePhybPathAsByteString(ref this CharacterBase character, uint partialSkeletonIndex)
    {
        Span<byte> pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolvePhybPath(pathBuffer, partialSkeletonIndex));
    }

    public static unsafe CiByteString ResolveKdbPathAsByteString(ref this CharacterBase character, uint partialSkeletonIndex)
    {
        var pathBuffer = stackalloc byte[CharacterBase.PathBufferSize];
        return ToOwnedByteString(character.ResolveKdbPath(pathBuffer, CharacterBase.PathBufferSize, partialSkeletonIndex));
    }

    private static unsafe CiByteString ToOwnedByteString(CStringPointer str)
        => str.HasValue ? new CiByteString(str.Value).Clone() : CiByteString.Empty;

    private static CiByteString ToOwnedByteString(ReadOnlySpan<byte> str)
        => str.Length == 0 ? CiByteString.Empty : CiByteString.FromSpanUnsafe(str, true).Clone();
}
