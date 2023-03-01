using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Penumbra.GameData.Interop;
using Penumbra.String;

namespace Penumbra.GameData.Data;

public partial class DisassembledShader
{
    public struct ResourceBinding
    {
        public string             Name;
        public ResourceType       Type;
        public Format             Format;
        public ResourceDimension  Dimension;
        public uint               Slot;
        public uint               Elements;
        public uint               RegisterCount;
        public VectorComponents[] Used;
        public VectorComponents   UsedDynamically;
    }

    // Abbreviated using the uppercased first char of their name
    public enum ResourceType : byte
    {
        Unspecified    = 0,
        ConstantBuffer = 0x43, // 'C'
        Sampler        = 0x53, // 'S'
        Texture        = 0x54, // 'T'
        Uav            = 0x55, // 'U'
    }

    // Abbreviated using the uppercased first and last char of their name
    public enum Format : ushort
    {
        Unspecified   = 0,
        NotApplicable = 0x4E41, // 'NA'
        Int           = 0x4954, // 'IT'
        Int4          = 0x4934, // 'I4'
        Float         = 0x4654, // 'FT'
        Float4        = 0x4634, // 'F4'
    }

    // Abbreviated using the uppercased first and last char of their name
    public enum ResourceDimension : ushort
    {
        Unspecified   = 0,
        NotApplicable = 0x4E41, // 'NA'
        TwoD          = 0x3244, // '2D'
        ThreeD        = 0x3344, // '3D'
        Cube          = 0x4345, // 'CE'
    }

    public struct InputOutput
    {
        public string           Name;
        public uint             Index;
        public VectorComponents Mask;
        public uint             Register;
        public string           SystemValue;
        public Format           Format;
        public VectorComponents Used;
    }

    [Flags]
    public enum VectorComponents : byte
    {
        X   = 1,
        Y   = 2,
        Z   = 4,
        W   = 8,
        All = 15,
    }

    public enum ShaderStage : byte
    {
        Unspecified = 0,
        Pixel       = 0x50, // 'P'
        Vertex      = 0x56, // 'V'
    }

