using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.Interop;
using OtterGui;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.PathResolving;
using Penumbra.Interop.Services;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI;
using static Penumbra.Interop.Structs.StructExtensions;
using ModelType = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase.ModelType;

namespace Penumbra.Interop.ResourceTree;

internal record GlobalResolveContext(ObjectIdentification Identifier, ModCollection Collection, TreeBuildCache TreeBuildCache, bool WithUiData)
{
    public readonly Dictionary<(Utf8GamePath, nint), ResourceNode> Nodes = new(128);

    public unsafe ResolveContext CreateContext(CharacterBase* characterBase, uint slotIndex = 0xFFFFFFFFu,
        EquipSlot slot = EquipSlot.Unknown, CharacterArmor equipment = default, SecondaryId secondaryId = default)
        => new(this, characterBase, slotIndex, slot, equipment, secondaryId);
}

internal unsafe partial record ResolveContext(
    GlobalResolveContext Global,
    Pointer<CharacterBase> CharacterBasePointer,
    uint SlotIndex,
    EquipSlot Slot,
    CharacterArmor Equipment,
    SecondaryId SecondaryId)
{
    public CharacterBase* CharacterBase
        => CharacterBasePointer.Value;

    private static readonly ByteString ShpkPrefix = ByteString.FromSpanUnsafe("shader/sm5/shpk"u8, true, true, true);

    private ModelType ModelType
        => CharacterBase->GetModelType();

    private ResourceNode? CreateNodeFromShpk(ShaderPackageResourceHandle* resourceHandle, ByteString gamePath)
    {
        if (resourceHandle == null)
            return null;
        if (gamePath.IsEmpty)
            return null;
        if (!Utf8GamePath.FromByteString(ByteString.Join((byte)'/', ShpkPrefix, gamePath), out var path, false))
            return null;

        return GetOrCreateNode(ResourceType.Shpk, (nint)resourceHandle->ShaderPackage, &resourceHandle->ResourceHandle, path);
    }

    [SkipLocalsInit]
    private ResourceNode? CreateNodeFromTex(TextureResourceHandle* resourceHandle, ByteString gamePath, bool dx11)
    {
        if (resourceHandle == null)
            return null;

        Utf8GamePath path;
        if (dx11)
        {
            var lastDirectorySeparator = gamePath.LastIndexOf((byte)'/');
            if (lastDirectorySeparator == -1 || lastDirectorySeparator > gamePath.Length - 3)
                return null;

            Span<byte> prefixed = stackalloc byte[260];
            gamePath.Span[..(lastDirectorySeparator + 1)].CopyTo(prefixed);
            prefixed[lastDirectorySeparator + 1] = (byte)'-';
            prefixed[lastDirectorySeparator + 2] = (byte)'-';
            gamePath.Span[(lastDirectorySeparator + 1)..].CopyTo(prefixed[(lastDirectorySeparator + 3)..]);

            if (!Utf8GamePath.FromSpan(prefixed[..(gamePath.Length + 2)], out var tmp))
                return null;

            path = tmp.Clone();
        }
        else
        {
            // Make sure the game path is owned, otherwise stale trees could cause crashes (access violations) or other memory safety issues.
            if (!gamePath.IsOwned)
                gamePath = gamePath.Clone();

            if (!Utf8GamePath.FromByteString(gamePath, out path))
                return null;
        }

        return GetOrCreateNode(ResourceType.Tex, (nint)resourceHandle->Texture, &resourceHandle->ResourceHandle, path);
    }

    private ResourceNode GetOrCreateNode(ResourceType type, nint objectAddress, ResourceHandle* resourceHandle,
        Utf8GamePath gamePath)
    {
        if (resourceHandle == null)
            throw new ArgumentNullException(nameof(resourceHandle));

        if (Global.Nodes.TryGetValue((gamePath, (nint)resourceHandle), out var cached))
            return cached;

        return CreateNode(type, objectAddress, resourceHandle, gamePath);
    }

    private ResourceNode CreateNode(ResourceType type, nint objectAddress, ResourceHandle* resourceHandle,
        Utf8GamePath gamePath, bool autoAdd = true)
    {
        if (resourceHandle == null)
            throw new ArgumentNullException(nameof(resourceHandle));

        var fileName       = resourceHandle->FileName.AsSpan();
        var additionalData = ByteString.Empty;
        if (PathDataHandler.Split(fileName, out fileName, out var data))
            additionalData = ByteString.FromSpanUnsafe(data, false).Clone();

        var fullPath = Utf8GamePath.FromSpan(fileName, out var p) ? new FullPath(p.Clone()) : FullPath.Empty;

        var node = new ResourceNode(type, objectAddress, (nint)resourceHandle, GetResourceHandleLength(resourceHandle), this)
        {
            GamePath       = gamePath,
            FullPath       = fullPath,
            AdditionalData = additionalData,
        };
        if (autoAdd)
            Global.Nodes.Add((gamePath, (nint)resourceHandle), node);

        return node;
    }

    public ResourceNode? CreateNodeFromEid(ResourceHandle* eid)
    {
        if (eid == null)
            return null;

        if (!Utf8GamePath.FromByteString(CharacterBase->ResolveEidPathAsByteString(), out var path))
            return null;

        return GetOrCreateNode(ResourceType.Eid, 0, eid, path);
    }

    public ResourceNode? CreateNodeFromImc(ResourceHandle* imc)
    {
        if (imc == null)
            return null;

        if (!Utf8GamePath.FromByteString(CharacterBase->ResolveImcPathAsByteString(SlotIndex), out var path))
            return null;

        return GetOrCreateNode(ResourceType.Imc, 0, imc, path);
    }

    public ResourceNode? CreateNodeFromPbd(ResourceHandle* pbd)
    {
        if (pbd == null)
            return null;

        return GetOrCreateNode(ResourceType.Pbd, 0, pbd, PreBoneDeformerReplacer.PreBoneDeformerPath);
    }

    public ResourceNode? CreateNodeFromTex(TextureResourceHandle* tex, string gamePath)
    {
        if (tex == null)
            return null;

        if (!Utf8GamePath.FromString(gamePath, out var path))
            return null;

        return GetOrCreateNode(ResourceType.Tex, (nint)tex->Texture, &tex->ResourceHandle, path);
    }

    public ResourceNode? CreateNodeFromModel(Model* mdl, ResourceHandle* imc)
    {
        if (mdl == null || mdl->ModelResourceHandle == null)
            return null;

        var mdlResource = mdl->ModelResourceHandle;

        var path = ResolveModelPath();

        if (Global.Nodes.TryGetValue((path, (nint)mdlResource), out var cached))
            return cached;

        var node = CreateNode(ResourceType.Mdl, (nint)mdl, &mdlResource->ResourceHandle, path, false);

        for (var i = 0; i < mdl->MaterialCount; i++)
        {
            var mtrl = mdl->Materials[i];
            if (mtrl == null)
                continue;

            var mtrlFileName = mdlResource->GetMaterialFileNameBySlot((uint)i);
            var mtrlNode     = CreateNodeFromMaterial(mtrl, ResolveMaterialPath(path, imc, mtrlFileName));
            if (mtrlNode != null)
            {
                if (Global.WithUiData)
                    mtrlNode.FallbackName = $"Material #{i}";
                node.Children.Add(mtrlNode);
            }
        }

        Global.Nodes.Add((path, (nint)mdl->ModelResourceHandle), node);

        return node;
    }

    private ResourceNode? CreateNodeFromMaterial(Material* mtrl, Utf8GamePath path)
    {
        if (mtrl == null || mtrl->MaterialResourceHandle == null)
            return null;

        var resource = mtrl->MaterialResourceHandle;
        if (Global.Nodes.TryGetValue((path, (nint)resource), out var cached))
            return cached;

        var node     = CreateNode(ResourceType.Mtrl, (nint)mtrl, &resource->ResourceHandle, path, false);
        var shpkNode = CreateNodeFromShpk(resource->ShaderPackageResourceHandle, new ByteString(resource->ShpkName));
        if (shpkNode != null)
        {
            if (Global.WithUiData)
                shpkNode.Name = "Shader Package";
            node.Children.Add(shpkNode);
        }

        var shpkFile = Global.WithUiData && shpkNode != null ? Global.TreeBuildCache.ReadShaderPackage(shpkNode.FullPath) : null;
        var shpk     = Global.WithUiData && shpkNode != null ? (ShaderPackage*)shpkNode.ObjectAddress : null;

        var alreadyProcessedSamplerIds = new HashSet<uint>();
        for (var i = 0; i < resource->TextureCount; i++)
        {
            var texNode = CreateNodeFromTex(resource->Textures[i].TextureResourceHandle, new ByteString(resource->TexturePath(i)),
                resource->Textures[i].IsDX11);
            if (texNode == null)
                continue;

            if (Global.WithUiData)
            {
                string? name = null;
                if (shpk != null)
                {
                    var index = GetTextureIndex(mtrl, resource->Textures[i].Flags, alreadyProcessedSamplerIds);
                    var samplerId = index != 0x001F
                        ? mtrl->Textures[index].Id
                        : GetTextureSamplerId(mtrl, resource->Textures[i].TextureResourceHandle, alreadyProcessedSamplerIds);
                    if (samplerId.HasValue)
                    {
                        alreadyProcessedSamplerIds.Add(samplerId.Value);
                        var samplerCrc = GetSamplerCrcById(shpk, samplerId.Value);
                        if (samplerCrc.HasValue)
                            name = shpkFile?.GetSamplerById(samplerCrc.Value)?.Name ?? $"Texture 0x{samplerCrc.Value:X8}";
                    }
                }

                texNode      = texNode.Clone();
                texNode.Name = name ?? $"Texture #{i}";
            }

            node.Children.Add(texNode);
        }

        Global.Nodes.Add((path, (nint)resource), node);

        return node;

        static uint? GetSamplerCrcById(ShaderPackage* shpk, uint id)
            => shpk->SamplersSpan.FindFirst(s => s.Id == id, out var s)
                ? s.CRC
                : null;

        static uint? GetTextureSamplerId(Material* mtrl, TextureResourceHandle* handle, HashSet<uint> alreadyVisitedSamplerIds)
            => mtrl->TexturesSpan.FindFirst(p => p.Texture == handle && !alreadyVisitedSamplerIds.Contains(p.Id), out var p)
                ? p.Id
                : null;

        static ushort GetTextureIndex(Material* mtrl, ushort texFlags, HashSet<uint> alreadyVisitedSamplerIds)
        {
            if ((texFlags & 0x001F) != 0x001F && !alreadyVisitedSamplerIds.Contains(mtrl->Textures[texFlags & 0x001F].Id))
                return (ushort)(texFlags & 0x001F);
            if ((texFlags & 0x03E0) != 0x03E0 && !alreadyVisitedSamplerIds.Contains(mtrl->Textures[(texFlags >> 5) & 0x001F].Id))
                return (ushort)((texFlags >> 5) & 0x001F);
            if ((texFlags & 0x7C00) != 0x7C00 && !alreadyVisitedSamplerIds.Contains(mtrl->Textures[(texFlags >> 10) & 0x001F].Id))
                return (ushort)((texFlags >> 10) & 0x001F);

            return 0x001F;
        }
    }

    public ResourceNode? CreateNodeFromPartialSkeleton(PartialSkeleton* sklb, uint partialSkeletonIndex)
    {
        if (sklb == null || sklb->SkeletonResourceHandle == null)
            return null;

        var path = ResolveSkeletonPath(partialSkeletonIndex);

        if (Global.Nodes.TryGetValue((path, (nint)sklb->SkeletonResourceHandle), out var cached))
            return cached;

        var node    = CreateNode(ResourceType.Sklb, (nint)sklb, (ResourceHandle*)sklb->SkeletonResourceHandle, path, false);
        var skpNode = CreateParameterNodeFromPartialSkeleton(sklb, partialSkeletonIndex);
        if (skpNode != null)
            node.Children.Add(skpNode);
        Global.Nodes.Add((path, (nint)sklb->SkeletonResourceHandle), node);

        return node;
    }

    private ResourceNode? CreateParameterNodeFromPartialSkeleton(PartialSkeleton* sklb, uint partialSkeletonIndex)
    {
        if (sklb == null || sklb->SkeletonParameterResourceHandle == null)
            return null;

        var path = ResolveSkeletonParameterPath(partialSkeletonIndex);

        if (Global.Nodes.TryGetValue((path, (nint)sklb->SkeletonParameterResourceHandle), out var cached))
            return cached;

        var node = CreateNode(ResourceType.Skp, (nint)sklb, (ResourceHandle*)sklb->SkeletonParameterResourceHandle, path, false);
        if (Global.WithUiData)
            node.FallbackName = "Skeleton Parameters";
        Global.Nodes.Add((path, (nint)sklb->SkeletonParameterResourceHandle), node);

        return node;
    }

    internal ResourceNode.UiData GuessModelUiData(Utf8GamePath gamePath)
    {
        var path = gamePath.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries);
        // Weapons intentionally left out.
        var isEquipment = SafeGet(path, 0) == "chara" && SafeGet(path, 1) is "accessory" or "equipment";
        if (isEquipment)
            foreach (var item in Global.Identifier.Identify(Equipment.Set, 0, Equipment.Variant, Slot.ToSlot()))
            {
                var name = Slot switch
                    {
                        EquipSlot.RFinger => "R: ",
                        EquipSlot.LFinger => "L: ",
                        _                 => string.Empty,
                    }
                  + item.Name;
                return new ResourceNode.UiData(name, ChangedItemDrawer.GetCategoryIcon(item.Name, item));
            }

        var dataFromPath = GuessUiDataFromPath(gamePath);
        if (dataFromPath.Name != null)
            return dataFromPath;

        return isEquipment
            ? new ResourceNode.UiData(Slot.ToName(), ChangedItemDrawer.GetCategoryIcon(Slot.ToSlot()))
            : new ResourceNode.UiData(null,          ChangedItemDrawer.ChangedItemIcon.Unknown);
    }

    internal ResourceNode.UiData GuessUiDataFromPath(Utf8GamePath gamePath)
    {
        foreach (var obj in Global.Identifier.Identify(gamePath.ToString()))
        {
            var name = obj.Key;
            if (name.StartsWith("Customization:"))
                name = name[14..].Trim();
            if (name != "Unknown")
                return new ResourceNode.UiData(name, ChangedItemDrawer.GetCategoryIcon(obj.Key, obj.Value));
        }

        return new ResourceNode.UiData(null, ChangedItemDrawer.ChangedItemIcon.Unknown);
    }

    private static string? SafeGet(ReadOnlySpan<string> array, Index index)
    {
        var i = index.GetOffset(array.Length);
        return i >= 0 && i < array.Length ? array[i] : null;
    }

    private static ulong GetResourceHandleLength(ResourceHandle* handle)
    {
        if (handle == null)
            return 0;

        return handle->GetLength();
    }
}
