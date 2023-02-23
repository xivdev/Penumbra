using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumina.Extensions;
using Penumbra.GameData.Data;

namespace Penumbra.GameData.Files;

public partial class ShpkFile : IWritable
{
    private const uint ShPkMagic = 0x6B506853u; // bytes of ShPk
    private const uint Dx9Magic  = 0x00395844u; // bytes of DX9\0
    private const uint Dx11Magic = 0x31315844u; // bytes of DX11

    public const uint MaterialParamsConstantId = 0x64D12851u;

    public uint            Version;
    public DxVersion       DirectXVersion;
    public Shader[]        VertexShaders;
    public Shader[]        PixelShaders;
    public uint            MaterialParamsSize;
    public MaterialParam[] MaterialParams;
    public Resource[]      Constants;
    public Resource[]      Samplers;
    public Resource[]      Uavs;
    public Key[]           SystemKeys;
    public Key[]           SceneKeys;
    public Key[]           MaterialKeys;
    public Key[]           SubViewKeys;
    public Node[]          Nodes;
    public Item[]          Items;
    public byte[]          AdditionalData;

    public  bool Valid { get; private set; }
    private bool _changed;

    public MaterialParam? GetMaterialParamById(uint id)
        => MaterialParams.FirstOrNull(m => m.Id == id);

    public Resource? GetConstantById(uint id)
        => Constants.FirstOrNull(c => c.Id == id);

    public Resource? GetConstantByName(string name)
        => Constants.FirstOrNull(c => c.Name == name);

    public Resource? GetSamplerById(uint id)
        => Samplers.FirstOrNull(s => s.Id == id);

    public Resource? GetSamplerByName(string name)
        => Samplers.FirstOrNull(s => s.Name == name);

    public Resource? GetUavById(uint id)
        => Uavs.FirstOrNull(u => u.Id == id);

    public Resource? GetUavByName(string name)
        => Uavs.FirstOrNull(u => u.Name == name);

    public Key? GetSystemKeyById(uint id)
        => SystemKeys.FirstOrNull(k => k.Id == id);

    public Key? GetSceneKeyById(uint id)
        => SceneKeys.FirstOrNull(k => k.Id == id);

    public Key? GetMaterialKeyById(uint id)
        => MaterialKeys.FirstOrNull(k => k.Id == id);

    public Node? GetNodeById(uint id)
        => Nodes.FirstOrNull(n => n.Id == id);

    public Item? GetItemById(uint id)
        => Items.FirstOrNull(i => i.Id == id);

    public ShpkFile(byte[] data, bool disassemble = false)
    {
        using var stream = new MemoryStream(data);
        using var r      = new BinaryReader(stream);

        if (r.ReadUInt32() != ShPkMagic)
            throw new InvalidDataException();

        Version = r.ReadUInt32();
        DirectXVersion = r.ReadUInt32() switch
        {
            Dx9Magic  => DxVersion.DirectX9,
            Dx11Magic => DxVersion.DirectX11,
            _         => throw new InvalidDataException(),
        };
        if (r.ReadUInt32() != data.Length)
            throw new InvalidDataException();

        var blobsOffset       = r.ReadUInt32();
        var stringsOffset     = r.ReadUInt32();
        var vertexShaderCount = r.ReadUInt32();
        var pixelShaderCount  = r.ReadUInt32();
        MaterialParamsSize = r.ReadUInt32();
        var materialParamCount = r.ReadUInt32();
        var constantCount      = r.ReadUInt32();
        var samplerCount       = r.ReadUInt32();
        var uavCount           = r.ReadUInt32();
        var systemKeyCount     = r.ReadUInt32();
        var sceneKeyCount      = r.ReadUInt32();
        var materialKeyCount   = r.ReadUInt32();
        var nodeCount          = r.ReadUInt32();
        var itemCount          = r.ReadUInt32();

        var blobs   = new ReadOnlySpan<byte>(data, (int)blobsOffset, (int)(stringsOffset - blobsOffset));
        var strings = new StringPool(new ReadOnlySpan<byte>(data, (int)stringsOffset, (int)(data.Length - stringsOffset)));

        VertexShaders = ReadShaderArray(r, (int)vertexShaderCount, DisassembledShader.ShaderStage.Vertex, DirectXVersion, disassemble, blobs,
            strings);
        PixelShaders = ReadShaderArray(r, (int)pixelShaderCount, DisassembledShader.ShaderStage.Pixel, DirectXVersion, disassemble, blobs,
            strings);

        MaterialParams = r.ReadStructuresAsArray<MaterialParam>((int)materialParamCount);

        Constants = ReadResourceArray(r, (int)constantCount, strings);
        Samplers  = ReadResourceArray(r, (int)samplerCount,  strings);
        Uavs      = ReadResourceArray(r, (int)uavCount,      strings);

        SystemKeys   = ReadKeyArray(r, (int)systemKeyCount);
        SceneKeys    = ReadKeyArray(r, (int)sceneKeyCount);
        MaterialKeys = ReadKeyArray(r, (int)materialKeyCount);

        var subViewKey1Default = r.ReadUInt32();
        var subViewKey2Default = r.ReadUInt32();

        SubViewKeys = new Key[]
        {
            new()
            {
                Id        = 1,
                DefaultValue = subViewKey1Default,
                Values    = Array.Empty<uint>(),
            },
            new()
            {
                Id        = 2,
                DefaultValue = subViewKey2Default,
                Values    = Array.Empty<uint>(),
            },
        };

        Nodes = ReadNodeArray(r, (int)nodeCount, SystemKeys.Length, SceneKeys.Length, MaterialKeys.Length, SubViewKeys.Length);
        Items = r.ReadStructuresAsArray<Item>((int)itemCount);

        AdditionalData = r.ReadBytes((int)(blobsOffset - r.BaseStream.Position)); // This should be empty, but just in case.

        if (disassemble)
            UpdateUsed();

        UpdateKeyValues();

        Valid    = true;
        _changed = false;
    }

