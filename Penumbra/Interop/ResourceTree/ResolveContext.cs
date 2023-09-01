using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.UI;

namespace Penumbra.Interop.ResourceTree;

internal record class GlobalResolveContext(Configuration Config, IObjectIdentifier Identifier, TreeBuildCache TreeBuildCache, ModCollection Collection, int Skeleton, bool WithUIData,
    bool RedactExternalPaths)
{
    public readonly Dictionary<nint, ResourceNode> Nodes = new(128);

    public ResolveContext CreateContext(EquipSlot slot, CharacterArmor equipment)
        => new(Config, Identifier, TreeBuildCache, Collection, Skeleton, WithUIData, RedactExternalPaths, Nodes, slot, equipment);
}

internal record class ResolveContext(Configuration Config, IObjectIdentifier Identifier, TreeBuildCache TreeBuildCache, ModCollection Collection, int Skeleton, bool WithUIData,
    bool RedactExternalPaths, Dictionary<nint, ResourceNode> Nodes, EquipSlot Slot, CharacterArmor Equipment)
{
    private static readonly ByteString ShpkPrefix = ByteString.FromSpanUnsafe("shader/sm5/shpk"u8, true, true, true);
    private unsafe ResourceNode? CreateNodeFromShpk(ShaderPackageResourceHandle* resourceHandle, ByteString gamePath, bool @internal)
    {
        if (Nodes.TryGetValue((nint)resourceHandle, out var cached))
            return cached;

        if (gamePath.IsEmpty)
            return null;
        if (!Utf8GamePath.FromByteString(ByteString.Join((byte)'/', ShpkPrefix, gamePath), out var path, false))
            return null;

        return CreateNodeFromGamePath(ResourceType.Shpk, (nint)resourceHandle->ShaderPackage, &resourceHandle->Handle, path, @internal);
    }

    private unsafe ResourceNode? CreateNodeFromTex(TextureResourceHandle* resourceHandle, ByteString gamePath, bool @internal, bool dx11)
    {
        if (Nodes.TryGetValue((nint)resourceHandle, out var cached))
            return cached;

        if (dx11)
        {
            var lastDirectorySeparator = gamePath.LastIndexOf((byte)'/');
            if (lastDirectorySeparator == -1 || lastDirectorySeparator > gamePath.Length - 3)
                return null;

            if (gamePath[lastDirectorySeparator + 1] != (byte)'-' || gamePath[lastDirectorySeparator + 2] != (byte)'-')
            {
                Span<byte> prefixed = stackalloc byte[gamePath.Length + 2];
                gamePath.Span[..(lastDirectorySeparator + 1)].CopyTo(prefixed);
                prefixed[lastDirectorySeparator + 1] = (byte)'-';
                prefixed[lastDirectorySeparator + 2] = (byte)'-';
                gamePath.Span[(lastDirectorySeparator + 1)..].CopyTo(prefixed[(lastDirectorySeparator + 3)..]);

                if (!Utf8GamePath.FromSpan(prefixed, out var tmp))
                    return null;

                gamePath = tmp.Path.Clone();
            }
        }

        if (!Utf8GamePath.FromByteString(gamePath, out var path))
            return null;

        return CreateNodeFromGamePath(ResourceType.Tex, (nint)resourceHandle->KernelTexture, &resourceHandle->Handle, path, @internal);
    }

    private unsafe ResourceNode CreateNodeFromGamePath(ResourceType type, nint objectAddress, ResourceHandle* resourceHandle, Utf8GamePath gamePath, bool @internal)
    {
        var fullPath = Utf8GamePath.FromByteString(GetResourceHandlePath(resourceHandle), out var p) ? new FullPath(p) : FullPath.Empty;
        if (fullPath.InternalName.IsEmpty)
            fullPath = Collection.ResolvePath(gamePath) ?? new FullPath(gamePath);

        var node = new ResourceNode(default, type, objectAddress, (nint)resourceHandle, gamePath, FilterFullPath(fullPath), GetResourceHandleLength(resourceHandle), @internal);
        if (resourceHandle != null)
            Nodes.Add((nint)resourceHandle, node);

        return node;
    }

    private unsafe ResourceNode? CreateNodeFromResourceHandle(ResourceType type, nint objectAddress, ResourceHandle* handle, bool @internal,
        bool withName)
    {
        var fullPath = Utf8GamePath.FromByteString(GetResourceHandlePath(handle), out var p) ? new FullPath(p) : FullPath.Empty;
        if (fullPath.InternalName.IsEmpty)
            return null;

        var gamePaths = Collection.ReverseResolvePath(fullPath).ToList();
        fullPath = FilterFullPath(fullPath);

        if (gamePaths.Count > 1)
            gamePaths = Filter(gamePaths);

        if (gamePaths.Count == 1)
            return new ResourceNode(withName ? GuessUIDataFromPath(gamePaths[0]) : default, type, objectAddress, (nint)handle, gamePaths[0], fullPath,
                GetResourceHandleLength(handle), @internal);

        Penumbra.Log.Information($"Found {gamePaths.Count} game paths while reverse-resolving {fullPath} in {Collection.Name}:");
        foreach (var gamePath in gamePaths)
            Penumbra.Log.Information($"Game path: {gamePath}");

        return new ResourceNode(default, type, objectAddress, (nint)handle, gamePaths.ToArray(), fullPath, GetResourceHandleLength(handle), @internal);
    }

    public unsafe ResourceNode? CreateHumanSkeletonNode(GenderRace gr)
    {
        var raceSexIdStr = gr.ToRaceCode();
        var path         = $"chara/human/c{raceSexIdStr}/skeleton/base/b0001/skl_c{raceSexIdStr}b0001.sklb";

        if (!Utf8GamePath.FromString(path, out var gamePath))
            return null;

        return CreateNodeFromGamePath(ResourceType.Sklb, 0, null, gamePath, false);
    }

    public unsafe ResourceNode? CreateNodeFromImc(ResourceHandle* imc)
    {
        if (Nodes.TryGetValue((nint)imc, out var cached))
            return cached;

        var node = CreateNodeFromResourceHandle(ResourceType.Imc, 0, imc, true, false);
        if (node == null)
            return null;

        if (WithUIData)
        {
            var uiData = GuessModelUIData(node.GamePath);
            node = node.WithUIData(uiData.PrependName("IMC: "));
        }

        Nodes.Add((nint)imc, node);

        return node;
    }

    public unsafe ResourceNode? CreateNodeFromTex(TextureResourceHandle* tex)
    {
        if (Nodes.TryGetValue((nint)tex, out var cached))
            return cached;

        var node = CreateNodeFromResourceHandle(ResourceType.Tex, (nint)tex->KernelTexture, &tex->Handle, false, WithUIData);
        if (node != null)
            Nodes.Add((nint)tex, node);

        return node;
    }

    public unsafe ResourceNode? CreateNodeFromRenderModel(RenderModel* mdl)
    {
        if (mdl == null || mdl->ResourceHandle == null || mdl->ResourceHandle->Category != ResourceCategory.Chara)
            return null;

        if (Nodes.TryGetValue((nint)mdl->ResourceHandle, out var cached))
            return cached;

        var node = CreateNodeFromResourceHandle(ResourceType.Mdl, (nint) mdl, mdl->ResourceHandle, false, false);
        if (node == null)
            return null;

        if (WithUIData)
            node = node.WithUIData(GuessModelUIData(node.GamePath));

        for (var i = 0; i < mdl->MaterialCount; i++)
        {
            var mtrl     = (Material*)mdl->Materials[i];
            var mtrlNode = CreateNodeFromMaterial(mtrl);
            if (mtrlNode != null)
                // Don't keep the material's name if it's redundant with the model's name.
                node.Children.Add(WithUIData
                    ? mtrlNode.WithUIData((string.Equals(mtrlNode.Name, node.Name, StringComparison.Ordinal) ? null : mtrlNode.Name)
                     ?? $"Material #{i}", mtrlNode.Icon)
                    : mtrlNode);
        }

        Nodes.Add((nint)mdl->ResourceHandle, node);

        return node;
    }

    private unsafe ResourceNode? CreateNodeFromMaterial(Material* mtrl)
    {
        static ushort GetTextureIndex(ushort texFlags)
        {
            if ((texFlags & 0x001F) != 0x001F)
                return (ushort)(texFlags & 0x001F);
            else if ((texFlags & 0x03E0) != 0x03E0)
                return (ushort)((texFlags >> 5) & 0x001F);
            else
                return (ushort)((texFlags >> 10) & 0x001F);
        }
        static uint? GetTextureSamplerId(Material* mtrl, TextureResourceHandle* handle)
        {
            var textures = mtrl->Textures;
            for (var i = 0; i < mtrl->TextureCount; ++i)
            {
                if (textures[i].ResourceHandle == handle)
                    return textures[i].Id;
            }

            return null;
        }
        static uint? GetSamplerCrcById(ShaderPackage* shpk, uint id)
        {
            var samplers = (ShaderPackageUtility.Sampler*)shpk->Samplers;
            for (var i = 0; i < shpk->SamplerCount; ++i)
            {
                if (samplers[i].Id == id)
                    return samplers[i].Crc;
            }

            return null;
        }

        if (mtrl == null)
            return null;

        var resource = mtrl->ResourceHandle;
        if (Nodes.TryGetValue((nint)resource, out var cached))
            return cached;

        var node     = CreateNodeFromResourceHandle(ResourceType.Mtrl, (nint) mtrl, &resource->Handle, false, WithUIData);
        if (node == null)
            return null;

        var shpkNode = CreateNodeFromShpk(resource->ShpkResourceHandle, new ByteString(resource->ShpkString), false);
        if (shpkNode != null)
            node.Children.Add(WithUIData ? shpkNode.WithUIData("Shader Package", 0) : shpkNode);
        var shpkFile = WithUIData && shpkNode != null ? TreeBuildCache.ReadShaderPackage(shpkNode.FullPath) : null;
        var shpk = WithUIData && shpkNode != null ? (ShaderPackage*)shpkNode.ObjectAddress : null;

        for (var i = 0; i < resource->NumTex; i++)
        {
            var texNode = CreateNodeFromTex(resource->TexSpace[i].ResourceHandle, new ByteString(resource->TexString(i)), false, resource->TexIsDX11(i));
            if (texNode == null)
                continue;

            if (WithUIData)
            {
                string? name = null;
                if (shpk != null)
                {
                    var index = GetTextureIndex(resource->TexSpace[i].Flags);
                    uint? samplerId;
                    if (index != 0x001F)
                        samplerId = mtrl->Textures[index].Id;
                    else
                        samplerId = GetTextureSamplerId(mtrl, resource->TexSpace[i].ResourceHandle);
                    if (samplerId.HasValue)
                    {
                        var samplerCrc = GetSamplerCrcById(shpk, samplerId.Value);
                        if (samplerCrc.HasValue)
                            name = shpkFile?.GetSamplerById(samplerCrc.Value)?.Name ?? $"Texture 0x{samplerCrc.Value:X8}";
                    }
                }
                node.Children.Add(texNode.WithUIData(name ?? $"Texture #{i}", 0));
            }
            else
            {
                node.Children.Add(texNode);
            }
        }

        Nodes.Add((nint)resource, node);

        return node;
    }

    public unsafe ResourceNode? CreateNodeFromPartialSkeleton(FFXIVClientStructs.FFXIV.Client.Graphics.Render.PartialSkeleton* sklb)
    {
        if (sklb->SkeletonResourceHandle == null)
            return null;

        if (Nodes.TryGetValue((nint)sklb->SkeletonResourceHandle, out var cached))
            return cached;

        var node = CreateNodeFromResourceHandle(ResourceType.Sklb, (nint)sklb, (ResourceHandle*)sklb->SkeletonResourceHandle, false, WithUIData);
        if (node != null)
            Nodes.Add((nint)sklb->SkeletonResourceHandle, node);

        return node;
    }

    public unsafe ResourceNode? CreateParameterNodeFromPartialSkeleton(FFXIVClientStructs.FFXIV.Client.Graphics.Render.PartialSkeleton* sklb)
    {
        if (sklb->SkeletonParameterResourceHandle == null)
            return null;

        if (Nodes.TryGetValue((nint)sklb->SkeletonParameterResourceHandle, out var cached))
            return cached;

        var node = CreateNodeFromResourceHandle(ResourceType.Skp, (nint)sklb, (ResourceHandle*)sklb->SkeletonParameterResourceHandle, true, WithUIData);
        if (node != null)
            Nodes.Add((nint)sklb->SkeletonParameterResourceHandle, node);

        return node;
    }

    private FullPath FilterFullPath(FullPath fullPath)
    {
        if (!fullPath.IsRooted)
            return fullPath;

        var relPath = Path.GetRelativePath(Config.ModDirectory, fullPath.FullName);
        if (!RedactExternalPaths || relPath == "." || !relPath.StartsWith('.') && !Path.IsPathRooted(relPath))
            return fullPath.Exists ? fullPath : FullPath.Empty;

        return FullPath.Empty;
    }

    private List<Utf8GamePath> Filter(List<Utf8GamePath> gamePaths)
    {
        var filtered = new List<Utf8GamePath>(gamePaths.Count);
        foreach (var path in gamePaths)
        {
            // In doubt, keep the paths.
            if (IsMatch(path.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries))
             ?? true)
                filtered.Add(path);
        }

        return filtered;
    }

    private bool? IsMatch(ReadOnlySpan<string> path)
        => SafeGet(path, 0) switch
        {
            "chara" => SafeGet(path, 1) switch
            {
                "accessory" => IsMatchEquipment(path[2..], $"a{Equipment.Set.Id:D4}"),
                "equipment" => IsMatchEquipment(path[2..], $"e{Equipment.Set.Id:D4}"),
                "monster"   => SafeGet(path, 2) == $"m{Skeleton:D4}",
                "weapon"    => IsMatchEquipment(path[2..], $"w{Equipment.Set.Id:D4}"),
                _           => null,
            },
            _ => null,
        };

    private bool? IsMatchEquipment(ReadOnlySpan<string> path, string equipmentDir)
        => SafeGet(path, 0) == equipmentDir
            ? SafeGet(path, 1) switch
            {
                "material" => SafeGet(path, 2) == $"v{Equipment.Variant.Id:D4}",
                _          => null,
            }
            : false;

    private ResourceNode.UIData GuessModelUIData(Utf8GamePath gamePath)
    {
        var path = gamePath.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries);
        // Weapons intentionally left out.
        var isEquipment = SafeGet(path, 0) == "chara" && SafeGet(path, 1) is "accessory" or "equipment";
        if (isEquipment)
            foreach (var item in Identifier.Identify(Equipment.Set, Equipment.Variant, Slot.ToSlot()))
            {
                var name = Slot switch
                    {
                        EquipSlot.RFinger => "R: ",
                        EquipSlot.LFinger => "L: ",
                        _                 => string.Empty,
                    }
                  + item.Name.ToString();
                return new(name, ChangedItemDrawer.GetCategoryIcon(item.Name, item));
            }

        var dataFromPath = GuessUIDataFromPath(gamePath);
        if (dataFromPath.Name != null)
            return dataFromPath;

        return isEquipment ? new(Slot.ToName(), ChangedItemDrawer.GetCategoryIcon(Slot.ToSlot())) : new(null, ChangedItemDrawer.ChangedItemIcon.Unknown);
    }

    private ResourceNode.UIData GuessUIDataFromPath(Utf8GamePath gamePath)
    {
        foreach (var obj in Identifier.Identify(gamePath.ToString()))
        {
            var name = obj.Key;
            if (name.StartsWith("Customization:"))
                name = name[14..].Trim();
            if (name != "Unknown")
                return new(name, ChangedItemDrawer.GetCategoryIcon(obj.Key, obj.Value));
        }

        return new(null, ChangedItemDrawer.ChangedItemIcon.Unknown);
    }

    private static string? SafeGet(ReadOnlySpan<string> array, Index index)
    {
        var i = index.GetOffset(array.Length);
        return i >= 0 && i < array.Length ? array[i] : null;
    }

    internal static unsafe ByteString GetResourceHandlePath(ResourceHandle* handle)
    {
        if (handle == null)
            return ByteString.Empty;

        var name = handle->FileName();
        if (name.IsEmpty)
            return ByteString.Empty;

        if (name[0] == (byte)'|')
        {
            var pos = name.IndexOf((byte)'|', 1);
            if (pos < 0)
                return ByteString.Empty;

            name = name.Substring(pos + 1);
        }

        return name;
    }

    private static unsafe ulong GetResourceHandleLength(ResourceHandle* handle)
    {
        if (handle == null)
            return 0;

        return ResourceHandle.GetLength(handle);
    }
}
