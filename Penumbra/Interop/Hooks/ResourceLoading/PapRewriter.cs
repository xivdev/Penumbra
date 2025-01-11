using System.Text.Unicode;
using Dalamud.Hooking;
using Iced.Intel;
using OtterGui;
using Penumbra.String.Classes;
using Swan;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public sealed class PapRewriter(PeSigScanner sigScanner, PapRewriter.PapResourceHandlerPrototype papResourceHandler) : IDisposable
{
    public unsafe delegate int PapResourceHandlerPrototype(void* self, byte* path, int length);

    private readonly Dictionary<nint, AsmHook>                 _hooks            = [];
    private readonly Dictionary<(nint, Register, ulong), nint> _nativeAllocPaths = [];
    private readonly List<nint>                                _nativeAllocCaves = [];

    public void Rewrite(string sig, string name)
    {
        if (!sigScanner.TryScanText(sig, out var address))
            throw new Exception($"Signature for {name} [{sig}] could not be found.");

        var funcInstructions = sigScanner.GetFunctionInstructions(address).ToArray();
        var hookPoints       = ScanPapHookPoints(funcInstructions).ToList();

        foreach (var hookPoint in hookPoints)
        {
            var stackAccesses = ScanStackAccesses(funcInstructions, hookPoint).ToList();
            var stringAllocation = NativeAllocPath(
                address, hookPoint.MemoryBase, hookPoint.MemoryDisplacement64,
                Utf8GamePath.MaxGamePathLength
            );
            WriteToAlloc(stringAllocation, Utf8GamePath.MaxGamePathLength, name);

            // We'll need to grab our true hook point; the location where we can change the path at our leisure.
            // This is going to be the first call instruction after our 'hookPoint', so, we'll find that.
            // Pretty scuffed, this might need a refactoring at some point.
            // We're doing it by skipping to our hookPoint's address in the list of instructions inside the function; then getting next CALL
            var skipIndex = funcInstructions.IndexOf(instr => instr.IP == hookPoint.IP) + 1;
            var detourPoint = funcInstructions.Skip(skipIndex)
                .First(instr => instr.Mnemonic == Mnemonic.Call);

            // We'll also remove all the 'hookPoints' from 'stackAccesses'.
            // We're handling the char *path redirection here, so we don't want this to hit the later code
            foreach (var hp in hookPoints)
                stackAccesses.RemoveAll(instr => instr.IP == hp.IP);

            var detourPointer  = Marshal.GetFunctionPointerForDelegate(papResourceHandler);
            var targetRegister = hookPoint.Op0Register.ToString().ToLower();
            var hookAddress    = new IntPtr((long)detourPoint.IP);

            var caveAllocation = NativeAllocCave(16);
            var hook = new AsmHook(
                hookAddress,
                [
                    "use64",
                    $"mov {targetRegister}, 0x{stringAllocation:x8}", // Move our char *path into the relevant register (rdx)

                    // After this asm stub, we have a call to Crc32(); since r9 is a volatile, unused register, we can use it ourselves
                    // We're essentially storing the original 2 arguments ('this', 'path'), in case they get mangled in our call
                    // We technically don't need to save rdx ('path'), since it'll be stringLoc, but eh
                    $"mov r9, 0x{caveAllocation:x8}",
                    "mov [r9], rcx",
                    "mov [r9+0x8], rdx",

                    // We can use 'rax' here too since it's also volatile, and it'll be overwritten by Crc32()'s return anyway
                    $"mov rax, 0x{detourPointer:x8}", // Get a pointer to our detour in place
                    "call rax",                       // Call detour

                    // Do the reverse process and retrieve the stored stuff
                    $"mov r9, 0x{caveAllocation:x8}",
                    "mov rcx, [r9]",
                    "mov rdx, [r9+0x8]",

                    // Plop 'rax' (our return value, the path size) into r8, so it's the third argument for the subsequent Crc32() call
                    "mov r8, rax",
                ], $"{name}.PapRedirection"
            );

            _hooks.Add(hookAddress, hook);
            hook.Enable();

            // Now we're adjusting every single reference to the stack allocated 'path' to our substantially bigger 'stringLoc'
            UpdatePathAddresses(stackAccesses, stringAllocation, name);
        }
    }

    private void UpdatePathAddresses(IEnumerable<Instruction> stackAccesses, nint stringAllocation, string name)
    {
        foreach (var (stackAccess, index) in stackAccesses.WithIndex())
        {
            var hookAddress = new IntPtr((long)stackAccess.IP + stackAccess.Length);

            // Hook already exists, means there's reuse of the same stack address across 2 GetResourceAsync; just skip
            if (_hooks.ContainsKey(hookAddress))
                continue;

            var targetRegister = stackAccess.Op0Register.ToString().ToLower();
            var hook = new AsmHook(
                hookAddress,
                [
                    "use64",
                    $"mov {targetRegister}, 0x{stringAllocation:x8}",
                ], $"{name}.PapStackAccess[{index}]"
            );

            _hooks.Add(hookAddress, hook);
            hook.Enable();
        }
    }

    private static IEnumerable<Instruction> ScanStackAccesses(IEnumerable<Instruction> instructions, Instruction hookPoint)
    {
        return instructions.Where(instr =>
                instr.Code == hookPoint.Code
             && instr.Op0Kind == hookPoint.Op0Kind
             && instr.Op1Kind == hookPoint.Op1Kind
             && instr.MemoryBase == hookPoint.MemoryBase
             && instr.MemoryDisplacement64 == hookPoint.MemoryDisplacement64)
            .GroupBy(instr => instr.IP)
            .Select(grp => grp.First());
    }

    // This is utterly fucked and hardcoded, but, again, it works
    // Might be a neat idea for a more versatile kind of signature though
    private static IEnumerable<Instruction> ScanPapHookPoints(Instruction[] funcInstructions)
    {
        for (var i = 0; i < funcInstructions.Length - 8; i++)
        {
            if (funcInstructions.AsSpan(i, 8) is
                [
                    { Code    : Code.Lea_r64_m },
                    { Code    : Code.Lea_r64_m },
                    { Mnemonic: Mnemonic.Call },
                    { Code    : Code.Lea_r64_m },
                    { Mnemonic: Mnemonic.Call },
                    { Code    : Code.Lea_r64_m },
                    ..,
                ]
               )
                yield return funcInstructions[i];
        }
    }

    private unsafe nint NativeAllocCave(nuint size)
    {
        var caveLoc = (nint)NativeMemory.Alloc(size);
        _nativeAllocCaves.Add(caveLoc);

        return caveLoc;
    }

    // This is a bit conked but, if we identify a path by:
    // 1) The function it belongs to (starting address, 'funcAddress')
    // 2) The stack register (not strictly necessary - should always be rbp - but abundance of caution, so I don't hit myself in the future)
    // 3) The displacement on the stack
    // Then we ensure we have a unique identifier for the specific variable location of that specific function
    // This is useful because sometimes the stack address is reused within the same function for different GetResourceAsync calls
    private unsafe nint NativeAllocPath(nint funcAddress, Register stackRegister, ulong stackDisplacement, nuint size)
        => _nativeAllocPaths.GetOrAdd((funcAddress, stackRegister, stackDisplacement), _ => (nint)NativeMemory.Alloc(size));

    private static unsafe void NativeFree(nint mem)
        => NativeMemory.Free((void*)mem);

    public void Dispose()
    {
        foreach (var hook in _hooks.Values)
        {
            hook.Disable();
            hook.Dispose();
        }

        _hooks.Clear();

        foreach (var mem in _nativeAllocCaves)
            NativeFree(mem);

        _nativeAllocCaves.Clear();

        foreach (var mem in _nativeAllocPaths.Values)
            NativeFree(mem);

        _nativeAllocPaths.Clear();
    }

    [Conditional("DEBUG")]
    private static unsafe void WriteToAlloc(nint alloc, int size, string name)
    {
        var span = new Span<byte>((void*)alloc, size);
        Utf8.TryWrite(span, $"Penumbra.{name}\0", out _);
    }
}
