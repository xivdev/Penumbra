using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Penumbra.GameData.Interop;

internal static class D3DCompiler
{
    [Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID3DBlob
    {
        [PreserveSig]
        public unsafe void* GetBufferPointer();

        [PreserveSig]
        public UIntPtr GetBufferSize();
    }

    [Flags]
    public enum DisassembleFlags : uint
    {
        EnableColorCode = 1,
        EnableDefaultValuePrints = 2,
        EnableInstructionNumbering = 4,
        EnableInstructionCycle = 8,
        DisableDebugInfo = 16,
        EnableInstructionOffset = 32,
        InstructionOnly = 64,
        PrintHexLiterals = 128,
    }

    public static unsafe string Disassemble(ReadOnlySpan<byte> blob, DisassembleFlags flags = 0, string comments = "")
    {
        ID3DBlob? disassembly;
        int hr;
        fixed (byte* pSrcData = blob)
        {
            hr = D3DDisassemble(pSrcData, new UIntPtr((uint)blob.Length), (uint)flags, comments, out disassembly);
        }
        Marshal.ThrowExceptionForHR(hr);
        var ret = Encoding.UTF8.GetString(BlobContents(disassembly));
        GC.KeepAlive(disassembly);
        return ret;
    }

    private static unsafe ReadOnlySpan<byte> BlobContents(ID3DBlob? blob)
    {
        if (blob == null)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        return new ReadOnlySpan<byte>(blob.GetBufferPointer(), (int)blob.GetBufferSize().ToUInt32());
    }

    [PreserveSig]
    [DllImport("D3DCompiler_47.dll")]
    private extern static unsafe int D3DDisassemble(
        [In] byte* pSrcData,
        [In] UIntPtr srcDataSize,
        uint flags,
        [MarshalAs(UnmanagedType.LPStr)] string szComments,
        out ID3DBlob? ppDisassembly);
}
