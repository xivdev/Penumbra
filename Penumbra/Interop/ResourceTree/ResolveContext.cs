using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;

namespace Penumbra.Interop.ResourceTree;

internal record class GlobalResolveContext(Configuration Config, IObjectIdentifier Identifier, TreeBuildCache TreeBuildCache, ModCollection Collection, int Skeleton, bool WithNames)
{
    public ResolveContext CreateContext(EquipSlot slot, CharacterArmor equipment)
        => new(Config, Identifier, TreeBuildCache, Collection, Skeleton, WithNames, slot, equipment);
}

internal record class ResolveContext(Configuration Config, IObjectIdentifier Identifier, TreeBuildCache TreeBuildCache, ModCollection Collection, int Skeleton, bool WithNames, EquipSlot Slot,
    CharacterArmor Equipment)
{
    private static readonly ByteString ShpkPrefix = ByteString.FromSpanUnsafe("shader/sm5/shpk"u8, true, true, true);
    private ResourceNode? CreateNodeFromShpk(nint sourceAddress, ByteString gamePath, bool @internal)
    {
        if (gamePath.IsEmpty)
            return null;
        if (!Utf8GamePath.FromByteString(ByteString.Join((byte)'/', ShpkPrefix, gamePath), out var path, false))
            return null;

        return CreateNodeFromGamePath(ResourceType.Shpk, sourceAddress, path, @internal);
    }

    private ResourceNode? CreateNodeFromTex(nint sourceAddress, ByteString gamePath, bool @internal, bool dx11)
    {
        if (dx11)
        {
            var lastDirectorySeparator = gamePath.LastIndexOf((byte)'/');
            if (lastDirectorySeparator == -1 || lastDirectorySeparator > gamePath.Length - 3)
                return null;

            if (gamePath[lastDirectorySeparator + 1] != (byte)'-' || gamePath[lastDirectorySeparator + 2] != (byte)'-')
            {
                Span<byte> prefixed = stackalloc byte[gamePath.Length + 3];
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

        return CreateNodeFromGamePath(ResourceType.Tex, sourceAddress, path, @internal);
    }

    private ResourceNode CreateNodeFromGamePath(ResourceType type, nint sourceAddress, Utf8GamePath gamePath, bool @internal)
        => new(null, type, sourceAddress, gamePath, FilterFullPath(Collection.ResolvePath(gamePath) ?? new FullPath(gamePath)), @internal);

    private unsafe ResourceNode? CreateNodeFromResourceHandle(ResourceType type, nint sourceAddress, ResourceHandle* handle, bool @internal,
        bool withName)
    {
        if (handle == null)
            return null;

        var name = handle->FileName();
        if (name.IsEmpty)
            return null;

        if (name[0] == (byte)'|')
        {
            var pos = name.IndexOf((byte)'|', 1);
            if (pos < 0)
                return null;

            name = name.Substring(pos + 1);
        }

        var fullPath  = new FullPath(Utf8GamePath.FromByteString(name, out var p) ? p : Utf8GamePath.Empty);
        var gamePaths = Collection.ReverseResolvePath(fullPath).ToList();
        fullPath = FilterFullPath(fullPath);

        if (gamePaths.Count > 1)
            gamePaths = Filter(gamePaths);

        if (gamePaths.Count == 1)
            return new ResourceNode(withName ? GuessNameFromPath(gamePaths[0]) : null, type, sourceAddress, gamePaths[0], fullPath, @internal);

        Penumbra.Log.Information($"Found {gamePaths.Count} game paths while reverse-resolving {fullPath} in {Collection.Name}:");
        foreach (var gamePath in gamePaths)
            Penumbra.Log.Information($"Game path: {gamePath}");

        return new ResourceNode(null, type, sourceAddress, gamePaths.ToArray(), fullPath, @internal);
    }
    public unsafe ResourceNode? CreateHumanSkeletonNode(GenderRace gr)
    {
        var raceSexIdStr = gr.ToRaceCode();
        var path         = $"chara/human/c{raceSexIdStr}/skeleton/base/b0001/skl_c{raceSexIdStr}b0001.sklb";

        if (!Utf8GamePath.FromString(path, out var gamePath))
            return null;

        return CreateNodeFromGamePath(ResourceType.Sklb, 0, gamePath, false);
    }

    public unsafe ResourceNode? CreateNodeFromImc(ResourceHandle* imc)
    {
        var node = CreateNodeFromResourceHandle(ResourceType.Imc, (nint) imc, imc, true, false);
        if (node == null)
            return null;

        if (WithNames)
        {
            var name = GuessModelName(node.GamePath);
            node = node.WithName(name != null ? $"IMC: {name}" : null);
        }

        return node;
    }

    public unsafe ResourceNode? CreateNodeFromTex(ResourceHandle* tex)
        => CreateNodeFromResourceHandle(ResourceType.Tex, (nint) tex, tex, false, WithNames);

    public unsafe ResourceNode? CreateNodeFromRenderModel(RenderModel* mdl)
    {
        if (mdl == null || mdl->ResourceHandle == null || mdl->ResourceHandle->Category != ResourceCategory.Chara)
            return null;

        var node = CreateNodeFromResourceHandle(ResourceType.Mdl, (nint) mdl, mdl->ResourceHandle, false, false);
        if (node == null)
            return null;

        if (WithNames)
            node = node.WithName(GuessModelName(node.GamePath));

        for (var i = 0; i < mdl->MaterialCount; i++)
        {
            var mtrl     = (Material*)mdl->Materials[i];
            var mtrlNode = CreateNodeFromMaterial(mtrl);
            if (mtrlNode != null)
                // Don't keep the material's name if it's redundant with the model's name.
                node.Children.Add(WithNames
                    ? mtrlNode.WithName((string.Equals(mtrlNode.Name, node.Name, StringComparison.Ordinal) ? null : mtrlNode.Name)
                     ?? $"Material #{i}")
                    : mtrlNode);
        }

        return node;
    }

    private unsafe ResourceNode? CreateNodeFromMaterial(Material* mtrl)
    {
        if (mtrl == null)
            return null;

        var resource = (MtrlResource*)mtrl->ResourceHandle;
        var node     = CreateNodeFromResourceHandle(ResourceType.Mtrl, (nint) mtrl, &resource->Handle, false, WithNames);
        if (node == null)
            return null;

        var mtrlFile = WithNames ? TreeBuildCache.ReadMaterial(node.FullPath) : null;

        var shpkNode = CreateNodeFromShpk(nint.Zero, new ByteString(resource->ShpkString), false);
        if (shpkNode != null)
            node.Children.Add(WithNames ? shpkNode.WithName("Shader Package") : shpkNode);
        var shpkFile = WithNames && shpkNode != null ? TreeBuildCache.ReadShaderPackage(shpkNode.FullPath) : null;
        var samplers = WithNames ? mtrlFile?.GetSamplersByTexture(shpkFile) : null;

        for (var i = 0; i < resource->NumTex; i++)
        {
            var texNode = CreateNodeFromTex(nint.Zero, new ByteString(resource->TexString(i)), false, resource->TexIsDX11(i));
            if (texNode == null)
                continue;

            if (WithNames)
            {
                var name = samplers != null && i < samplers.Count ? samplers[i].Item2?.Name : null;
                node.Children.Add(texNode.WithName(name ?? $"Texture #{i}"));
            }
            else
            {
                node.Children.Add(texNode);
            }
        }

        return node;
    }

    private FullPath FilterFullPath(FullPath fullPath)
    {
        if (!fullPath.IsRooted)
            return fullPath;

        var relPath = Path.GetRelativePath(Config.ModDirectory, fullPath.FullName);
        if (relPath == "." || !relPath.StartsWith('.') && !Path.IsPathRooted(relPath))
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
                "accessory" => IsMatchEquipment(path[2..], $"a{Equipment.Set.Value:D4}"),
                "equipment" => IsMatchEquipment(path[2..], $"e{Equipment.Set.Value:D4}"),
                "monster"   => SafeGet(path, 2) == $"m{Skeleton:D4}",
                "weapon"    => IsMatchEquipment(path[2..], $"w{Equipment.Set.Value:D4}"),
                _           => null,
            },
            _ => null,
        };

    private bool? IsMatchEquipment(ReadOnlySpan<string> path, string equipmentDir)
        => SafeGet(path, 0) == equipmentDir
            ? SafeGet(path, 1) switch
            {
                "material" => SafeGet(path, 2) == $"v{Equipment.Variant:D4}",
                _          => null,
            }
            : false;

    private string? GuessModelName(Utf8GamePath gamePath)
    {
        var path = gamePath.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries);
        // Weapons intentionally left out.
        var isEquipment = SafeGet(path, 0) == "chara" && SafeGet(path, 1) is "accessory" or "equipment";
        if (isEquipment)
            foreach (var item in Identifier.Identify(Equipment.Set, Equipment.Variant, Slot.ToSlot()))
            {
                return Slot switch
                    {
                        EquipSlot.RFinger => "R: ",
                        EquipSlot.LFinger => "L: ",
                        _                 => string.Empty,
                    }
                  + item.Name.ToString();
            }

        var nameFromPath = GuessNameFromPath(gamePath);
        if (nameFromPath != null)
            return nameFromPath;

        return isEquipment ? Slot.ToName() : null;
    }

    private string? GuessNameFromPath(Utf8GamePath gamePath)
    {
        foreach (var obj in Identifier.Identify(gamePath.ToString()))
        {
            var name = obj.Key;
            if (name.StartsWith("Customization:"))
                name = name[14..].Trim();
            if (name != "Unknown")
                return name;
        }

        return null;
    }

    private static string? SafeGet(ReadOnlySpan<string> array, Index index)
    {
        var i = index.GetOffset(array.Length);
        return i >= 0 && i < array.Length ? array[i] : null;
    }
}
