using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    [Signature( "48 8D 05 ?? ?? ?? ?? 48 89 03 B8 ?? ?? ?? ?? 66 89 83 ?? ?? ?? ?? 48 8B C3 48 89 8B ?? ?? ?? ?? 48 89 8B",
        ScanType = ScanType.StaticAddress )]
    public IntPtr* DrawObjectWeaponVTable;

    public Hook< GeneralResolveDelegate >? ResolveWeaponDecalPathHook;
    public Hook< EidResolveDelegate >?     ResolveWeaponEidPathHook;
    public Hook< GeneralResolveDelegate >? ResolveWeaponImcPathHook;
    public Hook< MPapResolveDelegate >?    ResolveWeaponMPapPathHook;
    public Hook< GeneralResolveDelegate >? ResolveWeaponMdlPathHook;
    public Hook< MaterialResolveDetour >?  ResolveWeaponMtrlPathHook;
    public Hook< MaterialResolveDetour >?  ResolveWeaponPapPathHook;
    public Hook< GeneralResolveDelegate >? ResolveWeaponPhybPathHook;
    public Hook< GeneralResolveDelegate >? ResolveWeaponSklbPathHook;
    public Hook< GeneralResolveDelegate >? ResolveWeaponSkpPathHook;
    public Hook< EidResolveDelegate >?     ResolveWeaponTmbPathHook;
    public Hook< MaterialResolveDetour >?  ResolveWeaponVfxPathHook;

    private void SetupWeaponHooks()
    {
        ResolveWeaponDecalPathHook = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectWeaponVTable[ ResolveDecalIdx ], ResolveWeaponDecalDetour );
        ResolveWeaponEidPathHook   = Hook< EidResolveDelegate >.FromAddress( DrawObjectWeaponVTable[ ResolveEidIdx ], ResolveWeaponEidDetour );
        ResolveWeaponImcPathHook   = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectWeaponVTable[ ResolveImcIdx ], ResolveWeaponImcDetour );
        ResolveWeaponMPapPathHook  = Hook< MPapResolveDelegate >.FromAddress( DrawObjectWeaponVTable[ ResolveMPapIdx ], ResolveWeaponMPapDetour );
        ResolveWeaponMdlPathHook   = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectWeaponVTable[ ResolveMdlIdx ], ResolveWeaponMdlDetour );
        ResolveWeaponMtrlPathHook  = Hook< MaterialResolveDetour >.FromAddress( DrawObjectWeaponVTable[ ResolveMtrlIdx ], ResolveWeaponMtrlDetour );
        ResolveWeaponPapPathHook   = Hook< MaterialResolveDetour >.FromAddress( DrawObjectWeaponVTable[ ResolvePapIdx ], ResolveWeaponPapDetour );
        ResolveWeaponPhybPathHook  = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectWeaponVTable[ ResolvePhybIdx ], ResolveWeaponPhybDetour );
        ResolveWeaponSklbPathHook  = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectWeaponVTable[ ResolveSklbIdx ], ResolveWeaponSklbDetour );
        ResolveWeaponSkpPathHook   = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectWeaponVTable[ ResolveSkpIdx ], ResolveWeaponSkpDetour );
        ResolveWeaponTmbPathHook   = Hook< EidResolveDelegate >.FromAddress( DrawObjectWeaponVTable[ ResolveTmbIdx ], ResolveWeaponTmbDetour );
        ResolveWeaponVfxPathHook   = Hook< MaterialResolveDetour >.FromAddress( DrawObjectWeaponVTable[ ResolveVfxIdx ], ResolveWeaponVfxDetour );
    }

    private void EnableWeaponHooks()
    {
        ResolveWeaponDecalPathHook?.Enable();
        ResolveWeaponEidPathHook?.Enable();
        ResolveWeaponImcPathHook?.Enable();
        ResolveWeaponMPapPathHook?.Enable();
        ResolveWeaponMdlPathHook?.Enable();
        ResolveWeaponMtrlPathHook?.Enable();
        ResolveWeaponPapPathHook?.Enable();
        ResolveWeaponPhybPathHook?.Enable();
        ResolveWeaponSklbPathHook?.Enable();
        ResolveWeaponSkpPathHook?.Enable();
        ResolveWeaponTmbPathHook?.Enable();
        ResolveWeaponVfxPathHook?.Enable();
    }

    private void DisableWeaponHooks()
    {
        ResolveWeaponDecalPathHook?.Disable();
        ResolveWeaponEidPathHook?.Disable();
        ResolveWeaponImcPathHook?.Disable();
        ResolveWeaponMPapPathHook?.Disable();
        ResolveWeaponMdlPathHook?.Disable();
        ResolveWeaponMtrlPathHook?.Disable();
        ResolveWeaponPapPathHook?.Disable();
        ResolveWeaponPhybPathHook?.Disable();
        ResolveWeaponSklbPathHook?.Disable();
        ResolveWeaponSkpPathHook?.Disable();
        ResolveWeaponTmbPathHook?.Disable();
        ResolveWeaponVfxPathHook?.Disable();
    }

    private void DisposeWeaponHooks()
    {
        ResolveWeaponDecalPathHook?.Dispose();
        ResolveWeaponEidPathHook?.Dispose();
        ResolveWeaponImcPathHook?.Dispose();
        ResolveWeaponMPapPathHook?.Dispose();
        ResolveWeaponMdlPathHook?.Dispose();
        ResolveWeaponMtrlPathHook?.Dispose();
        ResolveWeaponPapPathHook?.Dispose();
        ResolveWeaponPhybPathHook?.Dispose();
        ResolveWeaponSklbPathHook?.Dispose();
        ResolveWeaponSkpPathHook?.Dispose();
        ResolveWeaponTmbPathHook?.Dispose();
        ResolveWeaponVfxPathHook?.Dispose();
    }
}