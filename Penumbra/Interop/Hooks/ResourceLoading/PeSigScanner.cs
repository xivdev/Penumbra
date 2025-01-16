using System.IO.MemoryMappedFiles;
using Iced.Intel;
using OtterGui.Services;
using PeNet;
using Decoder = Iced.Intel.Decoder;

namespace Penumbra.Interop.Hooks.ResourceLoading;

// A good chunk of this was blatantly stolen from Dalamud's SigScanner 'cause Winter could not be faffed, Winter will definitely not rewrite it later
public unsafe class PeSigScanner : IDisposable, IService
{
    private readonly MemoryMappedFile         _file;
    private readonly MemoryMappedViewAccessor _textSection;

    private readonly nint _moduleBaseAddress;
    private readonly uint _textSectionVirtualAddress;

    public PeSigScanner()
    {
        var mainModule = Process.GetCurrentProcess().MainModule!;
        var fileName   = mainModule.FileName;
        _moduleBaseAddress = mainModule.BaseAddress;

        if (fileName == null)
            throw new Exception("Unable to obtain main module path. This should not happen.");

        _file = MemoryMappedFile.CreateFromFile(fileName, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);

        using var fileStream = _file.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
        var       pe         = new PeFile(fileStream);

        var textSection = pe.ImageSectionHeaders!.First(header => header.Name == ".text");

        var textSectionStart = textSection.PointerToRawData;
        var textSectionSize  = textSection.SizeOfRawData;
        _textSectionVirtualAddress = textSection.VirtualAddress;

        _textSection = _file.CreateViewAccessor(textSectionStart, textSectionSize, MemoryMappedFileAccess.Read);
    }


    private nint ScanText(string signature)
    {
        var scanRet = Scan(_textSection, signature);
        if (*(byte*)scanRet is 0xE8 or 0xE9)
            scanRet = ReadJmpCallSig(scanRet);

        return scanRet;
    }

    private static nint ReadJmpCallSig(nint sigLocation)
    {
        var jumpOffset = *(int*)(sigLocation + 1);
        return sigLocation + 5 + jumpOffset;
    }

    public bool TryScanText(string signature, out nint result)
    {
        try
        {
            result = ScanText(signature);
            return true;
        }
        catch (KeyNotFoundException)
        {
            result = nint.Zero;
            return false;
        }
    }

    private nint Scan(MemoryMappedViewAccessor section, string signature)
    {
        var (needle, mask) = ParseSignature(signature);

        var index = IndexOf(section, needle, mask);
        if (index < 0)
            throw new KeyNotFoundException($"Can't find a signature of {signature}");

        return (nint)(_moduleBaseAddress + index - section.PointerOffset + _textSectionVirtualAddress);
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
            if (hexString is "??" or "**")
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

    private static int IndexOf(MemoryMappedViewAccessor section, byte[] needle, bool[] mask)
    {
        if (needle.Length > section.Capacity)
            return -1;

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
        { }

        var diff = last - idx;
        if (diff == 0)
            diff = 1;

        for (idx = 0; idx <= 255; ++idx)
            badShift[idx] = diff;
        for (idx = last - diff; idx < last; ++idx)
            badShift[needle[idx]] = last - idx;
        return badShift;
    }

    // Detects function termination; this is done in a really stupid way that will possibly break if looked at wrong, but it'll work for now
    // If this shits itself, go bother Winter to implement proper CFG + basic block detection
    public IEnumerable<Instruction> GetFunctionInstructions(nint address)
    {
        var fileOffset = address - _textSectionVirtualAddress - _moduleBaseAddress;

        var codeReader = new MappedCodeReader(_textSection, fileOffset);
        var decoder    = Decoder.Create(64, codeReader, (ulong)address.ToInt64());

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
        _textSection.Dispose();
        _file.Dispose();
    }
}
