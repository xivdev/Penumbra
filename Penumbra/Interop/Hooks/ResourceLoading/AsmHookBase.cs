using Dalamud.Hooking;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public abstract class AsmHookBase(PeSigScanner sigScanner) : IDisposable
{
    protected readonly Dictionary<nint, AsmHook> Hooks            = [];
    protected readonly List<nint>                NativeAllocCaves = [];
    
    protected PeSigScanner SigScanner { get; } = sigScanner;


    protected AsmHook Setup(string hookName, nint hookAddress, byte[] bytecode, AsmHookBehaviour behaviour = AsmHookBehaviour.ExecuteFirst)
    {
        var hook = new AsmHook(hookAddress, bytecode, hookName, behaviour);

        Hooks.Add(hookAddress, hook);
        
        return hook;
    }
    
    
    protected static AssemblerRegister64 GetRegister64(Register reg)
        => reg switch
        {
            Register.RAX => rax,
            Register.RCX => rcx,
            Register.RDX => rdx,
            Register.RBX => rbx,
            Register.RSP => rsp,
            Register.RBP => rbp,
            Register.RSI => rsi,
            Register.RDI => rdi,
            Register.R8  => r8,
            Register.R9  => r9,
            Register.R10 => r10,
            Register.R11 => r11,
            Register.R12 => r12,
            Register.R13 => r13,
            Register.R14 => r14,
            Register.R15 => r15,
            _            => throw new ArgumentOutOfRangeException(nameof(reg), reg, "Unsupported register."),
        };
    
    protected static byte[] AssembleToBytes(Assembler assembler)
    {
        using var stream = new MemoryStream();
        var       writer = new StreamCodeWriter(stream);
        assembler.Assemble(writer, 0);
        return stream.ToArray();
    }
    
    protected unsafe nint NativeAllocCave(nuint size)
    {
        var caveLoc = (nint)NativeMemory.Alloc(size);
        NativeAllocCaves.Add(caveLoc);

        return caveLoc;
    }
    
    protected static unsafe void NativeFree(nint mem)
        => NativeMemory.Free((void*)mem);


    public virtual void Enable()
    {
        foreach (var hook in Hooks.Values)
            hook.Enable();
    }
    
    public virtual void Disable()
    {
        foreach (var hook in Hooks.Values)
            hook.Disable();
    }
    
    public virtual void Dispose()
    {
        foreach (var hook in Hooks.Values)
        {
            hook.Disable();
            hook.Dispose();
        }

        Hooks.Clear();

        foreach (var mem in NativeAllocCaves)
            NativeFree(mem);

        NativeAllocCaves.Clear();
    }
}
