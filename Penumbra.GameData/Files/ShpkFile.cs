using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lumina.Data.Parsing;
using Lumina.Extensions;
using Lumina.Misc;
using Penumbra.GameData.Data;

namespace Penumbra.GameData.Files;

public partial class ShpkFile : IWritable
{
    public enum DXVersion : uint
    {
        DirectX9  = 9,
        DirectX11 = 11,
    }

    public struct Resource
    {
        public uint Id;
        public string Name;
        public ushort Slot;
        public ushort Size;
        public DisassembledShader.VectorComponents[]? Used;
        public DisassembledShader.VectorComponents? UsedDynamically;
    }

    public struct Shader
    {
        public DisassembledShader.ShaderStage Stage;
        public DXVersion DirectXVersion;
        public Resource[] Constants;
        public Resource[] Samplers;
        public Resource[] UAVs;
        public byte[] AdditionalHeader;
        private byte[] _blob;
        private DisassembledShader? _disassembly;

        public byte[] Blob
        {
            get => _blob;
            set
            {
                if (_blob == value)
                {
                    return;
                }
                if (Stage != DisassembledShader.ShaderStage.Unspecified)
                {
                    // Reject the blob entirely if we can't disassemble it or if we find inconsistencies.
                    var disasm = DisassembledShader.Disassemble(value);
                    if (disasm.Stage != Stage || (disasm.ShaderModel >> 8) + 6 != (uint)DirectXVersion)
                    {
                        throw new ArgumentException($"The supplied blob is a DirectX {(disasm.ShaderModel >> 8) + 6} {disasm.Stage} shader ; expected a DirectX {(uint)DirectXVersion} {Stage} shader.", nameof(value));
                    }
                    if (disasm.ShaderModel >= 0x0500)
                    {
                        var samplers = new Dictionary<uint, string>();
                        var textures = new Dictionary<uint, string>();
                        foreach (var binding in disasm.ResourceBindings)
                        {
                            switch (binding.Type)
                            {
                                case DisassembledShader.ResourceType.Texture:
                                    textures[binding.Slot] = NormalizeResourceName(binding.Name);
                                    break;
                                case DisassembledShader.ResourceType.Sampler:
                                    samplers[binding.Slot] = NormalizeResourceName(binding.Name);
                                    break;
                            }
                        }
                        if (samplers.Count != textures.Count || !samplers.All(pair => textures.TryGetValue(pair.Key, out var texName) && pair.Value == texName))
                        {
                            throw new ArgumentException($"The supplied blob has inconsistent sampler and texture allocation.");
                        }
                    }
                    _blob = value;
                    _disassembly = disasm;
                }
                else
                {
                    _blob = value;
                    _disassembly = null;
                }
                UpdateUsed();
            }
        }

        public DisassembledShader? Disassembly => _disassembly;

        public Resource? GetConstantById(uint id)
        {
            return Constants.Select(res => new Resource?(res)).FirstOrDefault(res => res!.Value.Id == id);
        }

        public Resource? GetConstantByName(string name)
        {
            return Constants.Select(res => new Resource?(res)).FirstOrDefault(res => res!.Value.Name == name);
        }

        public Resource? GetSamplerById(uint id)
        {
            return Samplers.Select(res => new Resource?(res)).FirstOrDefault(res => res!.Value.Id == id);
        }

        public Resource? GetSamplerByName(string name)
        {
            return Samplers.Select(res => new Resource?(res)).FirstOrDefault(res => res!.Value.Name == name);
        }

        public Resource? GetUAVById(uint id)
        {
            return UAVs.Select(res => new Resource?(res)).FirstOrDefault(res => res!.Value.Id == id);
        }

        public Resource? GetUAVByName(string name)
        {
            return UAVs.Select(res => new Resource?(res)).FirstOrDefault(res => res!.Value.Name == name);
        }

