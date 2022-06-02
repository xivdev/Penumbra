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
        ResolveWeaponDecalPathHook = new Hook< GeneralResolveDelegate >( DrawObjectWeaponVTable[ ResolveDecalIdx ], ResolveWeaponDecalDetour );
        ResolveWeaponEidPathHook   = new Hook< EidResolveDelegate >( DrawObjectWeaponVTable[ ResolveEidIdx ], ResolveWeaponEidDetour );
        ResolveWeaponImcPathHook   = new Hook< GeneralResolveDelegate >( DrawObjectWeaponVTable[ ResolveImcIdx ], ResolveWeaponImcDetour );
        ResolveWeaponMPapPathHook  = new Hook< MPapResolveDelegate >( DrawObjectWeaponVTable[ ResolveMPapIdx ], ResolveWeaponMPapDetour );
        ResolveWeaponMdlPathHook   = new Hook< GeneralResolveDelegate >( DrawObjectWeaponVTable[ ResolveMdlIdx ], ResolveWeaponMdlDetour );
        ResolveWeaponMtrlPathHook  = new Hook< MaterialResolveDetour >( DrawObjectWeaponVTable[ ResolveMtrlIdx ], ResolveWeaponMtrlDetour );
        ResolveWeaponPapPathHook   = new Hook< MaterialResolveDetour >( DrawObjectWeaponVTable[ ResolvePapIdx ], ResolveWeaponPapDetour );
        ResolveWeaponPhybPathHook  = new Hook< GeneralResolveDelegate >( DrawObjectWeaponVTable[ ResolvePhybIdx ], ResolveWeaponPhybDetour );
        ResolveWeaponSklbPathHook  = new Hook< GeneralResolveDelegate >( DrawObjectWeaponVTable[ ResolveSklbIdx ], ResolveWeaponSklbDetour );
        ResolveWeaponSkpPathHook   = new Hook< GeneralResolveDelegate >( DrawObjectWeaponVTable[ ResolveSkpIdx ], ResolveWeaponSkpDetour );
        ResolveWeaponTmbPathHook   = new Hook< EidResolveDelegate >( DrawObjectWeaponVTable[ ResolveTmbIdx ], ResolveWeaponTmbDetour );
        ResolveWeaponVfxPathHook   = new Hook< MaterialResolveDetour >( DrawObjectWeaponVTable[ ResolveVfxIdx ], ResolveWeaponVfxDetour );
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