    public void UpdateResources()
    {
        var constants = new Dictionary<uint, Resource>();
        var samplers  = new Dictionary<uint, Resource>();
        var uavs      = new Dictionary<uint, Resource>();

        static void CollectResources(Dictionary<uint, Resource> resources, Resource[] shaderResources, Func<uint, Resource?> getExistingById,
            DisassembledShader.ResourceType type)
        {
            foreach (var resource in shaderResources)
            {
                if (resources.TryGetValue(resource.Id, out var carry) && type != DisassembledShader.ResourceType.ConstantBuffer)
                    continue;

                var existing = getExistingById(resource.Id);
                resources[resource.Id] = new Resource
                {
                    Id = resource.Id,
                    Name = resource.Name,
                    Slot = existing?.Slot ?? (type == DisassembledShader.ResourceType.ConstantBuffer ? (ushort)65535 : (ushort)2),
                    Size = type == DisassembledShader.ResourceType.ConstantBuffer ? Math.Max(carry.Size, resource.Size) : existing?.Size ?? 0,
                    Used = null,
                    UsedDynamically = null,
                };
            }
        }

        foreach (var shader in VertexShaders)
        {
            CollectResources(constants, shader.Constants, GetConstantById, DisassembledShader.ResourceType.ConstantBuffer);
            CollectResources(samplers,  shader.Samplers,  GetSamplerById,  DisassembledShader.ResourceType.Sampler);
            CollectResources(uavs,      shader.Uavs,      GetUavById,      DisassembledShader.ResourceType.Uav);
        }

        foreach (var shader in PixelShaders)
        {
            CollectResources(constants, shader.Constants, GetConstantById, DisassembledShader.ResourceType.ConstantBuffer);
            CollectResources(samplers,  shader.Samplers,  GetSamplerById,  DisassembledShader.ResourceType.Sampler);
            CollectResources(uavs,      shader.Uavs,      GetUavById,      DisassembledShader.ResourceType.Uav);
        }

        Constants = constants.Values.ToArray();
        Samplers  = samplers.Values.ToArray();
        Uavs      = uavs.Values.ToArray();
        UpdateUsed();

        // Ceil required size to a multiple of 16 bytes.
        // Offsets can be skipped, MaterialParamsConstantId's size is the count.
        MaterialParamsSize = (GetConstantById(MaterialParamsConstantId)?.Size ?? 0u) << 4;
        foreach (var param in MaterialParams)
            MaterialParamsSize = Math.Max(MaterialParamsSize, (uint)param.ByteOffset + param.ByteSize);
        MaterialParamsSize = (MaterialParamsSize + 0xFu) & ~0xFu;
    }