        public void UpdateResources(ShpkFile file)
        {
            if (_disassembly == null)
            {
                throw new InvalidOperationException();
            }
            var constants = new List<Resource>();
            var samplers = new List<Resource>();
            var uavs = new List<Resource>();
            foreach (var binding in _disassembly.ResourceBindings)
            {
                switch (binding.Type)
                {
                    case DisassembledShader.ResourceType.ConstantBuffer:
                        var name = NormalizeResourceName(binding.Name);
                        // We want to preserve IDs as much as possible, and to deterministically generate new ones in a way that's most compliant with the native ones, to maximize compatibility.
                        var id = GetConstantByName(name)?.Id ?? file.GetConstantByName(name)?.Id ?? Crc32.Get(name, 0xFFFFFFFFu);
                        constants.Add(new Resource
                        {
                            Id              = id,
                            Name            = name,
                            Slot            = (ushort)binding.Slot,
                            Size            = (ushort)binding.RegisterCount,
                            Used            = binding.Used,
                            UsedDynamically = binding.UsedDynamically,
                        });
                        break;
                    case DisassembledShader.ResourceType.Texture:
                        name = NormalizeResourceName(binding.Name);
                        id = GetSamplerByName(name)?.Id ?? file.GetSamplerByName(name)?.Id ?? Crc32.Get(name, 0xFFFFFFFFu);
                        samplers.Add(new Resource
                        {
                            Id              = id,
                            Name            = name,
                            Slot            = (ushort)binding.Slot,
                            Size            = (ushort)binding.Slot,
                            Used            = binding.Used,
                            UsedDynamically = binding.UsedDynamically,
                        });
                        break;
                    case DisassembledShader.ResourceType.UAV:
                        name = NormalizeResourceName(binding.Name);
                        id = GetUAVByName(name)?.Id ?? file.GetUAVByName(name)?.Id ?? Crc32.Get(name, 0xFFFFFFFFu);
                        uavs.Add(new Resource
                        {
                            Id              = id,
                            Name            = name,
                            Slot            = (ushort)binding.Slot,
                            Size            = (ushort)binding.Slot,
                            Used            = binding.Used,
                            UsedDynamically = binding.UsedDynamically,
                        });
                        break;
                }
            }
            Constants = constants.ToArray();
            Samplers = samplers.ToArray();
            UAVs = uavs.ToArray();
        }

        private void UpdateUsed()
        {
            if (_disassembly != null)
            {
                var cbUsage = new Dictionary<string, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();
                var tUsage = new Dictionary<string, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();
                var uUsage = new Dictionary<string, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();
                foreach (var binding in _disassembly.ResourceBindings)
                {
                    switch (binding.Type)
                    {
                        case DisassembledShader.ResourceType.ConstantBuffer:
                            cbUsage[NormalizeResourceName(binding.Name)] = (binding.Used, binding.UsedDynamically);
                            break;
                        case DisassembledShader.ResourceType.Texture:
                            tUsage[NormalizeResourceName(binding.Name)] = (binding.Used, binding.UsedDynamically);
                            break;
                        case DisassembledShader.ResourceType.UAV:
                            uUsage[NormalizeResourceName(binding.Name)] = (binding.Used, binding.UsedDynamically);
                            break;
                    }
                }
                static void CopyUsed(Resource[] resources, Dictionary<string, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)> used)
                {
                    for (var i = 0; i < resources.Length; ++i)
                    {
                        if (used.TryGetValue(resources[i].Name, out var usage))
                        {
                            resources[i].Used = usage.Item1;
                            resources[i].UsedDynamically = usage.Item2;
                        }
                        else
                        {
                            resources[i].Used = null;
                            resources[i].UsedDynamically = null;
                        }
                    }
                }
                CopyUsed(Constants, cbUsage);
                CopyUsed(Samplers, tUsage);
                CopyUsed(UAVs, uUsage);
            }
            else
            {
                ClearUsed(Constants);
                ClearUsed(Samplers);
                ClearUsed(UAVs);
            }
        }

