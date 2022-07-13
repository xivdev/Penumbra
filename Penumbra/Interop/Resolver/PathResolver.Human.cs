using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

namespace Penumbra.Interop.Resolver;

// We can hook the different Resolve-Functions using just the VTable of Human.
// The other DrawObject VTables and the ResolveRoot function are currently unused.
public unsafe partial class PathResolver
{
    [Signature( "48 8D 05 ?? ?? ?? ?? 48 89 03 48 8D 8B ?? ?? ?? ?? 44 89 83 ?? ?? ?? ?? 48 8B C1", ScanType = ScanType.StaticAddress )]
    public IntPtr* DrawObjectHumanVTable;

    // [Signature( "48 8D 1D ?? ?? ?? ?? 48 C7 41", ScanType = ScanType.StaticAddress )]
    // public IntPtr* DrawObjectVTable;
    // 
    // public const int ResolveRootIdx  = 71;

    public const int ResolveSklbIdx  = 72;
    public const int ResolveMdlIdx   = 73;
    public const int ResolveSkpIdx   = 74;
    public const int ResolvePhybIdx  = 75;
    public const int ResolvePapIdx   = 76;
    public const int ResolveTmbIdx   = 77;
    public const int ResolveMPapIdx  = 79;
    public const int ResolveImcIdx   = 81;
    public const int ResolveMtrlIdx  = 82;
    public const int ResolveDecalIdx = 83;
    public const int ResolveVfxIdx   = 84;
    public const int ResolveEidIdx   = 85;

    public const int OnModelLoadCompleteIdx = 58;

    public delegate IntPtr GeneralResolveDelegate( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4 );
    public delegate IntPtr MPapResolveDelegate( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, uint unk5 );
    public delegate IntPtr MaterialResolveDetour( IntPtr drawObject, IntPtr path, IntPtr unk3, uint unk4, ulong unk5 );
    public delegate IntPtr EidResolveDelegate( IntPtr drawObject, IntPtr path, IntPtr unk3 );

    public delegate void OnModelLoadCompleteDelegate( IntPtr drawObject );

    public Hook< GeneralResolveDelegate >? ResolveDecalPathHook;
    public Hook< EidResolveDelegate >?     ResolveEidPathHook;
    public Hook< GeneralResolveDelegate >? ResolveImcPathHook;
    public Hook< MPapResolveDelegate >?    ResolveMPapPathHook;
    public Hook< GeneralResolveDelegate >? ResolveMdlPathHook;
    public Hook< MaterialResolveDetour >?  ResolveMtrlPathHook;
    public Hook< MaterialResolveDetour >?  ResolvePapPathHook;
    public Hook< GeneralResolveDelegate >? ResolvePhybPathHook;
    public Hook< GeneralResolveDelegate >? ResolveSklbPathHook;
    public Hook< GeneralResolveDelegate >? ResolveSkpPathHook;
    public Hook< EidResolveDelegate >?     ResolveTmbPathHook;
    public Hook< MaterialResolveDetour >?  ResolveVfxPathHook;


    private void SetupHumanHooks()
    {
        ResolveDecalPathHook = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectHumanVTable[ ResolveDecalIdx ], ResolveDecalDetour );
        ResolveEidPathHook   = Hook< EidResolveDelegate >.FromAddress( DrawObjectHumanVTable[ ResolveEidIdx ], ResolveEidDetour );
        ResolveImcPathHook   = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectHumanVTable[ ResolveImcIdx ], ResolveImcDetour );
        ResolveMPapPathHook  = Hook< MPapResolveDelegate >.FromAddress( DrawObjectHumanVTable[ ResolveMPapIdx ], ResolveMPapDetour );
        ResolveMdlPathHook   = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectHumanVTable[ ResolveMdlIdx ], ResolveMdlDetour );
        ResolveMtrlPathHook  = Hook< MaterialResolveDetour >.FromAddress( DrawObjectHumanVTable[ ResolveMtrlIdx ], ResolveMtrlDetour );
        ResolvePapPathHook   = Hook< MaterialResolveDetour >.FromAddress( DrawObjectHumanVTable[ ResolvePapIdx ], ResolvePapDetour );
        ResolvePhybPathHook  = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectHumanVTable[ ResolvePhybIdx ], ResolvePhybDetour );
        ResolveSklbPathHook  = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectHumanVTable[ ResolveSklbIdx ], ResolveSklbDetour );
        ResolveSkpPathHook   = Hook< GeneralResolveDelegate >.FromAddress( DrawObjectHumanVTable[ ResolveSkpIdx ], ResolveSkpDetour );
        ResolveTmbPathHook   = Hook< EidResolveDelegate >.FromAddress( DrawObjectHumanVTable[ ResolveTmbIdx ], ResolveTmbDetour );
        ResolveVfxPathHook   = Hook< MaterialResolveDetour >.FromAddress( DrawObjectHumanVTable[ ResolveVfxIdx ], ResolveVfxDetour );
    }

    private void EnableHumanHooks()
    {
        ResolveDecalPathHook?.Enable();
        ResolveEidPathHook?.Enable();
        ResolveImcPathHook?.Enable();
        ResolveMPapPathHook?.Enable();
        ResolveMdlPathHook?.Enable();
        ResolveMtrlPathHook?.Enable();
        ResolvePapPathHook?.Enable();
        ResolvePhybPathHook?.Enable();
        ResolveSklbPathHook?.Enable();
        ResolveSkpPathHook?.Enable();
        ResolveTmbPathHook?.Enable();
        ResolveVfxPathHook?.Enable();
    }

    private void DisableHumanHooks()
    {
        ResolveDecalPathHook?.Disable();
        ResolveEidPathHook?.Disable();
        ResolveImcPathHook?.Disable();
        ResolveMPapPathHook?.Disable();
        ResolveMdlPathHook?.Disable();
        ResolveMtrlPathHook?.Disable();
        ResolvePapPathHook?.Disable();
        ResolvePhybPathHook?.Disable();
        ResolveSklbPathHook?.Disable();
        ResolveSkpPathHook?.Disable();
        ResolveTmbPathHook?.Disable();
        ResolveVfxPathHook?.Disable();
    }

    private void DisposeHumanHooks()
    {
        ResolveDecalPathHook?.Dispose();
        ResolveEidPathHook?.Dispose();
        ResolveImcPathHook?.Dispose();
        ResolveMPapPathHook?.Dispose();
        ResolveMdlPathHook?.Dispose();
        ResolveMtrlPathHook?.Dispose();
        ResolvePapPathHook?.Dispose();
        ResolvePhybPathHook?.Dispose();
        ResolveSklbPathHook?.Dispose();
        ResolveSkpPathHook?.Dispose();
        ResolveTmbPathHook?.Dispose();
        ResolveVfxPathHook?.Dispose();
    }
}