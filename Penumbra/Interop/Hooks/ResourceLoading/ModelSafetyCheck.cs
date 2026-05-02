using Dalamud.Hooking;
using Iced.Intel;
using Penumbra.GameData;
using static Iced.Intel.AssemblerRegisters;

namespace Penumbra.Interop.Hooks.ResourceLoading;

//public sealed class PapRewriter(PeSigScanner sigScanner, PapRewriter.PapResourceHandlerPrototype papResourceHandler) : IDisposable
public sealed class ModelSafetyCheck(PeSigScanner sigScanner) : AsmHookBase(sigScanner)
{
    public void Setup()
    { 
        const string sig = "48 8B 41 10 4C 39 88 C8 00 00 00";
        const string name = "ModelSafetyCheck";
        if (!SigScanner.TryScanText(sig, out var address))
            throw new Exception($"Signature for {name} [{sig}] could not be found.");

        var assembler = new Assembler(32);
        
        var labelNullPath = assembler.CreateLabel("null_path");
        var labelEnd      = assembler.CreateLabel("end");
        
        // Test if rcx is null; effectively if (model->Materials[i] == nullptr) -> jump to the null_path label
        assembler.test(rcx, rcx);
        assembler.jz(labelNullPath);
        
        // Original code we wrote over; if (model->Materials[i] != nullptr), it's safe to run it, then jump over the null_path case
        assembler.mov(rax, __qword_ptr[rcx + 0x10]);
        assembler.cmp(__qword_ptr[rax + 0xc8], r9);
        assembler.jmp(labelEnd);
        
        // model->Materials[i] is null; we zero out ZF via test rsp, rsp, which assumes rsp != 0. Safe assumption, I'd say, before it fucks me
        assembler.Label(ref labelNullPath);
        assembler.test(rsp, rsp);
        
        // Easy label to jump over the test shenanigans, preserving the original cmp flags
        assembler.Label(ref labelEnd);
        
        base.Setup(name, address, AssembleToBytes(assembler), AsmHookBehaviour.DoNotExecuteOriginal).Enable();
    }
}
