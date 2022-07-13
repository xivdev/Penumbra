using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    [Signature( "48 8D 05 ?? ?? ?? ?? 45 33 C0 48 89 03 BA", ScanType = ScanType.StaticAddress )]
    public IntPtr* DrawObjectDemiVTable;

    public Hook< GeneralResolveDelegate >? ResolveDemiDecalPathHook;
    public Hook< EidResolveDelegate >?     ResolveDemiEidPathHook;
    public Hook< GeneralResolveDelegate >? ResolveDemiImcPathHook;
    public Hook< MPapResolveDelegate >?    ResolveDemiMPapPathHook;
    public Hook< GeneralResolveDelegate >? ResolveDemiMdlPathHook;
    public Hook< MaterialResolveDetour >?  ResolveDemiMtrlPathHook;
    public Hook< MaterialResolveDetour >?  ResolveDemiPapPathHook;
    public Hook< GeneralResolveDelegate >? ResolveDemiPhybPathHook;
    public Hook< GeneralResolveDelegate >? ResolveDemiSklbPathHook;
    public Hook< GeneralResolveDelegate >? ResolveDemiSkpPathHook;
    public Hook< EidResolveDelegate >?     ResolveDemiTmbPathHook;
    public Hook< MaterialResolveDetour >?  ResolveDemiVfxPathHook;

    private void SetupDemiHooks()
    {
        ResolveDemiDecalPathHook = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectDemiVTable[ ResolveDecalIdx ], ResolveDemiDecalDetour );
        ResolveDemiEidPathHook   = Hook< EidResolveDelegate >.FromAddress( DrawObjectDemiVTable[ ResolveEidIdx ], ResolveDemiEidDetour );
        ResolveDemiImcPathHook   = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectDemiVTable[ ResolveImcIdx ], ResolveDemiImcDetour );
        ResolveDemiMPapPathHook  = Hook< MPapResolveDelegate >.FromAddress( DrawObjectDemiVTable[ ResolveMPapIdx ], ResolveDemiMPapDetour );
        ResolveDemiMdlPathHook   = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectDemiVTable[ ResolveMdlIdx ], ResolveDemiMdlDetour );
        ResolveDemiMtrlPathHook  = Hook< MaterialResolveDetour >.FromAddress( DrawObjectDemiVTable[ ResolveMtrlIdx ], ResolveDemiMtrlDetour );
        ResolveDemiPapPathHook   = Hook< MaterialResolveDetour >.FromAddress( DrawObjectDemiVTable[ ResolvePapIdx ], ResolveDemiPapDetour );
        ResolveDemiPhybPathHook  = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectDemiVTable[ ResolvePhybIdx ], ResolveDemiPhybDetour );
        ResolveDemiSklbPathHook  = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectDemiVTable[ ResolveSklbIdx ], ResolveDemiSklbDetour );
        ResolveDemiSkpPathHook   = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectDemiVTable[ ResolveSkpIdx ], ResolveDemiSkpDetour );
        ResolveDemiTmbPathHook   = Hook< EidResolveDelegate >.FromAddress( DrawObjectDemiVTable[ ResolveTmbIdx ], ResolveDemiTmbDetour );
        ResolveDemiVfxPathHook   = Hook< MaterialResolveDetour >.FromAddress( DrawObjectDemiVTable[ ResolveVfxIdx ], ResolveDemiVfxDetour );
    }

    private void EnableDemiHooks()
    {
        ResolveDemiDecalPathHook?.Enable();
        ResolveDemiEidPathHook?.Enable();
        ResolveDemiImcPathHook?.Enable();
        ResolveDemiMPapPathHook?.Enable();
        ResolveDemiMdlPathHook?.Enable();
        ResolveDemiMtrlPathHook?.Enable();
        ResolveDemiPapPathHook?.Enable();
        ResolveDemiPhybPathHook?.Enable();
        ResolveDemiSklbPathHook?.Enable();
        ResolveDemiSkpPathHook?.Enable();
        ResolveDemiTmbPathHook?.Enable();
        ResolveDemiVfxPathHook?.Enable();
    }

    private void DisableDemiHooks()
    {
        ResolveDemiDecalPathHook?.Disable();
        ResolveDemiEidPathHook?.Disable();
        ResolveDemiImcPathHook?.Disable();
        ResolveDemiMPapPathHook?.Disable();
        ResolveDemiMdlPathHook?.Disable();
        ResolveDemiMtrlPathHook?.Disable();
        ResolveDemiPapPathHook?.Disable();
        ResolveDemiPhybPathHook?.Disable();
        ResolveDemiSklbPathHook?.Disable();
        ResolveDemiSkpPathHook?.Disable();
        ResolveDemiTmbPathHook?.Disable();
        ResolveDemiVfxPathHook?.Disable();
    }

    private void DisposeDemiHooks()
    {
        ResolveDemiDecalPathHook?.Dispose();
        ResolveDemiEidPathHook?.Dispose();
        ResolveDemiImcPathHook?.Dispose();
        ResolveDemiMPapPathHook?.Dispose();
        ResolveDemiMdlPathHook?.Dispose();
        ResolveDemiMtrlPathHook?.Dispose();
        ResolveDemiPapPathHook?.Dispose();
        ResolveDemiPhybPathHook?.Dispose();
        ResolveDemiSklbPathHook?.Dispose();
        ResolveDemiSkpPathHook?.Dispose();
        ResolveDemiTmbPathHook?.Dispose();
        ResolveDemiVfxPathHook?.Dispose();
    }
}