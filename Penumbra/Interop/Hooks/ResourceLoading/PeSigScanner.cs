using System.IO.MemoryMappedFiles;
using Iced.Intel;
using PeNet;
using Decoder = Iced.Intel.Decoder;

namespace Penumbra.Interop.Hooks.ResourceLoading;

// A good chunk of this was blatantly stolen from Dalamud's SigScanner 'cause I could not be faffed, maybe I'll rewrite it later
public class PeSigScanner : IDisposable
{
    private MemoryMappedFile File { get; }

    private uint TextSectionStart { get; }
    private uint TextSectionSize  { get; }
    
    private IntPtr ModuleBaseAddress         { get; }
    private uint   TextSectionVirtualAddress { get; }
    
    private MemoryMappedViewAccessor TextSection { get; }
    
    
    public PeSigScanner()
    {
        var mainModule = Process.GetCurrentProcess().MainModule!;
        var fileName     = mainModule.FileName;
        ModuleBaseAddress = mainModule.BaseAddress;

        if (fileName == null)
        {
            throw new Exception("Can't get main module path, the fuck is going on?");
        }
        
        File = MemoryMappedFile.CreateFromFile(fileName, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);

        using var fileStream = File.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
        var       pe         = new PeFile(fileStream);

        var textSection = pe.ImageSectionHeaders!.First(header => header.Name == ".text");

        TextSectionStart = textSection.PointerToRawData;
        TextSectionSize  = textSection.SizeOfRawData;
        TextSectionVirtualAddress   = textSection.VirtualAddress;

        TextSection = File.CreateViewAccessor(TextSectionStart, TextSectionSize, MemoryMappedFileAccess.Read);
    }


    private IntPtr ScanText(string signature)
    {
        var scanRet = Scan(TextSection, signature);
        
        var instrByte = Marshal.ReadByte(scanRet);

        if (instrByte is 0xE8 or 0xE9)
            scanRet = ReadJmpCallSig(scanRet);

        return scanRet;
    }
    
    private static IntPtr ReadJmpCallSig(IntPtr sigLocation)
    {
        var jumpOffset = Marshal.ReadInt32(sigLocation, 1);
        return IntPtr.Add(sigLocation, 5 + jumpOffset);
    }
    
    public bool TryScanText(string signature, out IntPtr result)
    {
        try
        {
            result = ScanText(signature);
            return true;
        }
        catch (KeyNotFoundException)
        {
            result = IntPtr.Zero;
            return false;
        }
    }
    
    private IntPtr Scan(MemoryMappedViewAccessor section, string signature)
    {
        var (needle, mask) = ParseSignature(signature);
        
        var index       = IndexOf(section, needle, mask);
        if (index < 0)
            throw new KeyNotFoundException($"Can't find a signature of {signature}");
        return new IntPtr(ModuleBaseAddress + index - section.PointerOffset + TextSectionVirtualAddress);
    }
    
    private static (byte[] Needle, bool[] Mask) ParseSignature(string signature)
    {
        signature = signature.Replace(" ", string.Empty);
        if (signature.Length % 2 != 0)
            throw new ArgumentException("Signature without whitespaces must be divisible by two.", nameof(signature));

        var needleLength = signature.Length / 2;
        var needle       = new byte[needleLength];
        var mask         = new bool[needleLength];
        for (var i = 0; i < needleLength; i++)
        {
            var hexString = signature.Substring(i * 2, 2);
            if (hexString == "??" || hexString == "**")
            {
                needle[i] = 0;
                mask[i]   = true;
                continue;
            }

            needle[i] = byte.Parse(hexString, NumberStyles.AllowHexSpecifier);
            mask[i]   = false;
        }

        return (needle, mask);
    }
    
    private static unsafe int IndexOf(MemoryMappedViewAccessor section, byte[] needle, bool[] mask)
    {
        if (needle.Length > section.Capacity) return -1;
        var badShift  = BuildBadCharTable(needle, mask);
        var last      = needle.Length - 1;
        var offset    = 0;
        var maxOffset = section.Capacity - needle.Length;

        byte* buffer = null;
        section.SafeMemoryMappedViewHandle.AcquirePointer(ref buffer);
        try
        {
            while (offset <= maxOffset)
            {
                int position;
                for (position = last; needle[position] == *(buffer + position + offset) || mask[position]; position--)
                {
                    if (position == 0)
                        return offset;
                }

                offset += badShift[*(buffer + offset + last)];
            }
        }
        finally
        {
            section.SafeMemoryMappedViewHandle.ReleasePointer();
        }

        return -1;
    }
    
    
    private static int[] BuildBadCharTable(byte[] needle, bool[] mask)
    {
        int idx;
        var last     = needle.Length - 1;
        var badShift = new int[256];
        for (idx = last; idx > 0 && !mask[idx]; --idx)
        {
        }

        var diff            = last - idx;
        if (diff == 0) diff = 1;

        for (idx = 0; idx <= 255; ++idx)
            badShift[idx] = diff;
        for (idx = last - diff; idx < last; ++idx)
            badShift[needle[idx]] = last - idx;
        return badShift;
    }
    
    // Detects function termination; this is done in a really stupid way that will possibly break if looked at wrong, but it'll work for now
    // If this shits itself, go bother Winter to implement proper CFG + basic block detection
    public IEnumerable<Instruction> GetFunctionInstructions(IntPtr addr)
    {
        var fileOffset = addr - TextSectionVirtualAddress - ModuleBaseAddress;
        
        var codeReader = new MappedCodeReader(TextSection, fileOffset);
        var decoder = Decoder.Create(64, codeReader, (ulong)addr.ToInt64());
        
        do
        {
            decoder.Decode(out var instr);

            // Yes, this is catastrophically bad, but it works for some cases okay
            if (instr.Mnemonic == Mnemonic.Int3)
                break;

            yield return instr;
        } while (true);
    }

    public void Dispose()
    {
        TextSection.Dispose();
        File.Dispose();
    }
}
