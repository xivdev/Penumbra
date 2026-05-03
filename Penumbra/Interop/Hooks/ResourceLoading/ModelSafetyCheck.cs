using Dalamud.Hooking;
using Iced.Intel;
using Penumbra.GameData;
using static Iced.Intel.AssemblerRegisters;

namespace Penumbra.Interop.Hooks.ResourceLoading;

public sealed class ModelSafetyCheck(PeSigScanner sigScanner) : AsmHookBase(sigScanner)
{
    public void Setup()
    {
        if (!SigScanner.TryScanText(Sigs.FaceModelSafetyCheck, out var address))
            throw new Exception($"Signature for {nameof(Sigs.FaceModelSafetyCheck)} [{Sigs.FaceModelSafetyCheck}] could not be found.");

        var assembler = new Assembler(64);

        var labelNullPath = assembler.CreateLabel("null_path");
        var labelEnd      = assembler.CreateLabel("end");

        // Test if rcx is null; effectively if (model->Materials[i] == nullptr) -> jump to the null_path label
        assembler.test(rcx, rcx);
        assembler.jz(labelNullPath);

        // Original code we wrote over; if (model->Materials[i] != nullptr), it's safe to run it, then jump over the null_path case
        assembler.mov(rax, __qword_ptr[rcx + 0x10]);
        assembler.cmp(__qword_ptr[rax + 0xc8], r9);
        assembler.jmp(labelEnd);

        // model->Materials[i] is null; we zero out ZF via test rsp, rsp, which assumes rsp != 0. Safe assumption, Winter would say, before it fucks Winter.
        assembler.Label(ref labelNullPath);
        assembler.test(rsp, rsp);

        // Easy label to jump over the test shenanigans, preserving the original cmp flags
        assembler.Label(ref labelEnd);
        assembler.nop();

        base.Setup(nameof(Sigs.FaceModelSafetyCheck), address, AssembleToBytes(assembler), AsmHookBehaviour.DoNotExecuteOriginal).Enable();
    }

    public override void Enable()
    {
        if (HookOverrides.Instance.Objects.FaceModelSafetyCheck)
            return;

        base.Enable();
    }
}
