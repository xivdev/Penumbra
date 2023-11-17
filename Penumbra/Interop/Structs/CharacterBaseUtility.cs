using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.String;

namespace Penumbra.Interop.Structs;

// TODO submit these to ClientStructs
public static unsafe class CharacterBaseUtility
{
    private const int PathBufferSize = 260;

    private const uint ResolveSklbPathVf = 72;
    private const uint ResolveMdlPathVf = 73;
    private const uint ResolveSkpPathVf = 74;
    private const uint ResolveImcPathVf = 81;
    private const uint ResolveMtrlPathVf = 82;
    private const uint ResolveEidPathVf = 85;

    private static void* GetVFunc(CharacterBase* characterBase, uint vfIndex)
        => ((void**)characterBase->VTable)[vfIndex];

    private static ByteString? ResolvePath(CharacterBase* characterBase, uint vfIndex)
    {
        var vFunc = (delegate* unmanaged<CharacterBase*, byte*, nint, byte*>)GetVFunc(characterBase, vfIndex);
        var pathBuffer = stackalloc byte[PathBufferSize];
        var path = vFunc(characterBase, pathBuffer, PathBufferSize);
        return path != null ? new ByteString(path).Clone() : null;
    }

    private static ByteString? ResolvePath(CharacterBase* characterBase, uint vfIndex, uint slotIndex)
    {
        var vFunc = (delegate* unmanaged<CharacterBase*, byte*, nint, uint, byte*>)GetVFunc(characterBase, vfIndex);
        var pathBuffer = stackalloc byte[PathBufferSize];
        var path = vFunc(characterBase, pathBuffer, PathBufferSize, slotIndex);
        return path != null ? new ByteString(path).Clone() : null;
    }

    private static ByteString? ResolvePath(CharacterBase* characterBase, uint vfIndex, uint slotIndex, byte* name)
    {
        var vFunc = (delegate* unmanaged<CharacterBase*, byte*, nint, uint, byte*, byte*>)GetVFunc(characterBase, vfIndex);
        var pathBuffer = stackalloc byte[PathBufferSize];
        var path = vFunc(characterBase, pathBuffer, PathBufferSize, slotIndex, name);
        return path != null ? new ByteString(path).Clone() : null;
    }

    public static ByteString? ResolveEidPath(CharacterBase* characterBase)
        => ResolvePath(characterBase, ResolveEidPathVf);

    public static ByteString? ResolveImcPath(CharacterBase* characterBase, uint slotIndex)
        => ResolvePath(characterBase, ResolveImcPathVf, slotIndex);

    public static ByteString? ResolveMdlPath(CharacterBase* characterBase, uint slotIndex)
        => ResolvePath(characterBase, ResolveMdlPathVf, slotIndex);

    public static ByteString? ResolveMtrlPath(CharacterBase* characterBase, uint slotIndex, byte* mtrlFileName)
        => ResolvePath(characterBase, ResolveMtrlPathVf, slotIndex, mtrlFileName);

    public static ByteString? ResolveSklbPath(CharacterBase* characterBase, uint partialSkeletonIndex)
        => ResolvePath(characterBase, ResolveSklbPathVf, partialSkeletonIndex);

    public static ByteString? ResolveSkpPath(CharacterBase* characterBase, uint partialSkeletonIndex)
        => ResolvePath(characterBase, ResolveSkpPathVf, partialSkeletonIndex);
}