        private static string NormalizeResourceName(string resourceName)
        {
            var dot = resourceName.IndexOf('.');
            if (dot >= 0)
            {
                return resourceName[..dot];
            }
            else if (resourceName.EndsWith("_S") || resourceName.EndsWith("_T"))
            {
                return resourceName[..^2];
            }
            else
            {
                return resourceName;
            }
        }
    }

    public struct MaterialParam
    {
        public uint Id;
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
        public uint Id;
        public uint DefaultValue;
        public uint[] Values;
    }

    public struct Node
    {
        public uint Id;
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

    public class StringPool
    {
        public MemoryStream Data;
        public List<int> StartingOffsets;

        public StringPool(ReadOnlySpan<byte> bytes)
        {
            Data = new MemoryStream();
            Data.Write(bytes);
            StartingOffsets = new List<int>
            {
                0,
            };
            for (var i = 0; i < bytes.Length; ++i)
            {
                if (bytes[i] == 0)
                {
                    StartingOffsets.Add(i + 1);
                }
            }
            if (StartingOffsets[^1] == bytes.Length)
            {
                StartingOffsets.RemoveAt(StartingOffsets.Count - 1);
            }
            else
            {
                Data.WriteByte(0);
            }
        }

        public string GetString(int offset, int size)
        {
            return Encoding.UTF8.GetString(Data.GetBuffer().AsSpan().Slice(offset, size));
        }

        public string GetNullTerminatedString(int offset)
        {
            var str = Data.GetBuffer().AsSpan()[offset..];
            var size = str.IndexOf((byte)0);
            if (size >= 0)
            {
                str = str[..size];
            }
            return Encoding.UTF8.GetString(str);
        }

        public (int, int) FindOrAddString(string str)
        {
            var dataSpan = Data.GetBuffer().AsSpan();
            var bytes = Encoding.UTF8.GetBytes(str);
            foreach (var offset in StartingOffsets)
            {
                if (offset + bytes.Length > Data.Length)
                {
                    break;
                }
                var strSpan = dataSpan[offset..];
                var match = true;
                for (var i = 0; i < bytes.Length; ++i)
                {
                    if (strSpan[i] != bytes[i])
                    {
                        match = false;
                        break;
                    }
                }
                if (match && strSpan[bytes.Length] == 0)
                {
                    return (offset, bytes.Length);
                }
            }
            Data.Seek(0L, SeekOrigin.End);
            var newOffset = (int)Data.Position;
            StartingOffsets.Add(newOffset);
            Data.Write(bytes);
            Data.WriteByte(0);
            return (newOffset, bytes.Length);
        }
    }

    private const uint ShPkMagic = 0x6B506853u; // bytes of ShPk
    private const uint DX9Magic  = 0x00395844u; // bytes of DX9\0
    private const uint DX11Magic = 0x31315844u; // bytes of DX11

    public const uint MaterialParamsConstantId = 0x64D12851u;

    public uint Version;
    public DXVersion DirectXVersion;
    public Shader[] VertexShaders;
    public Shader[] PixelShaders;
    public uint MaterialParamsSize;
    public MaterialParam[] MaterialParams;
    public Resource[] Constants;
    public Resource[] Samplers;
    public Resource[] UAVs;
    public Key[] SystemKeys;
    public Key[] SceneKeys;
    public Key[] MaterialKeys;
    public Key[] SubViewKeys;
    public Node[] Nodes;
    public Item[] Items;
    public byte[] AdditionalData;

    public bool Valid { get; private set; }
    private bool _changed;

    public MaterialParam? GetMaterialParamById(uint id)
    {
        return MaterialParams.Select(param => new MaterialParam?(param)).FirstOrDefault(param => param!.Value.Id == id);
    }

    public Resource? GetConstantById(uint id)
    {
        return Constants.Select(res => new Resource?(res)).FirstOrDefault(res => res!.Value.Id == id);
    }

    public Resource? GetConstantByName(string name)
    {
        return Constants.Select(res => new Resource?(res)).FirstOrDefault(res => res!.Value.Name == name);
    }

    public Resource? GetSamplerById(uint id)
    {
        return Samplers.Select(res => new Resource?(res)).FirstOrDefault(res => res!.Value.Id == id);
    }

    public Resource? GetSamplerByName(string name)
    {
        return Samplers.Select(res => new Resource?(res)).FirstOrDefault(res => res!.Value.Name == name);
    }

    public Resource? GetUAVById(uint id)
    {
        return UAVs.Select(res => new Resource?(res)).FirstOrDefault(res => res!.Value.Id == id);
    }

    public Resource? GetUAVByName(string name)
    {
        return UAVs.Select(res => new Resource?(res)).FirstOrDefault(res => res!.Value.Name == name);
    }

    public Key? GetSystemKeyById(uint id)
    {
        return SystemKeys.Select(key => new Key?(key)).FirstOrDefault(key => key!.Value.Id == id);
    }

    public Key? GetSceneKeyById(uint id)
    {
        return SceneKeys.Select(key => new Key?(key)).FirstOrDefault(key => key!.Value.Id == id);
    }

    public Key? GetMaterialKeyById(uint id)
    {
        return MaterialKeys.Select(key => new Key?(key)).FirstOrDefault(key => key!.Value.Id == id);
    }

    public Node? GetNodeById(uint id)
    {
        return Nodes.Select(node => new Node?(node)).FirstOrDefault(node => node!.Value.Id == id);
    }

    public Item? GetItemById(uint id)
    {
        return Items.Select(item => new Item?(item)).FirstOrDefault(item => item!.Value.Id == id);
    }

    // Activator.CreateInstance can't use a ctor with a default value so this has to be made explicit
    public ShpkFile(byte[] data)
        : this(data, false)
    {
    }

    public ShpkFile(byte[] data, bool disassemble = false)
    {
        using var stream = new MemoryStream(data);
        using var r      = new BinaryReader(stream);

        if (r.ReadUInt32() != ShPkMagic)
        {
            throw new InvalidDataException();
        }
        Version = r.ReadUInt32();
        DirectXVersion = r.ReadUInt32() switch
        {
            DX9Magic  => DXVersion.DirectX9,
            DX11Magic => DXVersion.DirectX11,
            _         => throw new InvalidDataException(),
        };
        if (r.ReadUInt32() != data.Length)
        {
            throw new InvalidDataException();
        }
        var blobsOffset        = r.ReadUInt32();
        var stringsOffset      = r.ReadUInt32();
        var vertexShaderCount  = r.ReadUInt32();
        var pixelShaderCount   = r.ReadUInt32();
        MaterialParamsSize     = r.ReadUInt32();
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

        VertexShaders  = ReadShaderArray(r, (int)vertexShaderCount, DisassembledShader.ShaderStage.Vertex, DirectXVersion, disassemble, blobs, strings);
        PixelShaders   = ReadShaderArray(r, (int)pixelShaderCount, DisassembledShader.ShaderStage.Pixel, DirectXVersion, disassemble, blobs, strings);

        MaterialParams = r.ReadStructuresAsArray<MaterialParam>((int)materialParamCount);

        Constants      = ReadResourceArray(r, (int)constantCount, strings);
        Samplers       = ReadResourceArray(r, (int)samplerCount, strings);
        UAVs           = ReadResourceArray(r, (int)uavCount, strings);

        SystemKeys     = ReadKeyArray(r, (int)systemKeyCount);
        SceneKeys      = ReadKeyArray(r, (int)sceneKeyCount);
        MaterialKeys   = ReadKeyArray(r, (int)materialKeyCount);

        var subViewKey1Default = r.ReadUInt32();
        var subViewKey2Default = r.ReadUInt32();

        SubViewKeys    = new Key[] {
            new Key
            {
                Id           = 1,
                DefaultValue = subViewKey1Default,
                Values       = Array.Empty<uint>(),
            },
            new Key
            {
                Id           = 2,
                DefaultValue = subViewKey2Default,
                Values       = Array.Empty<uint>(),
            },
        };

        Nodes          = ReadNodeArray(r, (int)nodeCount, SystemKeys.Length, SceneKeys.Length, MaterialKeys.Length, SubViewKeys.Length);
        Items          = r.ReadStructuresAsArray<Item>((int)itemCount);

        AdditionalData = r.ReadBytes((int)(blobsOffset - r.BaseStream.Position)); // This should be empty, but just in case.

        if (disassemble)
        {
            UpdateUsed();
        }

        UpdateKeyValues();

        Valid = true;
        _changed = false;
    }

    public void UpdateResources()
    {
        var constants = new Dictionary<uint, Resource>();
        var samplers = new Dictionary<uint, Resource>();
        var uavs = new Dictionary<uint, Resource>();
        static void CollectResources(Dictionary<uint, Resource> resources, Resource[] shaderResources, Func<uint, Resource?> getExistingById, DisassembledShader.ResourceType type)
        {
            foreach (var resource in shaderResources)
            {
                if (resources.TryGetValue(resource.Id, out var carry) && type != DisassembledShader.ResourceType.ConstantBuffer)
                {
                    continue;
                }
                var existing = getExistingById(resource.Id);
                resources[resource.Id] = new Resource
                {
                    Id              = resource.Id,
                    Name            = resource.Name,
                    Slot            = existing?.Slot ?? (type == DisassembledShader.ResourceType.ConstantBuffer ? (ushort)65535 : (ushort)2),
                    Size            = type == DisassembledShader.ResourceType.ConstantBuffer ? Math.Max(carry.Size, resource.Size) : (existing?.Size ?? 0),
                    Used            = null,
                    UsedDynamically = null,
                };
            }
        }
        foreach (var shader in VertexShaders)
        {
            CollectResources(constants, shader.Constants, GetConstantById, DisassembledShader.ResourceType.ConstantBuffer);
            CollectResources(samplers, shader.Samplers, GetSamplerById, DisassembledShader.ResourceType.Sampler);
            CollectResources(uavs, shader.UAVs, GetUAVById, DisassembledShader.ResourceType.UAV);
        }
        foreach (var shader in PixelShaders)
        {
            CollectResources(constants, shader.Constants, GetConstantById, DisassembledShader.ResourceType.ConstantBuffer);
            CollectResources(samplers, shader.Samplers, GetSamplerById, DisassembledShader.ResourceType.Sampler);
            CollectResources(uavs, shader.UAVs, GetUAVById, DisassembledShader.ResourceType.UAV);
        }
        Constants = constants.Values.ToArray();
        Samplers = samplers.Values.ToArray();
        UAVs = uavs.Values.ToArray();
        UpdateUsed();
        MaterialParamsSize = (GetConstantById(MaterialParamsConstantId)?.Size ?? 0u) << 4;
        foreach (var param in MaterialParams)
        {
            MaterialParamsSize = Math.Max(MaterialParamsSize, (uint)param.ByteOffset + param.ByteSize);
        }
        MaterialParamsSize = (MaterialParamsSize + 0xFu) & ~0xFu;
    }

    private void UpdateUsed()
    {
        var cUsage = new Dictionary<uint, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();
        var sUsage = new Dictionary<uint, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();
        var uUsage = new Dictionary<uint, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();
        static void CollectUsed(Dictionary<uint, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)> usage, Resource[] resources)
        {
            foreach (var resource in resources)
            {
                if (resource.Used == null)
                {
                    continue;
                }
                usage.TryGetValue(resource.Id, out var carry);
                carry.Item1 ??= Array.Empty<DisassembledShader.VectorComponents>();
                var combined = new DisassembledShader.VectorComponents[Math.Max(carry.Item1.Length, resource.Used.Length)];
                for (var i = 0; i < combined.Length; ++i)
                {
                    combined[i] = (i < carry.Item1.Length ? carry.Item1[i] : 0) | (i < resource.Used.Length ? resource.Used[i] : 0);
                }
                usage[resource.Id] = (combined, carry.Item2 | (resource.UsedDynamically ?? 0));
            }
        }
        static void CopyUsed(Resource[] resources, Dictionary<uint, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)> used)
        {
            for (var i = 0; i < resources.Length; ++i)
            {
                if (used.TryGetValue(resources[i].Id, out var usage))
                {
                    resources[i].Used = usage.Item1;
                    resources[i].UsedDynamically = usage.Item2;
                }
                else
                {
                    resources[i].Used = null;
                    resources[i].UsedDynamically = null;
                }
            }
        }
        foreach (var shader in VertexShaders)
        {
            CollectUsed(cUsage, shader.Constants);
            CollectUsed(sUsage, shader.Samplers);
            CollectUsed(uUsage, shader.UAVs);
        }
        foreach (var shader in PixelShaders)
        {
            CollectUsed(cUsage, shader.Constants);
            CollectUsed(sUsage, shader.Samplers);
            CollectUsed(uUsage, shader.UAVs);
        }
        CopyUsed(Constants, cUsage);
        CopyUsed(Samplers, sUsage);
        CopyUsed(UAVs, uUsage);
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
            {
                valueSets[i].Add(values[i]);
            }
        }
        static void CopyValues(Key[] keys, HashSet<uint>[] valueSets)
        {
            for (var i = 0; i < keys.Length; ++i)
            {
                keys[i].Values = valueSets[i].ToArray();
            }
        }
        var systemKeyValues = InitializeValueSet(SystemKeys);
        var sceneKeyValues = InitializeValueSet(SceneKeys);
        var materialKeyValues = InitializeValueSet(MaterialKeys);
        var subViewKeyValues = InitializeValueSet(SubViewKeys);
        foreach (var node in Nodes)
        {
            CollectValues(systemKeyValues, node.SystemKeys);
            CollectValues(sceneKeyValues, node.SceneKeys);
            CollectValues(materialKeyValues, node.MaterialKeys);
            CollectValues(subViewKeyValues, node.SubViewKeys);
        }
        CopyValues(SystemKeys, systemKeyValues);
        CopyValues(SceneKeys, sceneKeyValues);
        CopyValues(MaterialKeys, materialKeyValues);
        CopyValues(SubViewKeys, subViewKeyValues);
    }

    public void SetInvalid()
    {
        Valid = false;
    }

    public void SetChanged()
    {
        _changed = true;
    }

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
            resources[i].Used = null;
            resources[i].UsedDynamically = null;
        }
    }

    private static Resource[] ReadResourceArray(BinaryReader r, int count, StringPool strings)
    {
        var ret = new Resource[count];
        for (var i = 0; i < count; ++i)
        {
            var buf = new Resource();

            buf.Id        = r.ReadUInt32();
            var strOffset = r.ReadUInt32();
            var strSize   = r.ReadUInt32();
            buf.Name      = strings.GetString((int)strOffset, (int)strSize);
            buf.Slot      = r.ReadUInt16();
            buf.Size      = r.ReadUInt16();

            ret[i] = buf;
        }

        return ret;
    }

    private static Shader[] ReadShaderArray(BinaryReader r, int count, DisassembledShader.ShaderStage stage, DXVersion directX, bool disassemble, ReadOnlySpan<byte> blobs, StringPool strings)
    {
        var extraHeaderSize = stage switch
        {
            DisassembledShader.ShaderStage.Vertex => directX switch
            {
                DXVersion.DirectX9  => 4,
                DXVersion.DirectX11 => 8,
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
            {
                throw new NotImplementedException();
            }

            var rawBlob = blobs.Slice((int)blobOffset, (int)blobSize);

            var shader = new Shader();

            shader.Stage            = disassemble ? stage : DisassembledShader.ShaderStage.Unspecified;
            shader.DirectXVersion   = directX;
            shader.Constants        = ReadResourceArray(r, constantCount, strings);
            shader.Samplers         = ReadResourceArray(r, samplerCount, strings);
            shader.UAVs             = ReadResourceArray(r, uavCount, strings);
            shader.AdditionalHeader = rawBlob[..extraHeaderSize].ToArray();
            shader.Blob             = rawBlob[extraHeaderSize..].ToArray();

            ret[i] = shader;
        }

        return ret;
    }

    private static Key[] ReadKeyArray(BinaryReader r, int count)
    {
        var ret = new Key[count];
        for (var i = 0; i < count; ++i)
        {
            var id           = r.ReadUInt32();
            var defaultValue = r.ReadUInt32();

            ret[i] = new Key
            {
                Id           = id,
                DefaultValue = defaultValue,
                Values       = Array.Empty<uint>(),
            };
        }

        return ret;
    }

    private static Node[] ReadNodeArray(BinaryReader r, int count, int systemKeyCount, int sceneKeyCount, int materialKeyCount, int subViewKeyCount)
    {
        var ret = new Node[count];
        for (var i = 0; i < count; ++i)
        {
            var id           = r.ReadUInt32();
            var passCount    = r.ReadUInt32();
            var passIndices  = r.ReadBytes(16);
            var systemKeys   = r.ReadStructuresAsArray<uint>(systemKeyCount);
            var sceneKeys    = r.ReadStructuresAsArray<uint>(sceneKeyCount);
            var materialKeys = r.ReadStructuresAsArray<uint>(materialKeyCount);
            var subViewKeys  = r.ReadStructuresAsArray<uint>(subViewKeyCount);
            var passes       = r.ReadStructuresAsArray<Pass>((int)passCount);

            ret[i] = new Node
            {
                Id           = id,
                PassIndices  = passIndices,
                SystemKeys   = systemKeys,
                SceneKeys    = sceneKeys,
                MaterialKeys = materialKeys,
                SubViewKeys  = subViewKeys,
                Passes       = passes,
            };
        }

        return ret;
    }
}