    private void UpdateUsed()
    {
        var cUsage = new Dictionary<uint, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();
        var sUsage = new Dictionary<uint, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();
        var uUsage = new Dictionary<uint, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();

        static void CollectUsed(Dictionary<uint, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)> usage,
            Resource[] resources)
        {
            foreach (var resource in resources)
            {
                if (resource.Used == null)
                    continue;

                usage.TryGetValue(resource.Id, out var carry);
                carry.Item1 ??= Array.Empty<DisassembledShader.VectorComponents>();
                var combined = new DisassembledShader.VectorComponents[Math.Max(carry.Item1.Length, resource.Used.Length)];
                for (var i = 0; i < combined.Length; ++i)
                    combined[i] = (i < carry.Item1.Length ? carry.Item1[i] : 0) | (i < resource.Used.Length ? resource.Used[i] : 0);
                usage[resource.Id] = (combined, carry.Item2 | (resource.UsedDynamically ?? 0));
            }
        }

        static void CopyUsed(Resource[] resources,
            Dictionary<uint, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)> used)
        {
            for (var i = 0; i < resources.Length; ++i)
            {
                if (used.TryGetValue(resources[i].Id, out var usage))
                {
                    resources[i].Used            = usage.Item1;
                    resources[i].UsedDynamically = usage.Item2;
                }
                else
                {
                    resources[i].Used            = null;
                    resources[i].UsedDynamically = null;
                }
            }
        }

        foreach (var shader in VertexShaders)
        {
            CollectUsed(cUsage, shader.Constants);
            CollectUsed(sUsage, shader.Samplers);
            CollectUsed(uUsage, shader.Uavs);
        }

        foreach (var shader in PixelShaders)
        {
            CollectUsed(cUsage, shader.Constants);
            CollectUsed(sUsage, shader.Samplers);
            CollectUsed(uUsage, shader.Uavs);
        }

        CopyUsed(Constants, cUsage);
        CopyUsed(Samplers,  sUsage);
        CopyUsed(Uavs,      uUsage);
    }

    public void UpdateKeyValues()
    {
        static HashSet<uint>[] InitializeValueSet(Key[] keys)
            => Array.ConvertAll(keys, key => new HashSet<uint>()
            {
                key.DefaultValue,
            });

        static void CollectValues(HashSet<uint>[] valueSets, uint[] values)
        {
            for (var i = 0; i < valueSets.Length; ++i)
                valueSets[i].Add(values[i]);
        }

        static void CopyValues(Key[] keys, HashSet<uint>[] valueSets)
        {
            for (var i = 0; i < keys.Length; ++i)
                keys[i].Values = valueSets[i].ToArray();
        }

        var systemKeyValues   = InitializeValueSet(SystemKeys);
        var sceneKeyValues    = InitializeValueSet(SceneKeys);
        var materialKeyValues = InitializeValueSet(MaterialKeys);
        var subViewKeyValues  = InitializeValueSet(SubViewKeys);
        foreach (var node in Nodes)
        {
            CollectValues(systemKeyValues,   node.SystemKeys);
            CollectValues(sceneKeyValues,    node.SceneKeys);
            CollectValues(materialKeyValues, node.MaterialKeys);
            CollectValues(subViewKeyValues,  node.SubViewKeys);
        }

        CopyValues(SystemKeys,   systemKeyValues);
        CopyValues(SceneKeys,    sceneKeyValues);
        CopyValues(MaterialKeys, materialKeyValues);
        CopyValues(SubViewKeys,  subViewKeyValues);
    }

    public void SetInvalid()
        => Valid = false;

    public void SetChanged()
        => _changed = true;

    public bool IsChanged()
    {
        var changed = _changed;
        _changed = false;
        return changed;
    }

    private static void ClearUsed(Resource[] resources)
    {
        for (var i = 0; i < resources.Length; ++i)
        {
            resources[i].Used            = null;
            resources[i].UsedDynamically = null;
        }
    }

    private static Resource[] ReadResourceArray(BinaryReader r, int count, StringPool strings)
    {
        var ret = new Resource[count];
        for (var i = 0; i < count; ++i)
        {
            var id        = r.ReadUInt32();
            var strOffset = r.ReadUInt32();
            var strSize   = r.ReadUInt32();
            ret[i] = new Resource
            {
                Id   = id,
                Name = strings.GetString((int)strOffset, (int)strSize),
                Slot = r.ReadUInt16(),
                Size = r.ReadUInt16(),
            };
        }

        return ret;
    }

    private static Shader[] ReadShaderArray(BinaryReader r, int count, DisassembledShader.ShaderStage stage, DxVersion directX,
        bool disassemble, ReadOnlySpan<byte> blobs, StringPool strings)
    {
        var extraHeaderSize = stage switch
        {
            DisassembledShader.ShaderStage.Vertex => directX switch
            {
                DxVersion.DirectX9  => 4,
                DxVersion.DirectX11 => 8,
                _                   => throw new NotImplementedException(),
            },
            _ => 0,
        };

        var ret = new Shader[count];
        for (var i = 0; i < count; ++i)
        {
            var blobOffset    = r.ReadUInt32();
            var blobSize      = r.ReadUInt32();
            var constantCount = r.ReadUInt16();
            var samplerCount  = r.ReadUInt16();
            var uavCount      = r.ReadUInt16();
            if (r.ReadUInt16() != 0)
                throw new NotImplementedException();

            var rawBlob = blobs.Slice((int)blobOffset, (int)blobSize);

            ret[i] = new Shader
            {
                Stage            = disassemble ? stage : DisassembledShader.ShaderStage.Unspecified,
                DirectXVersion   = directX,
                Constants        = ReadResourceArray(r, constantCount, strings),
                Samplers         = ReadResourceArray(r, samplerCount,  strings),
                Uavs             = ReadResourceArray(r, uavCount,      strings),
                AdditionalHeader = rawBlob[..extraHeaderSize].ToArray(),
                Blob             = rawBlob[extraHeaderSize..].ToArray(),
            };
        }

        return ret;
    }

    private static Key[] ReadKeyArray(BinaryReader r, int count)
    {
        var ret = new Key[count];
        for (var i = 0; i < count; ++i)
        {
            ret[i] = new Key
            {
                Id        = r.ReadUInt32(),
                DefaultValue = r.ReadUInt32(),
                Values    = Array.Empty<uint>(),
            };
        }

        return ret;
    }

    private static Node[] ReadNodeArray(BinaryReader r, int count, int systemKeyCount, int sceneKeyCount, int materialKeyCount,
        int subViewKeyCount)
    {
        var ret = new Node[count];
        for (var i = 0; i < count; ++i)
        {
            var id        = r.ReadUInt32();
            var passCount = r.ReadUInt32();
            ret[i] = new Node
            {
                Id           = id,
                PassIndices  = r.ReadBytes(16),
                SystemKeys   = r.ReadStructuresAsArray<uint>(systemKeyCount),
                SceneKeys    = r.ReadStructuresAsArray<uint>(sceneKeyCount),
                MaterialKeys = r.ReadStructuresAsArray<uint>(materialKeyCount),
                SubViewKeys  = r.ReadStructuresAsArray<uint>(subViewKeyCount),
                Passes       = r.ReadStructuresAsArray<Pass>((int)passCount),
            };
        }

        return ret;
    }

    public enum DxVersion : uint
    {
        DirectX9  = 9,
        DirectX11 = 11,
    }

    public struct Resource
    {
        public uint                                   Id;
        public string                                 Name;
        public ushort                                 Slot;
        public ushort                                 Size;
        public DisassembledShader.VectorComponents[]? Used;
        public DisassembledShader.VectorComponents?   UsedDynamically;
    }

    public struct MaterialParam
    {
        public uint   Id;
        public ushort ByteOffset;
        public ushort ByteSize;
    }

    public struct Pass
    {
        public uint Id;
        public uint VertexShader;
        public uint PixelShader;
    }

    public struct Key
    {
        public uint   Id;
        public uint   DefaultValue;
        public uint[] Values;
    }

    public struct Node
    {
        public uint   Id;
        public byte[] PassIndices;
        public uint[] SystemKeys;
        public uint[] SceneKeys;
        public uint[] MaterialKeys;
        public uint[] SubViewKeys;
        public Pass[] Passes;
    }

    public struct Item
    {
        public uint Id;
        public uint Node;
    }
}
