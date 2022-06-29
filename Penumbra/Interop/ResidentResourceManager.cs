using Dalamud.Logging;
using Dalamud.Utility.Signatures;

namespace Penumbra.Interop;

public unsafe class ResidentResourceManager
{
    // Some attach and physics files are stored in the resident resource manager, and we need to manually trigger a reload of them to get them to apply.
    public delegate void* ResidentResourceDelegate( void* residentResourceManager );

    [Signature( "E8 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? BA ?? ?? ?? ?? 41 B8 ?? ?? ?? ?? 48 8B 48 30 48 8B 01 FF 50 10 48 85 C0 74 0A" )]
    public ResidentResourceDelegate LoadPlayerResources = null!;

    [Signature( "41 55 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 4C 8B E9 48 83 C1 08" )]
    public ResidentResourceDelegate UnloadPlayerResources = null!;

    // A static pointer to the resident resource manager address.
    [Signature( "0F 44 FE 48 8B 0D ?? ?? ?? ?? 48 85 C9 74 05", ScanType = ScanType.StaticAddress )]
    private readonly Structs.ResidentResourceManager** _residentResourceManagerAddress = null;

    public Structs.ResidentResourceManager* Address
        => *_residentResourceManagerAddress;

    public ResidentResourceManager()
    {
        SignatureHelper.Initialise( this );
    }

    // Reload certain player resources by force.
    public void Reload()
    {
        if( Address != null && Address->NumResources > 0 )
        {
            PluginLog.Debug( "Reload of resident resources triggered." );
            UnloadPlayerResources.Invoke( Address );
            LoadPlayerResources.Invoke( Address );
        }
    }
}