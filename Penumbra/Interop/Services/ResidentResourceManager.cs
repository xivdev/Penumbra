using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using Penumbra.GameData;

namespace Penumbra.Interop.Services;

public unsafe class ResidentResourceManager
{
    // A static pointer to the resident resource manager address.
    [Signature(Sigs.ResidentResourceManager, ScanType = ScanType.StaticAddress)]
    private readonly Structs.ResidentResourceManager** _residentResourceManagerAddress = null;

    // Some attach and physics files are stored in the resident resource manager, and we need to manually trigger a reload of them to get them to apply.
    public delegate void* ResidentResourceDelegate(void* residentResourceManager);

    [Signature(Sigs.LoadPlayerResources)]
    public readonly ResidentResourceDelegate LoadPlayerResources = null!;

    [Signature(Sigs.UnloadPlayerResources)]
    public readonly ResidentResourceDelegate UnloadPlayerResources = null!;

    public Structs.ResidentResourceManager* Address
        => *_residentResourceManagerAddress;

    public ResidentResourceManager(IGameInteropProvider interop)
        => interop.InitializeFromAttributes(this);

    // Reload certain player resources by force.
    public void Reload()
    {
        if (Address != null && Address->NumResources > 0)
        {
            Penumbra.Log.Debug("Reload of resident resources triggered.");
            UnloadPlayerResources.Invoke(Address);
            LoadPlayerResources.Invoke(Address);
        }
    }
}
