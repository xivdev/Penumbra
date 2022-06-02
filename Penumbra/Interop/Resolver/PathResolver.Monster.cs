using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    [Signature( "48 8D 05 ?? ?? ?? ?? 48 89 03 33 C0 48 89 83 ?? ?? ?? ?? 48 89 83 ?? ?? ?? ?? C7 83", ScanType = ScanType.StaticAddress )]
    public IntPtr* DrawObjectMonsterVTable;

    public Hook<GeneralResolveDelegate>? ResolveMonsterDecalPathHook;
    public Hook<EidResolveDelegate>?     ResolveMonsterEidPathHook;
    public Hook<GeneralResolveDelegate>? ResolveMonsterImcPathHook;
    public Hook<MPapResolveDelegate>?    ResolveMonsterMPapPathHook;
    public Hook<GeneralResolveDelegate>? ResolveMonsterMdlPathHook;
    public Hook<MaterialResolveDetour>?  ResolveMonsterMtrlPathHook;
    public Hook<MaterialResolveDetour>?  ResolveMonsterPapPathHook;
    public Hook<GeneralResolveDelegate>? ResolveMonsterPhybPathHook;
    public Hook<GeneralResolveDelegate>? ResolveMonsterSklbPathHook;
    public Hook<GeneralResolveDelegate>? ResolveMonsterSkpPathHook;
    public Hook<EidResolveDelegate>?     ResolveMonsterTmbPathHook;
    public Hook<MaterialResolveDetour>?  ResolveMonsterVfxPathHook;

    private void SetupMonsterHooks()
    {
        ResolveMonsterDecalPathHook = new Hook<GeneralResolveDelegate>( DrawObjectMonsterVTable[ResolveDecalIdx], ResolveMonsterDecalDetour );
        ResolveMonsterEidPathHook   = new Hook<EidResolveDelegate>( DrawObjectMonsterVTable[ResolveEidIdx], ResolveMonsterEidDetour );
        ResolveMonsterImcPathHook   = new Hook<GeneralResolveDelegate>( DrawObjectMonsterVTable[ResolveImcIdx], ResolveMonsterImcDetour );
        ResolveMonsterMPapPathHook  = new Hook<MPapResolveDelegate>( DrawObjectMonsterVTable[ResolveMPapIdx], ResolveMonsterMPapDetour );
        ResolveMonsterMdlPathHook   = new Hook<GeneralResolveDelegate>( DrawObjectMonsterVTable[ResolveMdlIdx], ResolveMonsterMdlDetour );
        ResolveMonsterMtrlPathHook  = new Hook<MaterialResolveDetour>( DrawObjectMonsterVTable[ResolveMtrlIdx], ResolveMonsterMtrlDetour );
        ResolveMonsterPapPathHook   = new Hook<MaterialResolveDetour>( DrawObjectMonsterVTable[ResolvePapIdx], ResolveMonsterPapDetour );
        ResolveMonsterPhybPathHook  = new Hook<GeneralResolveDelegate>( DrawObjectMonsterVTable[ResolvePhybIdx], ResolveMonsterPhybDetour );
        ResolveMonsterSklbPathHook  = new Hook<GeneralResolveDelegate>( DrawObjectMonsterVTable[ResolveSklbIdx], ResolveMonsterSklbDetour );
        ResolveMonsterSkpPathHook   = new Hook<GeneralResolveDelegate>( DrawObjectMonsterVTable[ResolveSkpIdx], ResolveMonsterSkpDetour );
        ResolveMonsterTmbPathHook   = new Hook<EidResolveDelegate>( DrawObjectMonsterVTable[ResolveTmbIdx], ResolveMonsterTmbDetour );
        ResolveMonsterVfxPathHook   = new Hook<MaterialResolveDetour>( DrawObjectMonsterVTable[ResolveVfxIdx], ResolveMonsterVfxDetour );
    }

    private void EnableMonsterHooks()
    {
        ResolveMonsterDecalPathHook?.Enable();
        ResolveMonsterEidPathHook?.Enable();
        ResolveMonsterImcPathHook?.Enable();
        ResolveMonsterMPapPathHook?.Enable();
        ResolveMonsterMdlPathHook?.Enable();
        ResolveMonsterMtrlPathHook?.Enable();
        ResolveMonsterPapPathHook?.Enable();
        ResolveMonsterPhybPathHook?.Enable();
        ResolveMonsterSklbPathHook?.Enable();
        ResolveMonsterSkpPathHook?.Enable();
        ResolveMonsterTmbPathHook?.Enable();
        ResolveMonsterVfxPathHook?.Enable();
    }

    private void DisableMonsterHooks()
    {
        ResolveMonsterDecalPathHook?.Disable();
        ResolveMonsterEidPathHook?.Disable();
        ResolveMonsterImcPathHook?.Disable();
        ResolveMonsterMPapPathHook?.Disable();
        ResolveMonsterMdlPathHook?.Disable();
        ResolveMonsterMtrlPathHook?.Disable();
        ResolveMonsterPapPathHook?.Disable();
        ResolveMonsterPhybPathHook?.Disable();
        ResolveMonsterSklbPathHook?.Disable();
        ResolveMonsterSkpPathHook?.Disable();
        ResolveMonsterTmbPathHook?.Disable();
        ResolveMonsterVfxPathHook?.Disable();
    }

    private void DisposeMonsterHooks()
    {
        ResolveMonsterDecalPathHook?.Dispose();
        ResolveMonsterEidPathHook?.Dispose();
        ResolveMonsterImcPathHook?.Dispose();
        ResolveMonsterMPapPathHook?.Dispose();
        ResolveMonsterMdlPathHook?.Dispose();
        ResolveMonsterMtrlPathHook?.Dispose();
        ResolveMonsterPapPathHook?.Dispose();
        ResolveMonsterPhybPathHook?.Dispose();
        ResolveMonsterSklbPathHook?.Dispose();
        ResolveMonsterSkpPathHook?.Dispose();
        ResolveMonsterTmbPathHook?.Dispose();
        ResolveMonsterVfxPathHook?.Dispose();
    }
}