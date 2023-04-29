using System;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.Interop.ResourceLoading;
using Penumbra.String.Classes;

namespace Penumbra.Interop.Services;

public sealed unsafe class DecalReverter : IDisposable
{
    public static readonly Utf8GamePath DecalPath =
        Utf8GamePath.FromSpan("chara/common/texture/decal_equip/_stigma.tex"u8, out var p) ? p : Utf8GamePath.Empty;

    public static readonly Utf8GamePath TransparentPath =
        Utf8GamePath.FromSpan("chara/common/texture/transparent.tex"u8, out var p) ? p : Utf8GamePath.Empty;

    private readonly CharacterUtility _utility;
    private readonly Structs.TextureResourceHandle* _decal;
    private readonly Structs.TextureResourceHandle* _transparent;

    public DecalReverter(CharacterUtility utility, ResourceService resources, ModCollection? collection, bool doDecal)
    {
        _utility = utility;
        var ptr = _utility.Address;
        _decal = null;
        _transparent = null;
        if (doDecal)
        {
            var decalPath = collection?.ResolvePath(DecalPath)?.InternalName ?? DecalPath.Path;
            var decalHandle = resources.GetResource(ResourceCategory.Chara, ResourceType.Tex, decalPath);
            _decal = (Structs.TextureResourceHandle*)decalHandle;
            if (_decal != null)
                ptr->DecalTexResource = _decal;
        }
        else
        {
            var transparentPath = collection?.ResolvePath(TransparentPath)?.InternalName ?? TransparentPath.Path;
            var transparentHandle = resources.GetResource(ResourceCategory.Chara, ResourceType.Tex, transparentPath);
            _transparent = (Structs.TextureResourceHandle*)transparentHandle;
            if (_transparent != null)
                ptr->TransparentTexResource = _transparent;
        }
    }

    public void Dispose()
    {
        var ptr = _utility.Address;
        if (_decal != null)
        {
            ptr->DecalTexResource = (Structs.TextureResourceHandle*)_utility.DefaultDecalResource;
            --_decal->Handle.RefCount;
        }

        if (_transparent != null)
        {
            ptr->TransparentTexResource = (Structs.TextureResourceHandle*)_utility.DefaultTransparentResource;
            --_transparent->Handle.RefCount;
        }
    }
}