    [GeneratedRegex(@"\s(\w+)(?:\[\d+\])?;\s*//\s*Offset:\s*0\s*Size:\s*(\d+)$", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    private static partial Regex ResourceBindingSizeRegex();

    [GeneratedRegex(@"c(\d+)(?:\[([^\]]+)\])?(?:\.([wxyz]+))?", RegexOptions.NonBacktracking)]
    private static partial Regex Sm3ConstantBufferUsageRegex();

    [GeneratedRegex(@"^\s*texld\S*\s+[^,]+,[^,]+,\s*s(\d+)", RegexOptions.NonBacktracking)]
    private static partial Regex Sm3TextureUsageRegex();

    [GeneratedRegex(@"cb(\d+)\[([^\]]+)\]\.([wxyz]+)", RegexOptions.NonBacktracking)]
    private static partial Regex Sm5ConstantBufferUsageRegex();

    [GeneratedRegex(@"^\s*sample_\S*\s+[^.]+\.([wxyz]+),[^,]+,\s*t(\d+)\.([wxyz]+)", RegexOptions.NonBacktracking)]
    private static partial Regex Sm5TextureUsageRegex();

    private static readonly char[] Digits = Enumerable.Range(0, 10).Select(c => (char)('0' + c)).ToArray();

    public readonly ByteString                RawDisassembly;
    public readonly uint                      ShaderModel;
    public readonly ShaderStage               Stage;
    public readonly string                    BufferDefinitions;
    public readonly ResourceBinding[]         ResourceBindings;
    public readonly InputOutput[]             InputSignature;
    public readonly InputOutput[]             OutputSignature;
    public readonly IReadOnlyList<ByteString> Instructions;

    public DisassembledShader(ByteString rawDisassembly)
    {
        RawDisassembly = rawDisassembly;
        var lines = rawDisassembly.Split((byte) '\n');
        Instructions = lines.FindAll(ln => !ln.StartsWith("//"u8) && ln.Length > 0);
        var shaderModel = Instructions[0].Trim().Split((byte) '_');
        Stage       = (ShaderStage)(byte)char.ToUpper((char) shaderModel[0][0]);
        ShaderModel = (uint.Parse(shaderModel[1].ToString()) << 8) | uint.Parse(shaderModel[2].ToString());
        var header = PreParseHeader(lines.Take(lines.IndexOf(Instructions[0])).Select(l => l.ToString()).ToArray());
        switch (ShaderModel >> 8)
        {
            case 3:
                ParseSm3Header(header, out BufferDefinitions, out ResourceBindings, out InputSignature, out OutputSignature);
                ParseSm3ResourceUsage(Instructions, ResourceBindings);
                break;
            case 5:
                ParseSm5Header(header, out BufferDefinitions, out ResourceBindings, out InputSignature, out OutputSignature);
                ParseSm5ResourceUsage(Instructions, ResourceBindings);
                break;
            default: throw new NotImplementedException();
        }
    }

    public ResourceBinding? GetResourceBindingByName(ResourceType type, string name)
        => ResourceBindings.FirstOrNull(b => b.Type == type && b.Name == name);

    public ResourceBinding? GetResourceBindingBySlot(ResourceType type, uint slot)
        => ResourceBindings.FirstOrNull(b => b.Type == type && b.Slot == slot);

    public static DisassembledShader Disassemble(ReadOnlySpan<byte> shaderBlob)
        => new(D3DCompiler.Disassemble(shaderBlob));

    private static void ParseSm3Header(Dictionary<string, string[]> header, out string bufferDefinitions,
        out ResourceBinding[] resourceBindings, out InputOutput[] inputSignature, out InputOutput[] outputSignature)
    {
        bufferDefinitions = header.TryGetValue("Parameters", out var rawParameters)
            ? string.Join('\n', rawParameters)
            : string.Empty;
        if (header.TryGetValue("Registers", out var rawRegisters))
        {
            var (_, registers) = ParseTable(rawRegisters);
            resourceBindings = Array.ConvertAll(registers, register =>
            {
                var type = (ResourceType)(byte)char.ToUpper(register[1][0]);
                if (type == ResourceType.Sampler)
                    type = ResourceType.Texture;
                var size = uint.Parse(register[2]);
                return new ResourceBinding
                {
                    Name          = register[0],
                    Type          = type,
                    Format        = Format.Unspecified,
                    Dimension     = ResourceDimension.Unspecified,
                    Slot          = uint.Parse(register[1][1..]),
                    Elements      = 1,
                    RegisterCount = size,
                    Used          = new VectorComponents[size],
                };
            });
        }
        else
        {
            resourceBindings = Array.Empty<ResourceBinding>();
        }

        inputSignature  = Array.Empty<InputOutput>();
        outputSignature = Array.Empty<InputOutput>();
    }

    private static void ParseSm3ResourceUsage(IReadOnlyList<ByteString> instructions, ResourceBinding[] resourceBindings)
    {
        var cbIndices = new Dictionary<uint, int>();
        var tIndices  = new Dictionary<uint, int>();
        {
            var i = 0;
            foreach (var binding in resourceBindings)
            {
                switch (binding.Type)
                {
                    case ResourceType.ConstantBuffer:
                        for (var j = 0u; j < binding.RegisterCount; j++)
                            cbIndices[binding.Slot + j] = i;
                        break;
                    case ResourceType.Texture:
                        tIndices[binding.Slot] = i;
                        break;
                }

                ++i;
            }
        }
        foreach (var instruction in instructions)
        {
            var trimmed = instruction.Trim();
            if (trimmed.StartsWith("def"u8) || trimmed.StartsWith("dcl"u8))
                continue;

            var instructionString = instruction.ToString();
            foreach (Match cbMatch in Sm3ConstantBufferUsageRegex().Matches(instructionString))
            {
                var buffer = uint.Parse(cbMatch.Groups[1].Value);
                if (cbIndices.TryGetValue(buffer, out var i))
                {
                    var swizzle = cbMatch.Groups[3].Success ? ParseVectorComponents(cbMatch.Groups[3].Value) : VectorComponents.All;
                    if (cbMatch.Groups[2].Success)
                        resourceBindings[i].UsedDynamically |= swizzle;
                    else
                        resourceBindings[i].Used[buffer - resourceBindings[i].Slot] |= swizzle;
                }
            }

            var tMatch = Sm3TextureUsageRegex().Match(instructionString);
            if (tMatch.Success)
            {
                var texture = uint.Parse(tMatch.Groups[1].Value);
                if (tIndices.TryGetValue(texture, out var i))
                    resourceBindings[i].Used[0] = VectorComponents.All;
            }
        }
    }

    private static void ParseSm5Header(Dictionary<string, string[]> header, out string bufferDefinitions,
        out ResourceBinding[] resourceBindings, out InputOutput[] inputSignature, out InputOutput[] outputSignature)
    {
        if (header.TryGetValue("Resource Bindings", out var rawResBindings))
        {
            var (head, resBindings) = ParseTable(rawResBindings);
            resourceBindings = Array.ConvertAll(resBindings, binding =>
            {
                var type = (ResourceType)(byte)char.ToUpper(binding[1][0]);
                return new ResourceBinding
                {
                    Name          = binding[0],
                    Type          = type,
                    Format        = (Format)(((byte)char.ToUpper(binding[2][0]) << 8) | (byte)char.ToUpper(binding[2][^1])),
                    Dimension     = (ResourceDimension)(((byte)char.ToUpper(binding[3][0]) << 8) | (byte)char.ToUpper(binding[3][^1])),
                    Slot          = uint.Parse(binding[4][binding[4].IndexOfAny(Digits)..]),
                    Elements      = uint.Parse(binding[5]),
                    RegisterCount = type == ResourceType.Texture ? 1u : 0u,
                    Used          = type == ResourceType.Texture ? new VectorComponents[1] : Array.Empty<VectorComponents>(),
                };
            });
        }
        else
        {
            resourceBindings = Array.Empty<ResourceBinding>();
        }

        if (header.TryGetValue("Buffer Definitions", out var rawBufferDefs))
        {
            bufferDefinitions = string.Join('\n', rawBufferDefs);
            foreach (Match match in ResourceBindingSizeRegex().Matches(bufferDefinitions))
            {
                var name = match.Groups[1].Value;
                var bytesSize = uint.Parse(match.Groups[2].Value);
                var pos = Array.FindIndex(resourceBindings, binding => binding.Type == ResourceType.ConstantBuffer && binding.Name == name);
                if (pos >= 0)
                {
                    resourceBindings[pos].RegisterCount = (bytesSize + 0xF) >> 4;
                    resourceBindings[pos].Used          = new VectorComponents[resourceBindings[pos].RegisterCount];
                }
            }
        }
        else
        {
            bufferDefinitions = string.Empty;
        }

        static InputOutput ParseInputOutput(string[] inOut)
            => new()
            {
                Name        = inOut[0],
                Index       = uint.Parse(inOut[1]),
                Mask        = ParseVectorComponents(inOut[2]),
                Register    = uint.Parse(inOut[3]),
                SystemValue = string.Intern(inOut[4]),
                Format      = (Format)(((byte)char.ToUpper(inOut[5][0]) << 8) | (byte)char.ToUpper(inOut[5][^1])),
                Used        = ParseVectorComponents(inOut[6]),
            };

        if (header.TryGetValue("Input signature", out var rawInputSig))
        {
            var (_, inputSig) = ParseTable(rawInputSig);
            inputSignature    = Array.ConvertAll(inputSig, ParseInputOutput);
        }
        else
        {
            inputSignature = Array.Empty<InputOutput>();
        }

        if (header.TryGetValue("Output signature", out var rawOutputSig))
        {
            var (_, outputSig) = ParseTable(rawOutputSig);
            outputSignature    = Array.ConvertAll(outputSig, ParseInputOutput);
        }
        else
        {
            outputSignature = Array.Empty<InputOutput>();
        }
    }

    private static void ParseSm5ResourceUsage(IReadOnlyList<ByteString> instructions, ResourceBinding[] resourceBindings)
    {
        var cbIndices = new Dictionary<uint, int>();
        var tIndices  = new Dictionary<uint, int>();
        {
            var i = 0;
            foreach (var binding in resourceBindings)
            {
                switch (binding.Type)
                {
                    case ResourceType.ConstantBuffer:
                        cbIndices[binding.Slot] = i;
                        break;
                    case ResourceType.Texture:
                        tIndices[binding.Slot] = i;
                        break;
                }

                ++i;
            }
        }
        foreach (var instruction in instructions)
        {
            var trimmed = instruction.Trim();
            if (trimmed.StartsWith("def"u8) || trimmed.StartsWith("dcl"u8))
                continue;

            var instructionString = instruction.ToString();
            foreach (Match cbMatch in Sm5ConstantBufferUsageRegex().Matches(instructionString))
            {
                var buffer = uint.Parse(cbMatch.Groups[1].Value);
                if (cbIndices.TryGetValue(buffer, out var i))
                {
                    var swizzle = ParseVectorComponents(cbMatch.Groups[3].Value);
                    if (int.TryParse(cbMatch.Groups[2].Value, out var vector))
                    {
                        if (vector < resourceBindings[i].Used.Length)
                            resourceBindings[i].Used[vector] |= swizzle;
                    }
                    else
                    {
                        resourceBindings[i].UsedDynamically |= swizzle;
                    }
                }
            }

            var tMatch = Sm5TextureUsageRegex().Match(instructionString);
            if (tMatch.Success)
            {
                var texture = uint.Parse(tMatch.Groups[2].Value);
                if (tIndices.TryGetValue(texture, out var i))
                {
                    var outSwizzle   = ParseVectorComponents(tMatch.Groups[1].Value);
                    var rawInSwizzle = tMatch.Groups[3].Value;
                    var inSwizzle    = new StringBuilder(4);
                    if ((outSwizzle & VectorComponents.X) != 0)
                        inSwizzle.Append(rawInSwizzle[0]);
                    if ((outSwizzle & VectorComponents.Y) != 0)
                        inSwizzle.Append(rawInSwizzle[1]);
                    if ((outSwizzle & VectorComponents.Z) != 0)
                        inSwizzle.Append(rawInSwizzle[2]);
                    if ((outSwizzle & VectorComponents.W) != 0)
                        inSwizzle.Append(rawInSwizzle[3]);
                    resourceBindings[i].Used[0] |= ParseVectorComponents(inSwizzle.ToString());
                }
            }
        }
    }

    private static VectorComponents ParseVectorComponents(string components)
    {
        components = components.ToUpperInvariant();
        return (components.Contains('X') ? VectorComponents.X : 0)
          | (components.Contains('Y') ? VectorComponents.Y : 0)
          | (components.Contains('Z') ? VectorComponents.Z : 0)
          | (components.Contains('W') ? VectorComponents.W : 0);
    }

    private static Dictionary<string, string[]> PreParseHeader(ReadOnlySpan<string> header)
    {
        var sections = new Dictionary<string, string[]>();

        void AddSection(string name, ReadOnlySpan<string> section)
        {
            while (section.Length > 0 && section[0].Length <= 3)
                section = section[1..];
            while (section.Length > 0 && section[^1].Length <= 3)
                section = section[..^1];
            sections.Add(name, Array.ConvertAll(section.ToArray(), ln => ln.Length <= 3 ? string.Empty : ln[3..]));
        }

        var lastSectionName  = "";
        var lastSectionStart = 0;
        for (var i = 1; i < header.Length - 1; ++i)
        {
            string current;
            if (header[i - 1].Length <= 3 && header[i + 1].Length <= 3 && (current = header[i].TrimEnd()).EndsWith(':'))
            {
                AddSection(lastSectionName, header[lastSectionStart..(i - 1)]);
                lastSectionName  = current[3..^1];
                lastSectionStart = i + 2;
                ++i; // The next line cannot match
            }
        }

        AddSection(lastSectionName, header[lastSectionStart..]);

        return sections;
    }

    private static (string[], string[][]) ParseTable(ReadOnlySpan<string> lines)
    {
        var columns = new List<Range>();
        {
            var dashLine = lines[1];
            for (var i = 0; true; /* this part intentionally left blank */)
            {
                var start = dashLine.IndexOf('-', i);
                if (start < 0)
                    break;

                var end = dashLine.IndexOf(' ', start + 1);
                if (end < 0)
                {
                    columns.Add(start..dashLine.Length);
                    break;
                }
                else
                {
                    columns.Add(start..end);
                    i = end + 1;
                }
            }
        }
        var headers = new string[columns.Count];
        {
            var headerLine = lines[0];
            for (var i = 0; i < columns.Count; ++i)
                headers[i] = headerLine[columns[i]].Trim();
        }
        var data = new List<string[]>();
        foreach (var line in lines[2..])
        {
            var row = new string[columns.Count];
            for (var i = 0; i < columns.Count; ++i)
                row[i] = line[columns[i]].Trim();
            data.Add(row);
        }

        return (headers, data.ToArray());
    }
}
