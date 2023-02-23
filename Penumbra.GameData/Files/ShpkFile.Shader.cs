using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lumina.Misc;
using Penumbra.GameData.Data;

namespace Penumbra.GameData.Files;

public partial class ShpkFile
{
    public struct Shader
    {
        public  DisassembledShader.ShaderStage Stage;
        public  DxVersion                      DirectXVersion;
        public  Resource[]                     Constants;
        public  Resource[]                     Samplers;
        public  Resource[]                     Uavs;
        public  byte[]                         AdditionalHeader;
        private byte[]                         _byteData;
        private DisassembledShader?            _disassembly;

        public byte[] Blob
        {
            get => _byteData;
            set
            {
                if (_byteData == value)
                    return;

                if (Stage != DisassembledShader.ShaderStage.Unspecified)
                {
                    // Reject the blob entirely if we can't disassemble it or if we find inconsistencies.
                    var disasm = DisassembledShader.Disassemble(value);
                    if (disasm.Stage != Stage || (disasm.ShaderModel >> 8) + 6 != (uint)DirectXVersion)
                        throw new ArgumentException(
                            $"The supplied blob is a DirectX {(disasm.ShaderModel >> 8) + 6} {disasm.Stage} shader ; expected a DirectX {(uint)DirectXVersion} {Stage} shader.",
                            nameof(value));

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

                        if (samplers.Count != textures.Count
                         || !samplers.All(pair => textures.TryGetValue(pair.Key, out var texName) && pair.Value == texName))
                            throw new ArgumentException($"The supplied blob has inconsistent sampler and texture allocation.");
                    }

                    _byteData    = value;
                    _disassembly = disasm;
                }
                else
                {
                    _byteData    = value;
                    _disassembly = null;
                }

                UpdateUsed();
            }
        }

        public DisassembledShader? Disassembly
            => _disassembly;

        public Resource? GetConstantById(uint id)
            => Constants.FirstOrNull(res => res.Id == id);

        public Resource? GetConstantByName(string name)
            => Constants.FirstOrNull(res => res.Name == name);

        public Resource? GetSamplerById(uint id)
            => Samplers.FirstOrNull(s => s.Id == id);

        public Resource? GetSamplerByName(string name)
            => Samplers.FirstOrNull(s => s.Name == name);

        public Resource? GetUavById(uint id)
            => Uavs.FirstOrNull(u => u.Id == id);

        public Resource? GetUavByName(string name)
            => Uavs.FirstOrNull(u => u.Name == name);

        public void UpdateResources(ShpkFile file)
        {
            if (_disassembly == null)
                throw new InvalidOperationException();

            var constants = new List<Resource>();
            var samplers  = new List<Resource>();
            var uavs      = new List<Resource>();
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
                        id   = GetSamplerByName(name)?.Id ?? file.GetSamplerByName(name)?.Id ?? Crc32.Get(name, 0xFFFFFFFFu);
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
                    case DisassembledShader.ResourceType.Uav:
                        name = NormalizeResourceName(binding.Name);
                        id   = GetUavByName(name)?.Id ?? file.GetUavByName(name)?.Id ?? Crc32.Get(name, 0xFFFFFFFFu);
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
            Samplers  = samplers.ToArray();
            Uavs      = uavs.ToArray();
        }

        private void UpdateUsed()
        {
            if (_disassembly != null)
            {
                var cbUsage = new Dictionary<string, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();
                var tUsage  = new Dictionary<string, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();
                var uUsage  = new Dictionary<string, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)>();
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
                        case DisassembledShader.ResourceType.Uav:
                            uUsage[NormalizeResourceName(binding.Name)] = (binding.Used, binding.UsedDynamically);
                            break;
                    }
                }

                static void CopyUsed(Resource[] resources,
                    Dictionary<string, (DisassembledShader.VectorComponents[], DisassembledShader.VectorComponents)> used)
                {
                    for (var i = 0; i < resources.Length; ++i)
                    {
                        if (used.TryGetValue(resources[i].Name, out var usage))
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

                CopyUsed(Constants, cbUsage);
                CopyUsed(Samplers,  tUsage);
                CopyUsed(Uavs,      uUsage);
            }
            else
            {
                ClearUsed(Constants);
                ClearUsed(Samplers);
                ClearUsed(Uavs);
            }
        }

        private static string NormalizeResourceName(string resourceName)
        {
            var dot = resourceName.IndexOf('.');
            if (dot >= 0)
                return resourceName[..dot];
            if (resourceName.Length > 1 && resourceName[^2] is '_' && resourceName[^1] is 'S' or 'T')
                return resourceName[..^2];

            return resourceName;
        }
    }
}
