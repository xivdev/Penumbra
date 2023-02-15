using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        public Resource[] UnknownX;
        public Resource[] UnknownY;
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
                            throw new ArgumentException($"The supplied blob has inconsistent shader and texture allocation.");
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

        public void UpdateResources(ShpkFile file)
        {
            if (_disassembly == null)
            {
                throw new InvalidOperationException();
            }
            var constants = new List<Resource>();
            var samplers = new List<Resource>();
            foreach (var binding in _disassembly.ResourceBindings)
            {
                switch (binding.Type)
                {
                    case DisassembledShader.ResourceType.ConstantBuffer:
                        var name = NormalizeResourceName(binding.Name);
                        // We want to preserve IDs as much as possible, and to deterministically generate new ones, to maximize compatibility.
                        var id = GetConstantByName(name)?.Id ?? file.GetConstantByName(name)?.Id ?? Crc32.Get(name);
                        constants.Add(new Resource
                        {
                            Id   = id,
                            Name = name,
                            Slot = (ushort)binding.Slot,
                            Size = (ushort)binding.RegisterCount,
                            Used = binding.Used,
                        });
                        break;
                    case DisassembledShader.ResourceType.Texture:
                        name = NormalizeResourceName(binding.Name);
                        id = GetSamplerByName(name)?.Id ?? file.GetSamplerByName(name)?.Id ?? Crc32.Get(name);
                        samplers.Add(new Resource
                        {
                            Id   = id,
                            Name = name,
                            Slot = (ushort)binding.Slot,
                            Size = (ushort)binding.Slot,
                            Used = binding.Used,
                        });
                        break;
                }
            }
            Constants = constants.ToArray();
            Samplers = samplers.ToArray();
        }

        private void UpdateUsed()
        {
            if (_disassembly != null)
            {
                var cbUsage = new Dictionary<string, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();
                var tUsage = new Dictionary<string, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();
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
                    }
                }
                for (var i = 0; i < Constants.Length; ++i)
                {
                    if (cbUsage.TryGetValue(Constants[i].Name, out var usage))
                    {
                        Constants[i].Used = usage.Item1;
                        Constants[i].UsedDynamically = usage.Item2;
                    }
                    else
                    {
                        Constants[i].Used = null;
                        Constants[i].UsedDynamically = null;
                    }
                }
                for (var i = 0; i < Samplers.Length; ++i)
                {
                    if (tUsage.TryGetValue(Samplers[i].Name, out var usage))
                    {
                        Samplers[i].Used = usage.Item1;
                        Samplers[i].UsedDynamically = usage.Item2;
                    }
                    else
                    {
                        Samplers[i].Used = null;
                        Samplers[i].UsedDynamically = null;
                    }
                }
            }
            else
            {
                ClearUsed(Constants);
                ClearUsed(Samplers);
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

    public uint Unknown1;
    public DXVersion DirectXVersion;
    public Shader[] VertexShaders;
    public Shader[] PixelShaders;
    public uint MaterialParamsSize;
    public MaterialParam[] MaterialParams;
    public Resource[] Constants;
    public Resource[] Samplers;
    public (uint, uint)[] UnknownA;
    public (uint, uint)[] UnknownB;
    public (uint, uint)[] UnknownC;
    public uint Unknown2;
    public uint Unknown3;
    public uint Unknown4;
    public (uint, uint, uint) Unknowns;
    public byte[] AdditionalData;
    public StringPool Strings; // Cannot be safely discarded yet, we don't know if AdditionalData references it

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
        Unknown1 = r.ReadUInt32();
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
        var unknownACount      = r.ReadUInt32();
        var unknownBCount      = r.ReadUInt32();
        var unknownCCount      = r.ReadUInt32();
        Unknown2               = r.ReadUInt32();
        Unknown3               = r.ReadUInt32();
        Unknown4               = r.ReadUInt32();

        var blobs   = new ReadOnlySpan<byte>(data, (int)blobsOffset, (int)(stringsOffset - blobsOffset));
        Strings     = new StringPool(new ReadOnlySpan<byte>(data, (int)stringsOffset, (int)(data.Length - stringsOffset)));

        VertexShaders  = ReadShaderArray(r, (int)vertexShaderCount, DisassembledShader.ShaderStage.Vertex, DirectXVersion, disassemble, blobs, Strings);
        PixelShaders   = ReadShaderArray(r, (int)pixelShaderCount, DisassembledShader.ShaderStage.Pixel, DirectXVersion, disassemble, blobs, Strings);

        MaterialParams = r.ReadStructuresAsArray<MaterialParam>((int)materialParamCount);

        Constants      = ReadResourceArray(r, (int)constantCount, Strings);
        Samplers       = ReadResourceArray(r, (int)samplerCount, Strings);

        var unk1       = r.ReadUInt32();
        var unk2       = r.ReadUInt32();
        var unk3       = r.ReadUInt32();
        Unknowns       = (unk1, unk2, unk3);

        UnknownA       = ReadUInt32PairArray(r, (int)unknownACount);
        UnknownB       = ReadUInt32PairArray(r, (int)unknownBCount);
        UnknownC       = ReadUInt32PairArray(r, (int)unknownCCount);

        AdditionalData = r.ReadBytes((int)(blobsOffset - r.BaseStream.Position));

        if (disassemble)
        {
            UpdateUsed();
        }

        Valid = true;
        _changed = false;
    }

    public void UpdateResources()
    {
        var constants = new Dictionary<uint, Resource>();
        var samplers = new Dictionary<uint, Resource>();
        static void CollectResources(Dictionary<uint, Resource> resources, Resource[] shaderResources, Func<uint, Resource?> getExistingById, bool isSamplers)
        {
            foreach (var resource in shaderResources)
            {
                if (resources.TryGetValue(resource.Id, out var carry) && isSamplers)
                {
                    continue;
                }
                var existing = getExistingById(resource.Id);
                resources[resource.Id] = new Resource
                {
                    Id              = resource.Id,
                    Name            = resource.Name,
                    Slot            = existing?.Slot ?? (isSamplers ? (ushort)2 : (ushort)65535),
                    Size            = isSamplers ? (existing?.Size ?? 0) : Math.Max(carry.Size, resource.Size),
                    Used            = null,
                    UsedDynamically = null,
                };
            }
        }
        foreach (var shader in VertexShaders)
        {
            CollectResources(constants, shader.Constants, GetConstantById, false);
            CollectResources(samplers, shader.Samplers, GetSamplerById, true);
        }
        foreach (var shader in PixelShaders)
        {
            CollectResources(constants, shader.Constants, GetConstantById, false);
            CollectResources(samplers, shader.Samplers, GetSamplerById, true);
        }
        Constants = constants.Values.ToArray();
        Samplers = samplers.Values.ToArray();
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
        static void CollectUsage(Dictionary<uint, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)> usage, Resource[] resources)
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
        foreach (var shader in VertexShaders)
        {
            CollectUsage(cUsage, shader.Constants);
            CollectUsage(sUsage, shader.Samplers);
        }
        foreach (var shader in PixelShaders)
        {
            CollectUsage(cUsage, shader.Constants);
            CollectUsage(sUsage, shader.Samplers);
        }
        for (var i = 0; i < Constants.Length; ++i)
        {
            if (cUsage.TryGetValue(Constants[i].Id, out var usage))
            {
                Constants[i].Used = usage.Item1;
                Constants[i].UsedDynamically = usage.Item2;
            }
            else
            {
                Constants[i].Used = null;
                Constants[i].UsedDynamically = null;
            }
        }
        for (var i = 0; i < Samplers.Length; ++i)
        {
            if (sUsage.TryGetValue(Samplers[i].Id, out var usage))
            {
                Samplers[i].Used = usage.Item1;
                Samplers[i].UsedDynamically = usage.Item2;
            }
            else
            {
                Samplers[i].Used = null;
                Samplers[i].UsedDynamically = null;
            }
        }
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
            var unknownXCount = r.ReadUInt16();
            var unknownYCount = r.ReadUInt16();

            var rawBlob = blobs.Slice((int)blobOffset, (int)blobSize);

            var shader = new Shader();

            shader.Stage            = disassemble ? stage : DisassembledShader.ShaderStage.Unspecified;
            shader.DirectXVersion   = directX;
            shader.Constants        = ReadResourceArray(r, constantCount, strings);
            shader.Samplers         = ReadResourceArray(r, samplerCount, strings);
            shader.UnknownX         = ReadResourceArray(r, unknownXCount, strings);
            shader.UnknownY         = ReadResourceArray(r, unknownYCount, strings);
            shader.AdditionalHeader = rawBlob[..extraHeaderSize].ToArray();
            shader.Blob             = rawBlob[extraHeaderSize..].ToArray();

            ret[i] = shader;
        }

        return ret;
    }

    private static (uint, uint)[] ReadUInt32PairArray(BinaryReader r, int count)
    {
        var ret = new (uint, uint)[count];
        for (var i = 0; i < count; ++i)
        {
            var first  = r.ReadUInt32();
            var second = r.ReadUInt32();

            ret[i] = (first, second);
        }

        return ret;
    }
}
