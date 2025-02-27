using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.Interop;
using OtterGui;
using OtterGui.Text.HelperObjects;
using Penumbra.Api.Enums;
using Penumbra.Collections;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Hooks.PostProcessing;
using Penumbra.Interop.PathResolving;
using Penumbra.Meta;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI;
using static Penumbra.Interop.Structs.StructExtensions;
using CharaBase = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase;

namespace Penumbra.Interop.ResourceTree;

internal record GlobalResolveContext(
    MetaFileManager MetaFileManager,
    ObjectIdentification Identifier,
    ModCollection Collection,
    TreeBuildCache TreeBuildCache,
    bool WithUiData)
{
    public readonly Dictionary<(Utf8GamePath, nint), ResourceNode> Nodes = new(128);

    public unsafe ResolveContext CreateContext(CharaBase* characterBase, uint slotIndex = 0xFFFFFFFFu,
        FullEquipType slot = FullEquipType.Unknown, CharacterArmor equipment = default, SecondaryId secondaryId = default)
        => new(this, characterBase, slotIndex, slot, equipment, secondaryId);
}

internal unsafe partial record ResolveContext(
    GlobalResolveContext Global,
    Pointer<CharaBase> CharacterBasePointer,
    uint SlotIndex,
    FullEquipType Slot,
    CharacterArmor Equipment,
    SecondaryId SecondaryId)
{
    public CharaBase* CharacterBase
        => CharacterBasePointer.Value;

    private static readonly CiByteString ShpkPrefix = CiByteString.FromSpanUnsafe("shader/sm5/shpk"u8, true, true, true);

    private CharaBase.ModelType ModelType
        => CharacterBase->GetModelType();

    private ResourceNode? CreateNodeFromShpk(ShaderPackageResourceHandle* resourceHandle, CiByteString gamePath)
    {
        if (resourceHandle is null)
            return null;
        if (gamePath.IsEmpty)
            return null;
        if (!Utf8GamePath.FromByteString(CiByteString.Join((byte)'/', ShpkPrefix, gamePath), out var path))
            return null;

        return GetOrCreateNode(ResourceType.Shpk, (nint)resourceHandle->ShaderPackage, &resourceHandle->ResourceHandle, path);
    }

    [SkipLocalsInit]
    private ResourceNode? CreateNodeFromTex(TextureResourceHandle* resourceHandle, CiByteString gamePath, bool dx11)
    {
        if (resourceHandle is null)
            return null;

        Utf8GamePath path;
        if (dx11)
        {
            var lastDirectorySeparator = gamePath.LastIndexOf((byte)'/');
            if (lastDirectorySeparator == -1 || lastDirectorySeparator > gamePath.Length - 3)
                return null;

            Span<byte> prefixed = stackalloc byte[CharaBase.PathBufferSize];

            var writer = new SpanTextWriter(prefixed);
            writer.Append(gamePath.Span[..(lastDirectorySeparator + 1)]);
            writer.Append((byte)'-');
            writer.Append((byte)'-');
            writer.Append(gamePath.Span[(lastDirectorySeparator + 1)..]);
            writer.EnsureNullTerminated();

            if (!Utf8GamePath.FromSpan(prefixed[..(gamePath.Length + 2)], MetaDataComputation.None, out var tmp))
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
        if (resourceHandle is null)
            throw new ArgumentNullException(nameof(resourceHandle));

        if (Global.Nodes.TryGetValue((gamePath, (nint)resourceHandle), out var cached))
            return cached;

        return CreateNode(type, objectAddress, resourceHandle, gamePath);
    }

    private ResourceNode CreateNode(ResourceType type, nint objectAddress, ResourceHandle* resourceHandle,
        Utf8GamePath gamePath, bool autoAdd = true)
    {
        if (resourceHandle is null)
            throw new ArgumentNullException(nameof(resourceHandle));

        var fileName       = (ReadOnlySpan<byte>)resourceHandle->FileName.AsSpan();
        var additionalData = CiByteString.Empty;
        if (PathDataHandler.Split(fileName, out fileName, out var data))
            additionalData = CiByteString.FromSpanUnsafe(data, false).Clone();

        var fullPath = Utf8GamePath.FromSpan(fileName, MetaDataComputation.None, out var p) ? new FullPath(p.Clone()) : FullPath.Empty;

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
        if (eid is null)
            return null;

        if (!Utf8GamePath.FromByteString(CharacterBase->ResolveEidPathAsByteString(), out var path))
            return null;

        return GetOrCreateNode(ResourceType.Eid, 0, eid, path);
    }

    public ResourceNode? CreateNodeFromImc(ResourceHandle* imc)
    {
        if (imc is null)
            return null;

        if (!Utf8GamePath.FromByteString(CharacterBase->ResolveImcPathAsByteString(SlotIndex), out var path))
            return null;

        return GetOrCreateNode(ResourceType.Imc, 0, imc, path);
    }

    public ResourceNode? CreateNodeFromPbd(ResourceHandle* pbd)
    {
        if (pbd is null)
            return null;

        return GetOrCreateNode(ResourceType.Pbd, 0, pbd, PreBoneDeformerReplacer.PreBoneDeformerPath);
    }

    public ResourceNode? CreateNodeFromTex(TextureResourceHandle* tex, string gamePath)
    {
        if (tex is null)
            return null;

        if (!Utf8GamePath.FromString(gamePath, out var path))
            return null;

        return GetOrCreateNode(ResourceType.Tex, (nint)tex->Texture, &tex->ResourceHandle, path);
    }

    public ResourceNode? CreateNodeFromTex(TextureResourceHandle* tex, Utf8GamePath gamePath)
    {
        if (tex is null)
            return null;

        return GetOrCreateNode(ResourceType.Tex, (nint)tex->Texture, &tex->ResourceHandle, gamePath);
    }

    public ResourceNode? CreateNodeFromModel(Model* mdl, ResourceHandle* imc, TextureResourceHandle* decalHandle, ResourceHandle* mpapHandle)
    {
        if (mdl is null || mdl->ModelResourceHandle is null)
            return null;

        var mdlResource = mdl->ModelResourceHandle;

        var path = ResolveModelPath();

        if (Global.Nodes.TryGetValue((path, (nint)mdlResource), out var cached))
            return cached;

        var node = CreateNode(ResourceType.Mdl, (nint)mdl, &mdlResource->ResourceHandle, path, false);

        for (var i = 0; i < mdl->MaterialCount; i++)
        {
            var mtrl = mdl->Materials[i];
            if (mtrl is null)
                continue;

            var mtrlFileName = mdlResource->GetMaterialFileNameBySlot((uint)i);
            var mtrlNode     = CreateNodeFromMaterial(mtrl, ResolveMaterialPath(path, imc, mtrlFileName));
            if (mtrlNode is not null)
            {
                if (Global.WithUiData)
                    mtrlNode.FallbackName = $"Material #{i}";
                node.Children.Add(mtrlNode);
            }
        }

        if (CreateNodeFromDecal(decalHandle, imc) is { } decalNode)
            node.Children.Add(decalNode);

        if (CreateNodeFromMaterialPap(mpapHandle, imc) is { } mpapNode)
            node.Children.Add(mpapNode);

        Global.Nodes.Add((path, (nint)mdl->ModelResourceHandle), node);

        return node;
    }

    private ResourceNode? CreateNodeFromMaterial(Material* mtrl, Utf8GamePath path)
    {
        if (mtrl is null || mtrl->MaterialResourceHandle is null)
            return null;

        var resource = mtrl->MaterialResourceHandle;
        if (Global.Nodes.TryGetValue((path, (nint)resource), out var cached))
            return cached;

        var node     = CreateNode(ResourceType.Mtrl, (nint)mtrl, &resource->ResourceHandle, path, false);
        var shpkNode = CreateNodeFromShpk(resource->ShaderPackageResourceHandle, new CiByteString(resource->ShpkName));
        if (shpkNode is not null)
        {
            if (Global.WithUiData)
                shpkNode.Name = "Shader Package";
            node.Children.Add(shpkNode);
        }

        var shpkNames = Global.WithUiData && shpkNode is not null ? Global.TreeBuildCache.ReadShaderPackageNames(shpkNode.FullPath) : null;
        var shpk      = Global.WithUiData && shpkNode is not null ? (ShaderPackage*)shpkNode.ObjectAddress : null;

        var alreadyProcessedSamplerIds = new HashSet<uint>();
        for (var i = 0; i < resource->TextureCount; i++)
        {
            var texNode = CreateNodeFromTex(resource->Textures[i].TextureResourceHandle, new CiByteString(resource->TexturePath(i)),
                resource->Textures[i].IsDX11);
            if (texNode == null)
                continue;

            if (Global.WithUiData)
            {
                string? name = null;
                if (shpk is not null)
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
                        {
                            if (shpkNames != null && shpkNames.TryGetValue(samplerCrc.Value, out var samplerName))
                                name = samplerName.Value;
                            else
                                name = $"Texture 0x{samplerCrc.Value:X8}";
                        }
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

    public ResourceNode? CreateNodeFromDecal(TextureResourceHandle* decalHandle, ResourceHandle* imc)
    {
        if (decalHandle is null)
            return null;

        var path = ResolveDecalPath(imc);
        if (path.IsEmpty)
            return null;

        var node = CreateNodeFromTex(decalHandle, path)!;
        if (Global.WithUiData)
            node.FallbackName = "Decal";

        return node;
    }

    public ResourceNode? CreateNodeFromMaterialPap(ResourceHandle* mpapHandle, ResourceHandle* imc)
    {
        if (mpapHandle is null)
            return null;

        var path = ResolveMaterialAnimationPath(imc);
        if (path.IsEmpty)
            return null;

        if (Global.Nodes.TryGetValue((path, (nint)mpapHandle), out var cached))
            return cached;

        var node = CreateNode(ResourceType.Pap, 0, mpapHandle, path);
        if (Global.WithUiData)
            node.FallbackName = "Material Animation";

        return node;
    }

    public ResourceNode? CreateNodeFromMaterialSklb(SkeletonResourceHandle* sklbHandle)
    {
        if (sklbHandle is null)
            return null;

        if (Global.Nodes.TryGetValue((GamePaths.Sklb.MaterialAnimationSkeletonUtf8, (nint)sklbHandle), out var cached))
            return cached;

        var node = CreateNode(ResourceType.Sklb, 0, (ResourceHandle*)sklbHandle, GamePaths.Sklb.MaterialAnimationSkeletonUtf8);
        node.ForceInternal = true;

        return node;
    }

    public ResourceNode? CreateNodeFromPartialSkeleton(PartialSkeleton* sklb, ResourceHandle* phybHandle, uint partialSkeletonIndex)
    {
        if (sklb is null || sklb->SkeletonResourceHandle is null)
            return null;

        var path = ResolveSkeletonPath(partialSkeletonIndex);

        if (Global.Nodes.TryGetValue((path, (nint)sklb->SkeletonResourceHandle), out var cached))
            return cached;

        var node = CreateNode(ResourceType.Sklb, (nint)sklb, (ResourceHandle*)sklb->SkeletonResourceHandle, path, false);
        if (CreateParameterNodeFromPartialSkeleton(sklb, partialSkeletonIndex) is { } skpNode)
            node.Children.Add(skpNode);
        if (CreateNodeFromPhyb(phybHandle, partialSkeletonIndex) is { } phybNode)
            node.Children.Add(phybNode);
        Global.Nodes.Add((path, (nint)sklb->SkeletonResourceHandle), node);

        return node;
    }

    private ResourceNode? CreateParameterNodeFromPartialSkeleton(PartialSkeleton* sklb, uint partialSkeletonIndex)
    {
        if (sklb is null || sklb->SkeletonParameterResourceHandle is null)
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

    private ResourceNode? CreateNodeFromPhyb(ResourceHandle* phybHandle, uint partialSkeletonIndex)
    {
        if (phybHandle is null)
            return null;

        var path = ResolvePhysicsModulePath(partialSkeletonIndex);

        if (Global.Nodes.TryGetValue((path, (nint)phybHandle), out var cached))
            return cached;

        var node = CreateNode(ResourceType.Phyb, 0, phybHandle, path, false);
        if (Global.WithUiData)
            node.FallbackName = "Physics Module";
        Global.Nodes.Add((path, (nint)phybHandle), node);

        return node;
    }

    internal ResourceNode.UiData GuessModelUiData(Utf8GamePath gamePath)
    {
        var path = gamePath.Path.Split((byte)'/');
        // Weapons intentionally left out.
        var isEquipment = path.Count >= 2
         && path[0].Span.SequenceEqual("chara"u8)
         && (path[1].Span.SequenceEqual("accessory"u8) || path[1].Span.SequenceEqual("equipment"u8));
        if (isEquipment)
            foreach (var item in Global.Identifier.Identify(Equipment.Set, 0, Equipment.Variant, Slot.ToSlot()))
            {
                var name = item.Name;
                if (Slot is FullEquipType.Finger)
                    name = SlotIndex switch
                    {
                        8 => "R: " + name,
                        9 => "L: " + name,
                        _ => name,
                    };
                return new ResourceNode.UiData(name, item.Type.GetCategoryIcon().ToFlag());
            }

        var dataFromPath = GuessUiDataFromPath(gamePath);
        if (dataFromPath.Name is not null)
            return dataFromPath;

        return isEquipment
            ? new ResourceNode.UiData(Slot.ToName(), Slot.GetCategoryIcon().ToFlag())
            : new ResourceNode.UiData(null,          ChangedItemIconFlag.Unknown);
    }

    internal ResourceNode.UiData GuessUiDataFromPath(Utf8GamePath gamePath)
    {
        const string customization = "Customization: ";
        foreach (var obj in Global.Identifier.Identify(gamePath.ToString()))
        {
            var name = obj.Key;
            if (name.StartsWith(customization))
                name = name.AsSpan(14).Trim().ToString();
            if (name is not "Unknown")
                return new ResourceNode.UiData(name, obj.Value.GetIcon().ToFlag());
        }

        return new ResourceNode.UiData(null, ChangedItemIconFlag.Unknown);
    }

    private static ulong GetResourceHandleLength(ResourceHandle* handle)
        => handle is null ? 0ul : handle->GetLength();
}
